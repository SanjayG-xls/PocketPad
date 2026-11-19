# Building PocketPad

PocketPad has three pieces: a native Windows C#/WPF app, a Python local controller engine, and an Android Java companion. The HTML/CSS/JavaScript in `web/` is shared with the phone app.

## Prerequisites

- Windows x64 with .NET Framework 4.8, including the Framework C# compiler.
- Python 3.12 for the local engine and build helpers.
- A JDK with `java`, `javac` and `keytool`; set `JAVA_HOME` or pass `--java-home` to `android/build.py`.
- Android SDK build-tools 35.0.0 and Android platform 35. The bootstrap script fetches the official packages and ZXing 3.5.3.
- ViGEmBus for actual virtual controller output. Mock checks can run without creating a virtual device.

The Git repository contains source. Runtime binaries, SDK downloads and the signed driver installer are supplied separately in the [portable release](https://github.com/SanjayG-xls/PocketPad/releases/latest).

## Prepare a checkout

1. Clone the repository and download the portable release.
2. From the extracted `PocketPad` folder, copy `runtime/` and `tools/ViGEmBus_1.22.0_x64_x86_arm64.exe` into the matching locations in the checkout. These files stay ignored by Git.
3. Set `JAVA_HOME` to your JDK folder, then run the commands below in PowerShell from the repository root.

```powershell
git clone https://github.com/SanjayG-xls/PocketPad.git
cd PocketPad
# Supply the runtime and driver from the portable release as described above.
$env:JAVA_HOME = 'C:\path\to\your\jdk'
.\runtime\python.exe android\bootstrap.py
.\runtime\python.exe tools\build_release.py
```

The complete build produces `PocketPad.exe`, `PocketPad-Android.apk`, `PocketPad-Setup.exe` and `PocketPad-Portable.zip`. It stops if a build or required packaging check fails. Windows binaries target x64 and .NET Framework 4.8.

The APK builder creates a local signing identity under `android/.tools/` if one is missing. Keep that keystore private and preserve it to update your own APK installations; a different signing key cannot update an existing installation. Repository clones do not include Sanjay's signing key.

## Individual builds and checks

```powershell
.\runtime\python.exe desktop\build.py
.\runtime\python.exe android\build.py
.\runtime\python.exe android\check.py
.\runtime\python.exe installer\build.py
.\runtime\python.exe installer\check.py
.\runtime\python.exe tools\package.py
```

The installer check uses an isolated workspace target and test registration. It checks installation, native launch, an update that preserves user data, and uninstall/data retention.

```powershell
.\runtime\python.exe tools\check_host.py
.\runtime\python.exe tools\check_browser.py
.\runtime\python.exe tools\check_experience.py
```

Browser checks require installed Microsoft Edge. `check_device.py` and `check_ds4.py` need the installed virtual controller driver and create real test devices. Physical sensor feel and in-game behavior remain manual checks.

## Source layout

- `desktop/`: WPF shell, native editor, controller renderer, themes and asset generator.
- `android/`: native Java shell, sensor bridge, camera scanner, resources and build scripts.
- `web/`: controller UI, first-use experience and factory presets.
- `host.py`: local HTTP/WebSocket engine, pairing and virtual gamepad bridge.
- `installer/`: native setup/uninstaller and embedded-payload builder.
- `tools/`: release orchestration, portable packaging and input checks.

Public builds do not embed `data/presets.json`. Installed layouts and preferences live in the user's Windows profile and are preserved across updates.
