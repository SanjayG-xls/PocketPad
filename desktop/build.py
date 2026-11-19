"""Compile the native WPF desktop with the .NET Framework tools on Windows."""
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parent
FRAMEWORK=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319')
CSC=FRAMEWORK/'csc.exe'
BUILD=ROOT/'build'
BUILD.mkdir(exist_ok=True)
refs=[FRAMEWORK/'WPF/PresentationFramework.dll',FRAMEWORK/'WPF/PresentationCore.dll',FRAMEWORK/'WPF/WindowsBase.dll',
      FRAMEWORK/'System.Xaml.dll',FRAMEWORK/'System.Net.Http.dll',FRAMEWORK/'System.Web.Extensions.dll',
      FRAMEWORK/'System.IO.Compression.dll',FRAMEWORK/'System.IO.Compression.FileSystem.dll',FRAMEWORK/'System.Windows.Forms.dll']
def compile(output,files,extra=()):
    command=[str(CSC),'/nologo','/target:winexe','/platform:x64','/optimize+','/win32manifest:'+str(ROOT/'app.manifest'),'/out:'+str(output)]
    command+=['/reference:'+str(p) for p in refs]
    command+=list(extra)+[str(ROOT/file) for file in files]
    subprocess.run(command,check=True)
compile(BUILD/'AssetMaker.exe',['Branding.cs','AssetMaker.cs'])
subprocess.run([str(BUILD/'AssetMaker.exe'),str(ROOT/'assets')],check=True)
compile(PROJECT/'PocketPad.exe',['Models.cs','Engine.cs','Branding.cs','ControllerView.cs','Main.cs','Experience.cs'],
        ['/win32icon:'+str(ROOT/'assets/PocketPad.ico')])
(PROJECT/'PocketPad.exe.config').write_text('<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup><runtime><AppContextSwitchOverrides value="Switch.System.Windows.DoNotScaleForDpiChanges=false"/></runtime></configuration>',encoding='utf-8')
print('Built native desktop:',PROJECT/'PocketPad.exe')
