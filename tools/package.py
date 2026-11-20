"""Build a portable archive without including personal saved layouts."""
from pathlib import Path
import zipfile

root=Path(__file__).resolve().parents[1]
files=[root/name for name in ['PocketPad.exe','PocketPad.exe.config','Start PocketPad.cmd','Install Driver.cmd',
    'Allow Private Network.cmd','host.py','desktop_input.py','README.md','AUTHORS.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','requirements.txt','requirements-lock.txt']]
files.extend(p for p in (root/'desktop').rglob('*') if p.is_file() and 'build' not in p.relative_to(root/'desktop').parts and '__pycache__' not in p.parts)
if (root/'PocketPad-Android.apk').exists():
    files.append(root/'PocketPad-Android.apk')
    files.extend(root/'android'/name for name in ['README.md','build.py','bootstrap.py','check.py','.gitignore'])
    files.extend(p for p in (root/'android/app/src/main').rglob('*') if p.is_file() and 'controller' not in p.relative_to(root/'android/app/src/main').parts)
for directory in ['runtime','web','docs']:
    files.extend(p for p in (root/directory).rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc')
files.extend(root/'tools'/name for name in ['Launcher.cs','PocketPad.ico','ViGEmBus_1.22.0_x64_x86_arm64.exe',
    'build_presets.py','check_host.py','check_browser.py','check_remote.py','check_experience.py','check_device.py','check_ds4.py','package.py'])
archive=root/'PocketPad-Portable.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as bundle:
    for path in files:
        bundle.write(path,'PocketPad/'+path.relative_to(root).as_posix())
with zipfile.ZipFile(archive) as bundle:
    assert bundle.testzip() is None
    for name in ['desktop_input.py','web/remote.js','web/remote.css','PocketPad-Android.apk']:
        assert bundle.read('PocketPad/'+name)==(root/name).read_bytes(),f'Missing or stale remote input component: {name}'
print(f'Packaged and verified {len(files)} files: {archive.name} ({archive.stat().st_size/1024/1024:.1f} MB)')
