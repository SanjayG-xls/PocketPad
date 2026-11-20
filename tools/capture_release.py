"""Render release graphics around real UI captures from an isolated phone preview."""
import asyncio
import base64
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT));sys.path.insert(0,str(ROOT/'tools'))
from aiohttp import ClientSession,web
from host import Host
from check_browser import CDP

OUT=ROOT/'docs/images'
WORK=ROOT/'test-results/release'


def data(path,mime='image/png'):
    return 'data:'+mime+';base64,'+base64.b64encode(path.read_bytes()).decode()


def frame(path,extra=''):
    return '<div class="phone '+extra+'"><div class="screen"><img src="'+data(path)+'"></div></div>'


STYLE='''
*{box-sizing:border-box}body{margin:0;background:#0c0d12;color:#f5f4fa;font-family:"Segoe UI",Arial,sans-serif}main{position:relative;overflow:hidden;background:radial-gradient(ellipse at 83% 40%,#29273e 0,transparent 50%),radial-gradient(ellipse at 0 110%,#1a252b,transparent 55%),#0c0d12}h1,h2,p{margin:0}.brand{display:flex;align-items:center;gap:14px;font-size:23px;font-weight:600;letter-spacing:-.6px}.brand img{width:40px;height:40px}.eyebrow{display:flex;align-items:center;gap:12px;color:#a8a2c5;font-size:13px;font-weight:600;letter-spacing:2px;text-transform:uppercase}.dot{width:6px;height:6px;border-radius:50%;background:#b2baff}.phone{position:absolute;padding:7px;background:linear-gradient(135deg,#585860,#1a1a20 22%,#121216 70%,#51515b);border:1px solid #62626c;border-radius:35px;box-shadow:0 35px 75px #0008;overflow:hidden}.screen{overflow:hidden;border-radius:28px;background:#000}.screen img{display:block;width:100%;height:100%}.pill{border:1px solid #42424e;border-radius:24px;padding:10px 17px;font-size:13px;background:#171820b0;color:#cfcedd}.muted{color:#a3a2b3}.credit{font-size:13px;color:#888695}.caption{font-size:11px;color:#75747f}.tag{display:inline-block;background:#aaa1f41a;border:1px solid #a299ed50;border-radius:6px;padding:7px 11px;font-size:11px;letter-spacing:1.2px;color:#cbc3ff}.rule{height:1px;background:linear-gradient(90deg,#494554,transparent)}
'''


def hero():
    return f'''<!doctype html><meta charset="utf-8"><style>{STYLE}
main{{width:1600px;height:900px;padding:62px 80px}}.top{{display:flex;align-items:center;justify-content:space-between}}.copy{{position:relative;z-index:2;width:680px;margin-top:92px}}h1{{font-size:91px;font-weight:600;line-height:1.03;letter-spacing:-5px;margin:22px 0 28px}}h1 span{{color:#b8aff6}}.description{{font-size:23px;line-height:1.55;color:#a6a5b8;max-width:570px}}.pills{{display:flex;gap:10px;margin-top:38px}}.front{{width:322px;right:255px;top:170px;transform:rotate(-5deg);z-index:2}}.back{{width:298px;right:-3px;top:244px;transform:rotate(8deg);z-index:1}}.footer{{position:absolute;bottom:44px;left:80px;right:80px;display:flex;align-items:center;justify-content:space-between}}.feature-name{{font-size:12px;letter-spacing:1px;color:#a39db9}}.halo{{position:absolute;width:720px;height:720px;right:-70px;top:100px;border:1px solid #8c83af20;border-radius:50%}}.halo:after{{content:"";position:absolute;inset:55px;border:1px solid #8c83af18;border-radius:50%}}
</style><main><div class="halo"></div><div class="top"><div class="brand"><img src="{data(ROOT/'desktop/assets/icon-64.png')}">PocketPad</div><span class="tag">DESKTOP 2.2 &nbsp; / &nbsp; ANDROID 1.3</span></div><div class="copy"><div class="eyebrow"><i class="dot"></i>Virtual touchpad & keyboard</div><h1>Your phone.<br>Your PC.<br><span>Connected.</span></h1><p class="description">A black touchpad. Your familiar keyboard.<br>Control your PC from wherever you sit.</p><div class="pills"><span class="pill">Scroll & pinch</span><span class="pill">Type & edit</span><span class="pill">Switch apps</span></div></div>{frame(WORK/'keyboard.png','front')}{frame(WORK/'gestures.png','back')}<div class="footer"><span class="credit">Made by Sanjay</span><span class="feature-name">LOCAL CONNECTION &nbsp; · &nbsp; NO GAMEPAD DRIVER NEEDED</span></div></main>'''


def keyboard():
    return f'''<!doctype html><meta charset="utf-8"><style>{STYLE}
main{{width:1200px;height:800px;padding:54px 64px}}.copy{{margin-top:68px;width:630px}}h2{{font-size:61px;letter-spacing:-3px;line-height:1.06;margin:20px 0 25px}}.lead{{font-size:20px;line-height:1.65;color:#a6a4b8;max-width:540px}}.features{{margin-top:33px;display:grid;grid-template-columns:1fr;gap:20px}}.feature{{display:flex;align-items:center;gap:15px;font-size:16px;color:#dad7e6}}.feature i{{font-style:normal;color:#c1b8fa;border:1px solid #77708d60;border-radius:10px;padding:8px 11px;background:#20202a;font-size:12px}}.phone{{width:320px;right:70px;top:60px;transform:rotate(3deg)}}.note{{position:absolute;left:64px;bottom:40px;width:550px;font-size:12px;color:#74717f;line-height:1.7}}
</style><main><div class="eyebrow"><i class="dot"></i>Keyboard, on demand</div><div class="copy"><h2>Type on your phone.<br><span style="color:#b8aff6">Work on your PC.</span></h2><p class="lead">One small button brings up your phone keyboard. Text goes to the PC app you’re using.</p><div class="features"><div class="feature"><i>Aa</i>Unicode, emoji & composed text</div><div class="feature"><i>Ctrl + C</i>Shortcuts with one-use modifiers</div><div class="feature"><i>F1 — F12</i>Editing, arrows & function keys</div><div class="feature"><i>↔</i>Portrait & landscape layouts</div></div></div>{frame(WORK/'keyboard.png')}<p class="note">Actual PocketPad interface captured in an isolated preview.<br>The phone’s system keyboard appears below the controls on device.</p></main>'''


def gestures():
    return f'''<!doctype html><meta charset="utf-8"><style>{STYLE}
main{{width:1200px;height:800px;padding:54px 64px}}h2{{font-size:57px;line-height:1.1;letter-spacing:-2.7px;margin:22px 0 34px}}.cards{{display:grid;grid-template-columns:1fr 1fr;gap:16px;width:650px}}.card{{padding:24px 22px;background:#191a2370;border:1px solid #383443;border-radius:15px;min-height:170px}}.number{{color:#bdb3f3;font-size:31px;font-weight:300;letter-spacing:-1px}}.card strong{{display:block;font-size:18px;letter-spacing:-.4px;margin:12px 0 7px}}.card p{{font-size:14px;color:#9995ab;line-height:1.65}}.phone{{width:290px;right:55px;top:67px;transform:rotate(3deg)}}.bottom{{position:absolute;bottom:42px;left:64px;font-size:12px;color:#757180;max-width:650px;line-height:1.6}}
</style><main><div class="eyebrow"><i class="dot"></i>Familiar gestures. More room.</div><h2>A touchpad that<br><span style="color:#b8aff6">knows the moves.</span></h2><div class="cards"><div class="card"><span class="number">01</span><strong>Point. Tap. Drag.</strong><p>Move the pointer, click,<br>double-click and select.</p></div><div class="card"><span class="number">02</span><strong>Scroll. Pinch. Zoom.</strong><p>Two fingers for scrolling,<br>pinch zoom and right-click.</p></div><div class="card"><span class="number">03</span><strong>Switch your focus.</strong><p>Three-finger swipes for<br>apps, Task View and desktop.</p></div><div class="card"><span class="number">04</span><strong>Find more space.</strong><p>Four-finger swipes switch<br>your Windows virtual desktops.</p></div></div>{frame(WORK/'gestures.png')}<p class="bottom">Pinch zoom uses Ctrl + wheel in supported apps. Multi-finger gestures depend on phone support.<br>Actual phone interface shown in an isolated preview; tap ? for the full guide.</p></main>'''


async def main():
    OUT.mkdir(exist_ok=True);WORK.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='pocketpad-release-browser-') as profile,tempfile.TemporaryDirectory() as state:
        host=Host(8884,mock=True,data_dir=state)
        runner=web.AppRunner(host.app());await runner.setup();await web.TCPSite(runner,'127.0.0.1',8884).start()
        process=subprocess.Popen([r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe','--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=19367',f'--user-data-dir={profile}','about:blank'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            async with ClientSession() as session:
                for _ in range(100):
                    try:
                        async with session.put('http://127.0.0.1:19367/json/new?about:blank') as response:target=await response.json()
                        break
                    except OSError:await asyncio.sleep(.1)
                c=CDP(await session.ws_connect(target['webSocketDebuggerUrl']))
                await c.command('Page.enable');await c.command('Runtime.enable')
                await c.command('Page.addScriptToEvaluateOnNewDocument',source="localStorage.setItem('pocketpad-tour-complete','1');localStorage.setItem('pocketpad-experience',JSON.stringify({appearance:'matte',animations:false,haptics:false}));window.PocketPadNative={getPreferences(){return '{}'},tourCompleted(){return true},hasMotion(){return false},getMotionMode(){return 'off'},fullscreen(){},showKeyboard(){},hideKeyboard(){},setInputMode(){},connectionCue(){}}")
                await c.command('Emulation.setDeviceMetricsOverride',width=430,height=820,deviceScaleFactor=1,mobile=True)
                await c.command('Page.navigate',url='http://127.0.0.1:8884/play?demo=1&input=desktop')
                await c.wait("document.querySelector('#startPlaying')&&document.querySelectorAll('#playerSurface .control').length>10")
                await c.evaluate("document.querySelector('#startPlaying').click()")
                await c.wait("document.querySelector('#playerOverlay').hidden")
                await c.screenshot(WORK/'touchpad.png')
                await c.evaluate("document.querySelector('#remoteKeyboardButton').click()")
                await c.command('Input.insertText',text='Hello from PocketPad')
                await c.screenshot(WORK/'keyboard.png')
                await c.evaluate("document.querySelector('#remoteKeyboardClose').click();document.querySelector('#remoteHelpButton').click()")
                await c.screenshot(WORK/'gestures.png')
                for filename,render,width,height in [('virtual-touchpad-hero.png',hero,1600,900),('virtual-keyboard.png',keyboard,1200,800),('virtual-touchpad-gestures.png',gestures,1200,800)]:
                    document=WORK/(filename+'.html');document.write_text(render(),encoding='utf-8')
                    await c.command('Emulation.setDeviceMetricsOverride',width=width,height=height,deviceScaleFactor=1,mobile=False)
                    await c.command('Page.navigate',url=document.as_uri())
                    await c.wait("document.querySelector('main')&&[...document.images].every(i=>i.complete&&i.naturalWidth>0)")
                    await c.evaluate('document.fonts.ready')
                    await c.screenshot(OUT/filename)
                    print('Rendered',filename)
                assert not c.errors,c.errors
                await c.ws.close()
        finally:
            process.terminate();await asyncio.to_thread(process.wait,10);await runner.cleanup()


if __name__=='__main__':asyncio.run(main())
