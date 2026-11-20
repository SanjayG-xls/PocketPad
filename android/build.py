"""Build a signed, sideloadable APK using the official Android SDK CLI tools."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parent
TOOLS=ROOT/'.tools'
BUILD=ROOT/'build'
SRC=ROOT/'app/src/main'
parser=argparse.ArgumentParser()
parser.add_argument('--java-home',default=os.environ.get('JAVA_HOME',str(Path(os.environ.get('LOCALAPPDATA',''))/'Programs/STM32CubeMX/jre')))
args=parser.parse_args()
JAVA=Path(args.java_home)/'bin'
def run(*command):
    result=subprocess.run([str(x) for x in command],cwd=ROOT)
    if result.returncode:raise SystemExit(result.returncode)

if not TOOLS.joinpath('build-tools').exists():
    raise SystemExit('Run runtime\\python.exe android\\bootstrap.py first.')
aapt=next((TOOLS/'build-tools').rglob('aapt2.exe'))
tools=aapt.parent
android=next((TOOLS/'platform').rglob('android.jar'))
zxing=TOOLS/'zxing-core-3.5.3.jar'
for directory in ['compiled','gen','classes','dex']:
    (BUILD/directory).mkdir(parents=True,exist_ok=True)
assets=SRC/'assets/controller'
assets.mkdir(parents=True,exist_ok=True)
for name in ['index.html','app.js','style.css','experience.js','experience.css','remote.js','remote.css','presets.json','pocketpad-mark.svg']:
    shutil.copyfile(PROJECT/'web'/name,assets/name)
run(aapt,'compile','--dir',SRC/'res','-o',BUILD/'resources.zip')
run(aapt,'link','-o',BUILD/'unsigned.apk','-I',android,'--manifest',SRC/'AndroidManifest.xml',
    '--java',BUILD/'gen','--min-sdk-version','26','--target-sdk-version','35','-A',SRC/'assets',BUILD/'resources.zip')
sources=list((SRC/'java').rglob('*.java'))+list((BUILD/'gen').rglob('*.java'))
run(JAVA/'javac.exe','-encoding','UTF-8','-source','8','-target','8','-Xlint:-options',
    '-classpath',str(android)+os.pathsep+str(zxing),'-d',BUILD/'classes',*sources)
with zipfile.ZipFile(BUILD/'classes.jar','w') as jar:
    for file in (BUILD/'classes').rglob('*.class'):
        jar.write(file,file.relative_to(BUILD/'classes').as_posix())
run(JAVA/'java.exe','-cp',tools/'lib/d8.jar','com.android.tools.r8.D8','--release','--min-api','26','--lib',android,
    '--output',BUILD/'dex',BUILD/'classes.jar',zxing)
with zipfile.ZipFile(BUILD/'unsigned.apk') as resources, zipfile.ZipFile(BUILD/'packaged.apk','w') as apk:
    for entry in resources.infolist():
        apk.writestr(entry,resources.read(entry.filename))
    apk.write(BUILD/'dex/classes.dex','classes.dex',compress_type=zipfile.ZIP_DEFLATED)
run(tools/'zipalign.exe','-f','-p','4',BUILD/'packaged.apk',BUILD/'aligned.apk')
keystore=TOOLS/'pocketpad-local.jks'
if not keystore.exists():
    # This is a local personal-use signing identity, not a store/distribution key.
    run(JAVA/'keytool.exe','-genkeypair','-keystore',keystore,'-storepass','android','-keypass','android',
        '-alias','pocketpad','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=PocketPad Local, O=Personal use, C=IN')
out=PROJECT/'PocketPad-Android.apk'
run(JAVA/'java.exe','-jar',tools/'lib/apksigner.jar','sign','--ks',keystore,'--ks-key-alias','pocketpad',
    '--ks-pass','pass:android','--key-pass','pass:android','--out',out,BUILD/'aligned.apk')
run(JAVA/'java.exe','-jar',tools/'lib/apksigner.jar','verify','--verbose',out)
run(aapt,'dump','badging',out)
print(f'Built and verified: {out.name} ({out.stat().st_size/1024/1024:.2f} MB)')
