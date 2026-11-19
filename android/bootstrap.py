"""Fetch verified Android SDK build packages for the lightweight local APK build."""
from concurrent.futures import ThreadPoolExecutor
import hashlib
from pathlib import Path
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT=Path(__file__).resolve().parent
TOOLS=ROOT/'.tools'
TOOLS.mkdir(exist_ok=True)
repo=TOOLS/'repository.xml'
if not repo.exists():
    urllib.request.urlretrieve('https://dl.google.com/android/repository/repository2-1.xml',repo)
tree=ET.parse(repo)

def fetch_package(name,folder):
    target=TOOLS/folder
    if target.exists():
        print('Already available:',name,flush=True)
        return
    package=next(p for p in tree.getroot().iter('remotePackage') if p.get('path')==name)
    archives=package.find('archives')
    archive=next(a for a in archives.findall('archive') if a.findtext('host-os') in (None,'windows'))
    complete=archive.find('complete')
    url='https://dl.google.com/android/repository/'+complete.findtext('url')
    dest=TOOLS/(folder+'.zip')
    print('Downloading',name,round(int(complete.findtext('size'))/1024/1024,1),'MB',flush=True)
    urllib.request.urlretrieve(url,dest)
    checksum=complete.find('checksum')
    actual=hashlib.new(checksum.get('type','sha1'),dest.read_bytes()).hexdigest()
    if actual!=checksum.text.strip():raise RuntimeError('SDK checksum mismatch')
    with zipfile.ZipFile(dest) as bundle:
        bundle.extractall(target)
    print('Verified and extracted:',name,flush=True)

with ThreadPoolExecutor(max_workers=2) as pool:
    jobs=[pool.submit(fetch_package,*spec) for spec in [('build-tools;35.0.0','build-tools'),('platforms;android-35','platform')]]
    for job in jobs:job.result()

jar=TOOLS/'zxing-core-3.5.3.jar'
if not jar.exists():
    url='https://repo.maven.apache.org/maven2/com/google/zxing/core/3.5.3/core-3.5.3.jar'
    urllib.request.urlretrieve(url,jar)
    expected=urllib.request.urlopen(url+'.sha1').read().decode().strip().split()[0]
    assert hashlib.sha1(jar.read_bytes()).hexdigest()==expected,'QR library checksum mismatch'
print('Android build prerequisites ready.',flush=True)
