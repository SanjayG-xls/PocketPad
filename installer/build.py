"""Build a self-contained native Windows installer and matching uninstaller."""
from pathlib import Path
import subprocess
import zipfile

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parent
DESKTOP=PROJECT/'desktop'
BUILD=ROOT/'build'
BUILD.mkdir(parents=True,exist_ok=True)
FRAMEWORK=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319')
refs=[FRAMEWORK/'WPF/PresentationFramework.dll',FRAMEWORK/'WPF/PresentationCore.dll',FRAMEWORK/'WPF/WindowsBase.dll',FRAMEWORK/'System.Xaml.dll',
      FRAMEWORK/'System.Web.Extensions.dll',FRAMEWORK/'System.IO.Compression.dll',FRAMEWORK/'System.IO.Compression.FileSystem.dll',FRAMEWORK/'System.Windows.Forms.dll']
resources=['/resource:'+str(DESKTOP/'Shell.xaml')+',PocketPad.Theme',
           '/resource:'+str(DESKTOP/'assets/setup-art.png')+',PocketPad.SetupArt']
def compile(target,uninstall=False):
    cmd=[str(FRAMEWORK/'csc.exe'),'/nologo','/target:winexe','/platform:x64','/optimize+','/win32manifest:'+str(DESKTOP/'app.manifest'),'/out:'+str(target),'/win32icon:'+str(DESKTOP/'assets/PocketPad.ico')]
    cmd+=['/reference:'+str(p) for p in refs]+resources
    if uninstall:cmd+=['/define:UNINSTALL']
    else:cmd+=['/resource:'+str(BUILD/'payload.zip')+',PocketPad.Payload']
    cmd+=[str(DESKTOP/'Branding.cs'),str(ROOT/'Setup.cs')]
    subprocess.run(cmd,check=True)
compile(BUILD/'Uninstall.exe',True)
files=[PROJECT/name for name in ['PocketPad.exe','PocketPad.exe.config','host.py','desktop_input.py','PocketPad-Android.apk','README.md','AUTHORS.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','requirements.txt','requirements-lock.txt']]
files+=[DESKTOP/'Shell.xaml']
files.extend(p for p in (DESKTOP/'assets').iterdir() if p.suffix.lower() in ('.png','.ico'))
for folder in ['runtime','web','docs']:
    files.extend(p for p in (PROJECT/folder).rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc')
files.extend([PROJECT/'tools/ViGEmBus_1.22.0_x64_x86_arm64.exe',PROJECT/'android/README.md'])
if (DESKTOP/'README.md').exists():files.append(DESKTOP/'README.md')
with zipfile.ZipFile(BUILD/'payload.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as payload:
    for path in files:payload.write(path,path.relative_to(PROJECT).as_posix())
    payload.write(BUILD/'Uninstall.exe','Uninstall.exe')
    # Distributable installers always start with factory layouts. Personal slots
    # remain in the owner's Windows profile and never enter a public release.
compile(PROJECT/'PocketPad-Setup.exe')
print('Built installer:',PROJECT/'PocketPad-Setup.exe')
print('Installer size:',round((PROJECT/'PocketPad-Setup.exe').stat().st_size/1024/1024,1),'MB')
