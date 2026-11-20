"""Verify the APK contains the current shared UI, native classes and notices."""
import hashlib
from pathlib import Path
import zipfile

root=Path(__file__).resolve().parents[1]
with zipfile.ZipFile(root/'PocketPad-Android.apk') as apk:
    assert apk.testzip() is None
    for file in ['index.html','app.js','style.css','experience.js','experience.css','remote.js','remote.css','presets.json']:
        assert apk.read('assets/controller/'+file)==(root/'web'/file).read_bytes(),f'Stale bundled UI: {file}'
    assert apk.read('classes.dex').startswith(b'dex\n')
    assert 'resources.arsc' in apk.namelist()
    assert 'AndroidManifest.xml' in apk.namelist()
    assert 'assets/licenses/ZXing-LICENSE.txt' in apk.namelist()
    assert 'assets/licenses/ZXing-NOTICE.txt' in apk.namelist()
print('PASS: APK integrity, current controller UI, DEX bytecode, Android resources and QR library notices.')
print('SHA-256:',hashlib.sha256((root/'PocketPad-Android.apk').read_bytes()).hexdigest())
