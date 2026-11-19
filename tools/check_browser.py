"""Small Edge/CDP smoke check; no additional browser/test framework required."""
import asyncio
import base64
import json
from pathlib import Path
import subprocess
import sys
import tempfile

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from aiohttp import ClientSession, web
from host import Host, neutral

ROOT=Path(__file__).resolve().parents[1]


class CDP:
    def __init__(self,ws):self.ws=ws;self.counter=0;self.errors=[]
    async def command(self,method,**params):
        self.counter+=1
        await self.ws.send_json({'id':self.counter,'method':method,'params':params})
        while True:
            message=await self.ws.receive_json(timeout=15)
            if message.get('method')=='Runtime.exceptionThrown':
                self.errors.append(message['params']['exceptionDetails'])
            if message.get('id')==self.counter:
                if 'error' in message:raise RuntimeError(message['error'])
                return message.get('result',{})
    async def evaluate(self,expression):
        result=await self.command('Runtime.evaluate',expression=expression,returnByValue=True,awaitPromise=True)
        if 'exceptionDetails' in result:raise RuntimeError(result['exceptionDetails'])
        return result.get('result',{}).get('value')
    async def wait(self,expression):
        for _ in range(80):
            if await self.evaluate(expression):return
            await asyncio.sleep(.1)
        raise AssertionError('Timed out: '+expression)
    async def screenshot(self,path):
        shot=await self.command('Page.captureScreenshot',format='png',captureBeyondViewport=False)
        Path(path).write_bytes(base64.b64decode(shot['data']))


async def check_driving(controller,host):
    """Exercise the phone-to-host path, including analog levels and release."""
    assert await controller.evaluate("document.querySelector('#playerDrivingToggle').getAttribute('aria-pressed')==='true'")
    boxes=await controller.evaluate("['left-stick','right-stick'].map(id=>{let r=document.querySelector('#playerSurface [data-id='+id+']').getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}})")
    def point(index,x,y):
        box=boxes[index]
        return dict(id=51+index,x=box['x']+box['w']*x,y=box['y']+box['h']*y)
    async def touch(kind,*points):
        await controller.command('Input.dispatchTouchEvent',type=kind,touchPoints=list(points))
        await asyncio.sleep(.07)
    await touch('touchStart',point(0,.52,.48))
    assert host.state==neutral(),'Driving deadzone did not center'
    await touch('touchMove',point(0,.5,.225))
    assert .47<host.state['rt']<.53 and host.state['ly']==0,f'Half throttle failed: {host.state}'
    await touch('touchMove',point(0,.5,.01))
    assert host.state['rt']>.95 and host.state['lx']==0,'Full throttle failed'
    await touch('touchMove',point(0,.99,.01))
    assert host.state['rt']>.95 and host.state['lx']>.95,'Turning reduced full throttle'
    await touch('touchStart',point(0,.99,.01),point(1,.25,.75))
    assert host.state['rt']>.95 and host.state['lx']>.95 and host.state['rx']<-.3 and host.state['ry']>.3,'Driving and camera multitouch failed'
    await touch('touchEnd')
    assert host.state==neutral(),'Driving release stuck'
    await touch('touchStart',point(0,.01,.5))
    assert host.state['lx']<-.95 and host.state['rt']==0 and host.state['lt']==0,'Steering alone applied a pedal'
    await touch('touchMove',point(0,.5,.775))
    assert .47<host.state['lt']<.53 and host.state['rt']==0,'Proportional brake failed'
    await touch('touchMove',point(0,.5,.99))
    assert host.state['lt']>.95 and host.state['rt']==0,'Full brake/reverse failed'
    await touch('touchCancel')
    assert host.state==neutral(),'Cancelled driving touch stuck'
    await touch('touchStart',point(0,.5,.01))
    await controller.evaluate("document.querySelector('#playerDrivingToggle').click()")
    await asyncio.sleep(.07)
    assert host.state==neutral(),'Mode switch left throttle held'
    await touch('touchMove',point(0,.5,.02))
    assert host.state==neutral(),'Cleared touch resumed after mode switch'
    await touch('touchEnd')
    await touch('touchStart',point(0,.5,.01))
    assert host.state['ly']<-.95 and host.state['rt']==0,'On-foot movement was not restored'
    await touch('touchEnd')
    await controller.command('Page.reload')
    await controller.wait("document.querySelectorAll('#playerSurface .control').length>10")
    assert await controller.evaluate("document.querySelector('#playerDrivingToggle').getAttribute('aria-pressed')==='false'"),'Driving preference was not restored'
    await controller.evaluate("document.querySelector('#playerDrivingToggle').click();document.querySelector('#startPlaying').click()")
    await controller.wait("document.querySelector('#playerOverlay').hidden")
    await controller.evaluate("document.fullscreenElement?document.exitFullscreen():null")


async def main():
    results=ROOT/'test-results';results.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='pocketpad-browser-') as profile, tempfile.TemporaryDirectory() as data:
        host=Host(8877,mock=True,data_dir=data)
        runner=web.AppRunner(host.app());await runner.setup()
        await web.TCPSite(runner,'127.0.0.1',8877).start()
        process=subprocess.Popen([r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
            '--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check',
            '--remote-debugging-port=19365',f'--user-data-dir={profile}','about:blank'],
            stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            async with ClientSession() as session:
                for _ in range(100):
                    try:
                        async with session.get('http://localhost:19365/json/version') as r:
                            if r.status==200:break
                    except OSError:pass
                    await asyncio.sleep(.1)
                async def page(url,width,height,mobile=False,native=False):
                    async with session.put('http://localhost:19365/json/new?about:blank') as r:target=await r.json()
                    ws=await session.ws_connect(target['webSocketDebuggerUrl'])
                    c=CDP(ws)
                    await c.command('Runtime.enable');await c.command('Page.enable')
                    await c.command('Page.addScriptToEvaluateOnNewDocument',source="try{localStorage.setItem('pocketpad-tour-complete','1');localStorage.setItem('pocketpad-experience',JSON.stringify({appearance:'matte',animations:false,haptics:true}));}catch(e){}")
                    if native:
                        await c.command('Page.addScriptToEvaluateOnNewDocument',source="window.__nativeCalls=[];window.__prefs={appearance:'matte',animations:false,haptics:true};window.PocketPadNative={getPreferences(){return JSON.stringify(window.__prefs)},savePreferences(value){Object.assign(window.__prefs,JSON.parse(value))},connectionCue(state){window.__nativeCalls.push('cue-'+state)},tourCompleted(){return localStorage.getItem('pocketpad-tour-complete')==='1'},completeTour(){localStorage.setItem('pocketpad-tour-complete','1')},clickHaptic(){window.__nativeCalls.push('haptic')},hasMotion(){return true},getMotionMode(){return 'off'},getMotionPreferences(){return JSON.stringify(window.__gyroPrefs||{})},saveMotionPreferences(json){window.__gyroPrefs=JSON.parse(json)},setMotion(mode){window.__nativeCalls.push(mode)},recenter(){window.__nativeCalls.push('recenter')},fullscreen(){window.__nativeCalls.push('fullscreen')},home(){window.__nativeCalls.push('home')}}")
                    await c.command('Emulation.setDeviceMetricsOverride',width=width,height=height,deviceScaleFactor=1,mobile=mobile)
                    if mobile:await c.command('Emulation.setTouchEmulationEnabled',enabled=True,maxTouchPoints=5)
                    await c.command('Page.navigate',url=url)
                    return c
                desktop=await page('http://localhost:8877',1440,1000)
                await desktop.wait("document.querySelectorAll('.preset-item').length===4")
                await desktop.screenshot(results/'desktop.png')
                await desktop.evaluate("document.querySelector('[title=\"Add Left bumper\"]').click()")
                await desktop.wait("document.querySelector('#controlLabel')!==null")
                await desktop.evaluate("document.querySelector('#controlLabel').value='BOOST';document.querySelector('#controlLabel').dispatchEvent(new Event('change'))")
                await desktop.evaluate("document.querySelector('#controlSize').value='120';document.querySelector('#controlSize').dispatchEvent(new Event('input'));document.querySelector('#controlSize').dispatchEvent(new Event('change'))")
                await desktop.evaluate("document.querySelector('#saveButton').click();document.querySelector('#presetName').value='Racing custom';document.querySelector('#saveSlot').value='2';document.querySelector('#saveForm').requestSubmit()")
                await desktop.wait("document.querySelector('.preset-item.active strong').textContent==='Racing custom'")
                assert host.custom[0]['name']=='Racing custom'
                assert host.custom[0]['controls'][-1]['label']=='BOOST'
                assert host.custom[0]['controls'][-1]['w']>178,'Individual control size was not saved'
                before=await desktop.evaluate("(()=>{let e=document.querySelector('#editorSurface .control:last-child');let r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,left:e.style.left}})()")
                await desktop.command('Input.dispatchMouseEvent',type='mousePressed',x=before['x'],y=before['y'],button='left',clickCount=1)
                await desktop.command('Input.dispatchMouseEvent',type='mouseMoved',x=before['x']+40,y=before['y']+25,button='left',buttons=1)
                await desktop.command('Input.dispatchMouseEvent',type='mouseReleased',x=before['x']+40,y=before['y']+25,button='left')
                after=await desktop.evaluate("document.querySelector('#editorSurface .control:last-child').style.left")
                assert before['left']!=after, 'HUD drag did not move control'
                handle=await desktop.evaluate("(()=>{let r=document.querySelector('#editorSurface .selected .resize-handle').getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,w:Number(document.querySelector('#controlW').value)}})()")
                await desktop.command('Input.dispatchMouseEvent',type='mousePressed',x=handle['x'],y=handle['y'],button='left',clickCount=1)
                await desktop.command('Input.dispatchMouseEvent',type='mouseMoved',x=handle['x']+25,y=handle['y']+10,button='left',buttons=1)
                await desktop.command('Input.dispatchMouseEvent',type='mouseReleased',x=handle['x']+25,y=handle['y']+10,button='left')
                assert await desktop.evaluate("Number(document.querySelector('#controlW').value)")>handle['w'],'Corner resize failed'
                await desktop.evaluate("document.querySelector('#saveButton').click();document.querySelector('#saveForm').requestSubmit()")
                await desktop.wait("document.querySelector('#draftBadge').hidden")
                await desktop.evaluate("document.querySelector('[data-tab=connect]').click()")
                await desktop.wait("document.querySelector('#qrImage').complete&&document.querySelector('#qrImage').naturalWidth>0")
                await desktop.screenshot(results/'pairing.png')
                phone=await page('http://localhost:8877/play?token='+host.token,932,430,True)
                await phone.wait("document.querySelectorAll('#playerSurface .control').length>10")
                await phone.evaluate("document.querySelector('#startPlaying').click()")
                await phone.wait("document.querySelector('#playerOverlay').hidden")
                # Avoid browser fullscreen modifying device metrics during touch checks.
                await phone.evaluate("document.fullscreenElement?document.exitFullscreen():null")
                await asyncio.sleep(.2)
                points=await phone.evaluate("(()=>{let ids=['face-A','left-stick'];return ids.map(id=>{let r=document.querySelector('#playerSurface [data-id=\"'+id+'\"]').getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,w:r.width}})})()")
                touches=[{'x':points[0]['x'],'y':points[0]['y'],'id':1},{'x':points[1]['x']+points[1]['w']*.4,'y':points[1]['y'],'id':2}]
                await phone.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=touches)
                await asyncio.sleep(.15)
                assert 'A' in host.state['buttons'],f'Face button missing: {host.state}'
                assert host.state['lx']>.5,f'Stick missing during simultaneous touch: {host.state}'
                await phone.screenshot(results/'phone.png')
                await phone.command('Input.dispatchTouchEvent',type='touchEnd',touchPoints=[])
                await asyncio.sleep(.15)
                assert host.state==neutral(),f'Inputs stuck after release: {host.state}'
                await check_driving(phone,host)
                full_width=await phone.evaluate("document.querySelector('#playerSurface').clientWidth===document.querySelector('#player').clientWidth")
                assert full_width,'Phone controller is letterboxed horizontally'
                stick_width=await phone.evaluate("document.querySelector('#playerSurface [data-id=\"left-stick\"]').getBoundingClientRect().width")
                await phone.evaluate("document.querySelector('#phoneControlSize').value='120';document.querySelector('#phoneControlSize').dispatchEvent(new Event('input'))")
                bigger=await phone.evaluate("document.querySelector('#playerSurface [data-id=\"left-stick\"]').getBoundingClientRect().width")
                assert bigger>stick_width*1.15,'Phone size slider failed'
                await phone.evaluate("document.querySelector('#phoneSizeReset').click()")
                # Switch to the factory PS5 HUD and create the second custom slot.
                await phone.evaluate("document.querySelector('#playerPresetButton').click();document.querySelectorAll('#playerPresetMenu button')[1].click()")
                await phone.wait("document.querySelector('#playerSurface').classList.contains('ps5')")
                await check_driving(phone,host)
                await phone.screenshot(results/'ps5.png')
                touchpad=await phone.evaluate("(()=>{let r=document.querySelector('#playerSurface [data-id=\"touchpad\"]').getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}})()")
                points=[{'id':11,'x':touchpad['x']+touchpad['w']*.25,'y':touchpad['y']+touchpad['h']*.4},
                        {'id':12,'x':touchpad['x']+touchpad['w']*.75,'y':touchpad['y']+touchpad['h']*.6}]
                await phone.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=points)
                await asyncio.sleep(.08)
                assert len(host.state['touches'])==2,host.state
                assert abs(host.state['touches'][0]['x']-.25)<.03
                click=await phone.evaluate("(()=>{let r=document.querySelector('#playerSurface [data-id=\"touch-click\"]').getBoundingClientRect();return {id:13,x:r.x+r.width/2,y:r.y+r.height/2}})()")
                await phone.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=points+[click])
                await asyncio.sleep(.08)
                assert host.state['touchClick'],'Separate touchpad click failed'
                assert len(host.state['touches'])==2,'Click lost touch contacts'
                points[0]['x']+=touchpad['w']*.1
                await phone.command('Input.dispatchTouchEvent',type='touchMove',touchPoints=points+[click])
                await asyncio.sleep(.08)
                assert abs(host.state['touches'][0]['x']-.35)<.03,'Touch position did not update'
                await phone.screenshot(results/'ps5-touch.png')
                await phone.command('Input.dispatchTouchEvent',type='touchEnd',touchPoints=[])
                await asyncio.sleep(.2)
                assert host.state==neutral(),host.state
                await phone.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=points)
                await phone.command('Input.dispatchTouchEvent',type='touchEnd',touchPoints=[])
                await asyncio.sleep(.025)
                assert host.state['touchClick'],'Two-finger tap click failed'
                await asyncio.sleep(.18)
                assert host.state==neutral(),'Tap click did not release'
                await desktop.evaluate("document.querySelector('[data-tab=editor]').click();document.querySelector('#saveButton').click();document.querySelector('#presetName').value='PS custom';document.querySelector('#saveSlot').value='3';document.querySelector('#saveForm').requestSubmit()")
                await desktop.wait("document.querySelector('.preset-item.active strong').textContent==='PS custom'")
                assert all(host.custom),'Both custom presets were not stored'
                await phone.evaluate("document.querySelector('#exitPlayer').click()")
                await asyncio.sleep(.2)
                assert host.owner is None
                native=await page('http://localhost:8877/play?demo=1',932,430,True,True)
                await native.wait("document.querySelectorAll('#playerSurface .control').length>10")
                assert not await native.evaluate("document.querySelector('#playerMotionButton').hidden"),'Native gyro menu missing'
                await native.evaluate("document.querySelector('#startPlaying').click()")
                await native.wait("document.querySelector('#playerOverlay').hidden")
                assert host.owner is None,'Offline preview claimed the PC controller'
                await native.evaluate("document.querySelector('#motionMode').value='steer';document.querySelector('#motionMode').dispatchEvent(new Event('change'));window.dispatchEvent(new CustomEvent('pocketpad-motion',{detail:{roll:15,pitch:0}}))")
                assert await native.evaluate("window.__nativeCalls.includes('steer')"),'Native motion bridge not invoked'
                await native.evaluate("document.querySelector('#motionRecenter').click()")
                assert await native.evaluate("window.__nativeCalls.includes('recenter')")
                await native.screenshot(results/'android-preview.png')
                await native.evaluate("window.dispatchEvent(new Event('pocketpad-pause'))")
                await native.wait("!document.querySelector('#playerOverlay').hidden")
                await native.command('Page.navigate',url='http://localhost:8877/play?token='+host.token)
                await native.wait("document.querySelectorAll('#playerSurface .control').length>10")
                await native.evaluate("document.querySelector('#startPlaying').click()")
                await native.wait("document.querySelector('#playerOverlay').hidden")
                await native.evaluate("document.querySelector('#motionMode').value='steer';document.querySelector('#motionMode').dispatchEvent(new Event('change'));for(let i=0;i<12;i++)window.dispatchEvent(new CustomEvent('pocketpad-motion',{detail:{roll:30,pitch:0}}))")
                await asyncio.sleep(.1)
                assert host.state['lx']>.9,'Native gyro did not reach the PC input stream'
                # Quick off must immediately clear motion, then restore the last profile.
                await native.evaluate("document.querySelector('#playerGyroToggle').click()")
                await asyncio.sleep(.07)
                assert host.state['lx']==0,'Gyro off left the steering axis active'
                assert await native.evaluate("document.querySelector('#playerGyroToggle').getAttribute('aria-pressed')==='false'")
                await native.evaluate("document.querySelector('#playerGyroToggle').click()")
                assert await native.evaluate("document.querySelector('#motionMode').value==='steer'")
                await native.evaluate("document.querySelector('#motionMode').value='aim';document.querySelector('#motionMode').dispatchEvent(new Event('change'));window.__gyro=()=>{for(let i=0;i<12;i++)window.dispatchEvent(new CustomEvent('pocketpad-motion',{detail:{roll:25,pitch:15,yawRate:-1.8,pitchRate:.9}}))};window.__gyro()")
                await asyncio.sleep(.06)
                assert host.state['rx']==0 and host.state['ry']==0,'Aim-only gyro moved the camera without LT/L2'
                lt=await native.evaluate("(()=>{let el=[...document.querySelectorAll('#playerSurface .control')].find(e=>e.dataset.id==='left-trigger');if(!el)el=document.querySelector('#playerSurface .trigger');let r=el.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+2}})()")
                await native.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=[dict(id=31,**lt)])
                await native.evaluate("window.__gyro()")
                await asyncio.sleep(.06)
                assert host.state['lt']>.8 and host.state['rx']>.9 and host.state['ry']<-.4,f'Gated gyro aiming failed: {host.state}'
                # Physical camera stick takes priority, even while aiming.
                stick=await native.evaluate("(()=>{let r=document.querySelector('#playerSurface [data-id=right-stick]').getBoundingClientRect();return {x:r.x+r.width*.12,y:r.y+r.height/2}})()")
                await native.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=[dict(id=31,**lt),dict(id=32,**stick)])
                await native.evaluate("window.__gyro()")
                await asyncio.sleep(.06)
                assert host.state['rx']<-.4 and host.state['ry']==0,'Gyro fought manual right-stick input'
                await native.command('Input.dispatchTouchEvent',type='touchEnd',touchPoints=[])
                await native.evaluate("document.querySelector('#motionAimOnly').checked=false;document.querySelector('#motionAimOnly').dispatchEvent(new Event('change'));window.__gyro()")
                await asyncio.sleep(.06)
                assert host.state['rx']>.9,'Always-on gyro mode failed'
                await native.evaluate("window.dispatchEvent(new CustomEvent('pocketpad-motion',{detail:{roll:25,pitch:15,yawRate:0,pitchRate:0}}))")
                await asyncio.sleep(.06)
                assert host.state['rx']==0 and host.state['ry']==0,'Stationary phone kept moving the camera'
                await native.evaluate("window.__gyro()")
                await asyncio.sleep(.24)
                assert host.state['rx']==0,'Stale sensor output remained active'
                await native.evaluate("document.querySelector('#motionSensitivity').value='60';document.querySelector('#motionSensitivity').dispatchEvent(new Event('input'));document.querySelector('#motionInvertX').checked=true;document.querySelector('#motionInvertX').dispatchEvent(new Event('change'));window.__gyro()")
                await asyncio.sleep(.06)
                assert -.65<host.state['rx']<-.5,'Sensitivity/invert settings were ignored'
                assert await native.evaluate("JSON.parse(localStorage.getItem('pocketpad-gyro')).sensitivity===60&&window.__gyroPrefs.sensitivity===60"),'Gyro settings not persisted'
                await native.evaluate("document.querySelector('#playerMotionButton').click()")
                await native.screenshot(results/'gyro-aim.png')
                await native.evaluate("document.querySelector('#motionRecenter').click()")
                await asyncio.sleep(.06)
                assert host.state['rx']==0,'Recenter did not release gyro output'
                await native.evaluate("window.dispatchEvent(new Event('pocketpad-pause'))")
                await asyncio.sleep(.1)
                assert host.owner is None and host.state==neutral(),'Native pause left game inputs active'
                assert not native.errors,native.errors
                await native.ws.close()
                assert not desktop.errors,desktop.errors
                assert not phone.errors,phone.errors
                print('PASS: editor, resizing, phone sizing, presets, QR, driving throttle/steering/brake/camera on both layouts, mode persistence/release, multitouch, native touchpad, click/release, Android bridge, offline preview, gyro controls, and app pause.')
                await desktop.ws.close();await phone.ws.close()
        finally:
            process.terminate()
            await asyncio.to_thread(process.wait,10)
            await runner.cleanup()


if __name__=='__main__':asyncio.run(main())
