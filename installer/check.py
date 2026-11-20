"""Install, launch, update and uninstall in an isolated workspace folder."""
import hashlib
import json
from pathlib import Path
import subprocess
import winreg

ROOT=Path(__file__).resolve().parents[1]
FIXTURE=ROOT/'installer/build/install-smoke'
TARGET=FIXTURE/'PocketPad'
DATA=FIXTURE/'test-user-data'
OUTPUT=FIXTURE/'qa'
SETUP=ROOT/'PocketPad-Setup.exe'
KEY=r'Software\Microsoft\Windows\CurrentVersion\Uninstall\PocketPadDesktopNativeTest'

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None
original=digest(ROOT/'data/presets.json')

def run(*args):
    result=subprocess.run([str(a) for a in args],creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
    if result.returncode:
        detail=Path(__import__('tempfile').gettempdir())/'PocketPad-setup-error.txt'
        raise RuntimeError(f'Command failed ({result.returncode}): {args[0]}\n'+(detail.read_text() if detail.exists() else ''))

FIXTURE.mkdir(parents=True,exist_ok=True)
if TARGET.exists():
    assert (TARGET/'PocketPad.install.json').exists(),'Refusing to remove an unowned test directory'
    run(SETUP,'--uninstall','--silent','--test-install','--target',TARGET)
run(SETUP,'--silent','--test-install','--target',TARGET)
assert json.loads((TARGET/'PocketPad.install.json').read_text())['appId']=='PocketPad.Desktop.Native'
assert (TARGET/'PocketPad.exe').stat().st_size>30000,'Expected native application executable'
assert (TARGET/'Uninstall.exe').exists()
for name in ['desktop_input.py','web/remote.js','web/remote.css']:
    assert (TARGET/name).read_bytes()==(ROOT/name).read_bytes(),f'Missing or stale desktop input component: {name}'
assert (FIXTURE/'test-shortcuts/PocketPad.lnk').exists()
assert (FIXTURE/'test-shortcuts/Uninstall PocketPad.lnk').exists()
with winreg.OpenKey(winreg.HKEY_CURRENT_USER,KEY) as key:
    assert Path(winreg.QueryValueEx(key,'InstallLocation')[0])==TARGET
    assert winreg.QueryValueEx(key,'DisplayVersion')[0]=='2.2'
run(TARGET/'PocketPad.exe','--smoke','--mock','--port','8896','--data-dir',DATA,'--output',OUTPUT)
assert (OUTPUT/'result.txt').exists(),(OUTPUT/'error.txt').read_text() if (OUTPUT/'error.txt').exists() else 'Native smoke result missing'
saved=digest(DATA/'presets.json')
preferences=digest(FIXTURE/'desktop.json')
assert saved is not None
run(SETUP,'--silent','--test-install','--target',TARGET)
assert digest(DATA/'presets.json')==saved,'An update replaced personal layouts'
assert digest(FIXTURE/'desktop.json')==preferences,'An update replaced appearance or walkthrough settings'
run(SETUP,'--uninstall','--silent','--test-install','--target',TARGET)
assert not TARGET.exists(),'Application files remain after uninstall'
assert digest(DATA/'presets.json')==saved,'Uninstall did not preserve personal layouts'
assert digest(FIXTURE/'desktop.json')==preferences,'Uninstall did not preserve appearance settings'
try:
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,KEY):raise AssertionError('Uninstall registration remains')
except FileNotFoundError:pass
assert digest(ROOT/'data/presets.json')==original,'Original workspace layouts were changed'
print('PASS: isolated install, Windows shortcuts/registration, installed native launch, editor/pairing, update preserving layouts, uninstall and data retention.')
