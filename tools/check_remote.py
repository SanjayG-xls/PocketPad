"""Phone touch/IME to Windows packet regression checks, without desktop injection."""
import asyncio
import json
from pathlib import Path
import subprocess
import sys
import tempfile

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
sys.path.insert(0,str(Path(__file__).resolve().parent))
from aiohttp import ClientSession,web
from host import Host,neutral
from check_browser import CDP

ROOT=Path(__file__).resolve().parents[1]


async def main():
    (ROOT/'test-results').mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='pocketpad-remote-browser-') as profile,tempfile.TemporaryDirectory() as data:
        host=Host(8878,mock=True,data_dir=data)
        commands=[];packets=[]
        host.desktop.emit=lambda events:packets.extend(events)
        original=host.desktop.apply
        def apply(command):
            original(command)
            commands.append(dict(command))
        host.desktop.apply=apply
        runner=web.AppRunner(host.app());await runner.setup()
        await web.TCPSite(runner,'127.0.0.1',8878).start()
        process=subprocess.Popen([r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
            '--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check',
            '--remote-debugging-port=19366',f'--user-data-dir={profile}','about:blank'],
            stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            async with ClientSession() as session:
                for _ in range(100):
                    try:
                        async with session.put('http://localhost:19366/json/new?about:blank') as response:
                            target=await response.json()
                        break
                    except OSError:await asyncio.sleep(.1)
                c=CDP(await session.ws_connect(target['webSocketDebuggerUrl']))
                await c.command('Page.enable');await c.command('Runtime.enable')
                await c.command('Page.addScriptToEvaluateOnNewDocument',source="localStorage.setItem('pocketpad-tour-complete','1');localStorage.setItem('pocketpad-experience',JSON.stringify({animations:false}));window.__native=[];window.PocketPadNative={getPreferences(){return '{}'},tourCompleted(){return true},hasMotion(){return false},getMotionMode(){return 'off'},setInputMode(mode){window.__native.push(mode)},showKeyboard(){window.__native.push('keyboard')},hideKeyboard(){},fullscreen(){},connectionCue(){},clickHaptic(){}}")
                await c.command('Emulation.setDeviceMetricsOverride',width=932,height=430,deviceScaleFactor=1,mobile=True)
                await c.command('Emulation.setTouchEmulationEnabled',enabled=True,maxTouchPoints=5)
                await c.command('Page.navigate',url='http://localhost:8878/play?token='+host.token+'&input=desktop')
                await c.wait("document.querySelectorAll('#playerSurface .control').length>10&&document.body.classList.contains('remote-mode')")
                await c.evaluate("document.querySelector('#startPlaying').click()")
                await c.wait("document.querySelector('#playerOverlay').hidden")
                for _ in range(40):
                    if host.status()['inputMode']=='desktop':break
                    await asyncio.sleep(.025)
                assert host.status()['inputMode']=='desktop'
                assert await c.evaluate("getComputedStyle(document.querySelector('#remotePad')).backgroundColor==='rgb(0, 0, 0)'")
                async def wait_for(predicate,message):
                    for _ in range(60):
                        if predicate():return
                        await asyncio.sleep(.025)
                    raise AssertionError(message+': '+repr(commands))
                async def touch(kind,points=()):
                    await c.command('Input.dispatchTouchEvent',type=kind,touchPoints=[dict(id=i+1,x=x,y=y) for i,(x,y) in enumerate(points)])
                    await asyncio.sleep(.05)
                def actions(name):return [p for p in commands if p['action']==name]
                def clear():commands.clear();packets.clear()
                clear()
                await touch('touchStart',[(420,210)])
                await touch('touchMove',[(460,230)])
                await touch('touchEnd')
                await wait_for(lambda:any(p['dx']>50 and p['dy']>20 for p in actions('move')),'Relative pointer failed')
                assert not actions('click'),'Pointer movement clicked'
                clear()
                await touch('touchStart',[(420,210)]);await touch('touchEnd')
                await touch('touchStart',[(420,210)]);await touch('touchEnd')
                await wait_for(lambda:len(actions('click'))==2,'Double tap did not send two clicks')
                clear()
                await touch('touchStart',[(420,210),(500,210)])
                await touch('touchEnd')
                await wait_for(lambda:any(p['button']=='right' for p in actions('click')),'Two-finger right-click failed')
                assert not actions('button'),'Adding fingers after a tap produced an accidental drag'
                clear()
                await touch('touchStart',[(420,210),(500,210)])
                await touch('touchMove',[(420,160),(500,160)])
                await touch('touchMove',[(390,160),(470,160)])
                await touch('touchEnd')
                await wait_for(lambda:any(p['dy']<0 for p in actions('scroll')) and any(p['dx']>0 for p in actions('scroll')),'Vertical/horizontal scrolling failed')
                assert not actions('click') and not actions('zoom'),'Scrolling was confused with tap/zoom'
                clear()
                await touch('touchStart',[(420,210),(500,210)])
                await touch('touchMove',[(390,210),(530,210)])
                await touch('touchMove',[(435,210),(485,210)])
                await touch('touchEnd')
                await wait_for(lambda:any(p['steps']>0 for p in actions('zoom')) and any(p['steps']<0 for p in actions('zoom')),'Pinch zoom directions failed')
                assert not actions('click') and not actions('scroll'),'Pinch caused scroll/click'
                for count,direction,name in [(3,(-90,0),'app_next'),(3,(90,0),'app_previous'),(3,(0,-90),'task_view'),(3,(0,90),'show_desktop'),(4,(-90,0),'desktop_next'),(4,(90,0),'desktop_previous')]:
                    clear();start=[(360+i*55,210) for i in range(count)]
                    await touch('touchStart',start)
                    await touch('touchMove',[(x+direction[0],y+direction[1]) for x,y in start])
                    await touch('touchEnd')
                    await wait_for(lambda:any(p['name']==name for p in actions('gesture')),name+' failed')
                    assert len(actions('gesture'))==1 and not actions('click') and not actions('scroll'),'Swipe emitted duplicate/unrelated input'
                for count,name in [(3,'search'),(4,'quick_settings')]:
                    clear();await touch('touchStart',[(360+i*55,210) for i in range(count)]);await touch('touchEnd')
                    await wait_for(lambda:any(p['name']==name for p in actions('gesture')),name+' tap failed')
                clear()
                await touch('touchStart',[(420,210)])
                await asyncio.sleep(.45)
                await wait_for(lambda:'left' in host.desktop.held,'Hold did not start drag')
                await touch('touchMove',[(480,240)]);await touch('touchEnd')
                assert not host.desktop.held and not actions('click'),'Drag release failed'
                # Opening the phone keyboard must keep the touchpad active.
                await c.evaluate("document.querySelector('#remoteKeyboardButton').click()")
                assert await c.evaluate("document.activeElement.id==='remoteText'&&window.__native.includes('keyboard')")
                clear()
                await c.command('Input.insertText',text='Hello नमस्ते 😀')
                await wait_for(lambda:any(p.get('text')=='Hello नमस्ते 😀' for p in commands),'Unicode phone typing failed')
                assert len([p for p in packets if p.type==1 and p.keyboard.flags==4])>10,'No Windows Unicode packets'
                await c.command('Input.dispatchKeyEvent',type='keyDown',key='Backspace',code='Backspace',windowsVirtualKeyCode=8)
                await c.command('Input.dispatchKeyEvent',type='keyUp',key='Backspace',code='Backspace',windowsVirtualKeyCode=8)
                await wait_for(lambda:bool(actions('key')),'Backspace failed')
                clear()
                await c.evaluate("document.querySelector('[data-key=Enter]').click();document.querySelector('[data-modifier=Ctrl]').click()")
                await c.command('Input.insertText',text='c')
                await wait_for(lambda:any(p.get('text')=='c' and p.get('modifiers')==['Ctrl'] for p in commands),'Ctrl+C shortcut failed')
                assert await c.evaluate("document.querySelector('[data-modifier=Ctrl]').getAttribute('aria-pressed')==='false'")
                # IME composition is sent only on commit, never duplicated.
                await c.evaluate("document.querySelector('[data-key=Enter]').click()")
                await c.evaluate("window.__imeEvents=[];for(let type of ['compositionstart','compositionupdate','compositionend','beforeinput','input'])document.querySelector('#remoteText').addEventListener(type,e=>window.__imeEvents.push({type,inputType:e.inputType,composing:e.isComposing,value:e.target.value,data:e.data}))")
                clear()
                await c.command('Input.imeSetComposition',text='に',selectionStart=1,selectionEnd=1)
                await asyncio.sleep(.06)
                assert not actions('text'),('Uncommitted IME text leaked',commands,await c.evaluate('window.__imeEvents'))
                await c.command('Input.insertText',text='日本')
                await wait_for(lambda:any(p.get('text')=='日本' for p in commands),'IME commit failed')
                assert len(actions('text'))==1,'IME text was duplicated'
                # Autocorrect replaces the recent text, rather than appending a duplicate.
                await c.evaluate("document.querySelector('[data-key=Enter]').click()")
                await c.command('Input.insertText',text='teh')
                clear()
                await c.evaluate("let field=document.querySelector('#remoteText');field.value='\u200bthe';field.dispatchEvent(new InputEvent('input',{inputType:'insertReplacementText'}))")
                await wait_for(lambda:bool(actions('text')),'Autocorrect failed')
                assert len(actions('key'))==2 and actions('text')[-1]['text']=='he','Autocorrect diff incorrect'
                await c.command('Emulation.setDeviceMetricsOverride',width=932,height=150,deviceScaleFactor=1,mobile=True)
                await c.wait("document.querySelector('#remotePanel').classList.contains('remote-compact')")
                assert await c.evaluate("document.querySelector('#remoteText').getBoundingClientRect().bottom<=150"),'Keyboard controls were clipped in a short viewport'
                await c.evaluate("document.querySelector('#remoteKeyboardClose').click()")
                # Portrait layout and help remain usable without a rotate prompt.
                await c.command('Emulation.setDeviceMetricsOverride',width=430,height=932,deviceScaleFactor=1,mobile=True)
                await c.wait("document.querySelector('#remotePanel').clientHeight>800")
                assert await c.evaluate("getComputedStyle(document.querySelector('.rotate-hint')).display==='none'")
                await c.screenshot(ROOT/'test-results/remote-touchpad.png')
                await c.evaluate("document.querySelector('#remoteHelpButton').click()")
                await c.screenshot(ROOT/'test-results/remote-gestures.png')
                await c.evaluate("document.querySelector('#remoteHelpClose').click();document.querySelector('#remoteBack').click()")
                await wait_for(lambda:host.status()['inputMode']=='gamepad','Return to gamepad failed')
                assert host.state==neutral()
                await c.evaluate("document.querySelector('#playerRemoteButton').click()")
                await wait_for(lambda:host.status()['inputMode']=='desktop','Reopening remote failed')
                # Pausing mid-drag must release, and resume must restore desktop mode.
                await touch('touchStart',[(200,300)]);await asyncio.sleep(.45)
                await wait_for(lambda:'left' in host.desktop.held,'Second drag failed')
                await c.evaluate("document.querySelector('#remotePause').click()")
                await wait_for(lambda:host.owner is None and not host.desktop.held,'Pause stuck a mouse button')
                await touch('touchEnd')
                await c.evaluate("document.querySelector('#startPlaying').click()")
                await wait_for(lambda:host.owner is not None and host.status()['inputMode']=='desktop','Remote resume failed')
                await c.evaluate("window.dispatchEvent(new Event('pocketpad-pause'))")
                await wait_for(lambda:host.owner is None,'Native background did not release remote')
                assert not c.errors,c.errors
                await c.ws.close()
                print('PASS: black touchpad, pointer, double tap, right click, drag/release, two-axis scroll, pinch zoom, 3/4-finger swipes/taps, Unicode/IME/autocorrect typing, shortcuts, portrait, gamepad isolation, pause/resume and native background.')
        finally:
            process.terminate();await asyncio.to_thread(process.wait,10)
            await runner.cleanup()


if __name__=='__main__':asyncio.run(main())
