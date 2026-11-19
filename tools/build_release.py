"""Rebuild both apps and installer, stopping immediately if any build fails."""
from pathlib import Path
import hashlib
import subprocess
import sys

root=Path(__file__).resolve().parents[1]
for script in ['desktop/build.py','android/build.py','android/check.py','installer/build.py','installer/check.py','tools/package.py']:
    print('Running',script,flush=True)
    subprocess.run([sys.executable,str(root/script)],cwd=root,check=True)
assets=['PocketPad-Setup.exe','PocketPad-Android.apk','PocketPad-Portable.zip']
(root/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256((root/name).read_bytes()).hexdigest()+'  '+name+'\n' for name in assets),encoding='ascii')
print('Release artifacts ready: '+', '.join(assets)+', SHA256SUMS.txt')
