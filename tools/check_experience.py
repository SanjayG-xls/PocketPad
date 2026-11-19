"""Focused Android-WebView experience checks through an isolated Edge/CDP session."""
import asyncio
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT));sys.path.insert(0,str(ROOT/'tools'))
from aiohttp import ClientSession,web
from host import Host,neutral
from check_browser import CDP


async def main():
    output=ROOT/'test-results/experience';output.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='pocketpad-experience-') as profile,tempfile.TemporaryDirectory() as data:
        host=Host(8882,mock=True,data_dir=data)
        runner=web.AppRunner(host.app());await runner.setup();await web.TCPSite(runner,'127.0.0.1',8882).start()
        process=subprocess.Popen([r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe','--headless=new','--disable-gpu','--no-first-run','--remote-debugging-port=19375',f'--user-data-dir={profile}','about:blank'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            async with ClientSession() as session:
                for _ in range(100):
                    try:
                        async with session.get('http://localhost:19375/json/version') as r:
                            if r.status==200:break
                    except OSError:pass
                    await asyncio.sleep(.1)
                async with session.put('http://localhost:19375/json/new?about:blank') as r:target=await r.json()
                c=CDP(await session.ws_connect(target['webSocketDebuggerUrl']))
                await c.command('Runtime.enable');await c.command('Page.enable')
                await c.command('Page.addScriptToEvaluateOnNewDocument',source="window.__calls=[];window.__prefs={appearance:'matte',animations:true,haptics:true};window.__tourDone=false;window.PocketPadNative={getPreferences(){return JSON.stringify(window.__prefs)},savePreferences(json){Object.assign(window.__prefs,JSON.parse(json))},tourCompleted(){return window.__tourDone},completeTour(){window.__tourDone=true},connectionCue(state){window.__calls.push('cue-'+state)},clickHaptic(){window.__calls.push('touch')},hasMotion(){return true},getMotionMode(){return 'off'},setMotion(mode){window.__calls.push(mode)},recenter(){window.__calls.push('recenter')},fullscreen(){},home(){}}")
                await c.command('Emulation.setDeviceMetricsOverride',width=932,height=430,deviceScaleFactor=1,mobile=True)
                await c.command('Emulation.setTouchEmulationEnabled',enabled=True,maxTouchPoints=5)
                await c.command('Page.navigate',url='http://localhost:8882/play?token='+host.token)
                await c.wait("document.querySelector('#walkthrough')&&!document.querySelector('#walkthrough').hidden")
                assert host.owner is None and host.state==neutral()
                assert await c.evaluate("!document.body.classList.contains('motion-off')"),'Motion should be on by default'
                await asyncio.sleep(.7);await c.screenshot(output/'mobile-guide-pair.png')
                await c.evaluate("document.querySelector('#tourNext').click();document.querySelector('#tourSizeRange').value=95;document.querySelector('#tourSizeRange').dispatchEvent(new Event('input'))")
                assert await c.evaluate("document.querySelector('#tourFeedback').textContent.includes('95')")
                await c.evaluate("document.querySelector('#tourNext').click()")
                await asyncio.sleep(.7)
                points=await c.evaluate("(()=>{let r=document.querySelector('#tourTouchSurface').getBoundingClientRect();return [{id:1,x:r.x+r.width*.25,y:r.y+r.height*.4},{id:2,x:r.x+r.width*.75,y:r.y+r.height*.6}]})()")
                await c.command('Input.dispatchTouchEvent',type='touchStart',touchPoints=points)
                assert await c.evaluate("document.querySelectorAll('#tourTouchSurface i').length===2")
                assert host.owner is None and host.state==neutral(),'Tutorial sent game input'
                await c.screenshot(output/'mobile-guide-touch.png')
                await c.command('Input.dispatchTouchEvent',type='touchEnd',touchPoints=[])
                await c.evaluate("document.querySelector('#tourNext').click();document.querySelector('#tourGyroRange').value=20;document.querySelector('#tourGyroRange').dispatchEvent(new Event('input'))")
                assert await c.evaluate("document.querySelector('#tourFeedback').textContent.includes('20')")
                await c.evaluate("document.querySelector('#tourNext').click()")
                assert await c.evaluate("window.__tourDone&&document.querySelector('#walkthrough').hidden")
                await c.evaluate("document.querySelector('#playerSettingsButton').click();document.querySelector('#appearanceChoice').value='oled';document.querySelector('#appearanceChoice').dispatchEvent(new Event('change'))")
                assert await c.evaluate("getComputedStyle(document.querySelector('#playerSurface')).backgroundColor==='rgb(0, 0, 0)'")
                assert await c.evaluate("window.__prefs.appearance==='oled'")
                await asyncio.sleep(.7);await c.screenshot(output/'mobile-oled.png')
                await c.evaluate("document.querySelector('#appearanceChoice').value='glass';document.querySelector('#appearanceChoice').dispatchEvent(new Event('change'))")
                assert await c.evaluate("getComputedStyle(document.querySelector('#playerSettingsMenu')).backdropFilter.includes('blur')")
                await c.screenshot(output/'mobile-glass.png')
                await c.evaluate("document.querySelector('#richMotion').checked=false;document.querySelector('#richMotion').dispatchEvent(new Event('change'))")
                await asyncio.sleep(.1)
                assert await c.evaluate("document.body.classList.contains('motion-off')&&document.getAnimations().filter(a=>a.playState==='running').length===0"),'Animations remain active after disabling motion'
                assert await c.evaluate("window.__prefs.animations===false")
                await c.evaluate("document.querySelector('#playerSettingsMenu').hidden=true;document.querySelector('#startPlaying').click()")
                await c.wait("document.querySelector('#playerOverlay').hidden")
                assert host.owner is not None
                await host.owner.close(code=1001,message=b'Temporary network interruption')
                await c.wait("document.body.dataset.connection==='reconnecting'")
                assert host.owner is None and host.state==neutral()
                await c.screenshot(output/'mobile-reconnecting.png')
                await c.wait("document.body.dataset.connection==='ready'&&document.querySelector('#playerOverlay').hidden")
                assert host.owner is not None,'Interrupted play did not reconnect'
                assert await c.evaluate("window.__calls.includes('cue-reconnecting')&&window.__calls.filter(c=>c==='cue-ready').length>=2")
                await c.evaluate("window.dispatchEvent(new Event('pocketpad-pause'))")
                await asyncio.sleep(.15)
                assert host.owner is None and host.state==neutral()
                await c.evaluate("document.querySelector('#experienceHaptics').checked=false;document.querySelector('#experienceHaptics').dispatchEvent(new Event('change'))")
                count=await c.evaluate("window.__calls.length")
                await c.evaluate("import('/assets/experience.js').then(m=>{m.connectionState('reconnecting');m.connectionState('ready')})")
                assert await c.evaluate("window.__calls.length")==count,'Disabled haptics still triggered a native cue'
                assert not c.errors,c.errors
                await c.ws.close()
                print('PASS: first-use guide, practice resizing/two-touch/gyro isolation, three themes, persisted native settings, default motion, complete motion disable, haptic preferences, reconnect/resume and neutral input release.')
        finally:
            process.terminate();await asyncio.to_thread(process.wait,10);await runner.cleanup()

if __name__=='__main__':asyncio.run(main())
