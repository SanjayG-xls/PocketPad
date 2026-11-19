# PocketPad Android companion

The installable application is `../PocketPad-Android.apk`. Android 8.0 or newer is required. The application uses the same controller HTML, CSS, JavaScript and factory layouts as the PC host. No Play Store account or publication is required.

## Install and connect

1. Copy the APK to your phone and open it, or restart the PC host, choose **Connection → Install Android app**, scan that download QR with your phone camera, and download the APK.
2. Android may ask you to allow the browser/file manager you used to install applications. Allow that source for this local installation.
3. Open **PocketPad** on the phone. It shows a short animated controller mark, then the connection screen.
4. In the PC studio, select **Connection → Pair controller**. In the Android app, tap **Scan QR code** and grant camera access. Scan the PC pairing QR.
5. The app opens the existing dark PS5/Xbox controller interface in landscape. Tap **Start controller**. Keep PocketPad running on the PC while you play.

**Enter pairing link** accepts the full `/play?token=...` URL from the PC. **Reconnect** remembers the last paired PC; scan again after a host restart changes the token. **Preview controllers** opens bundled PS5/Xbox layouts offline. Preview sends no inputs to a PC.

## Native features

Version **1.1** adds Matte black, OLED black and Translucent themes, richer animation by default, connection haptic cues, and an interactive first-use guide. The app identity and signing key remain the same, so it installs as an update to 1.0 while keeping preferences.

Open **Settings** on the Android home screen or controller toolbar to choose a theme and turn rich animations off. Both locations share the same saved preference. Touch / connection haptics have a separate switch. Disabling motion removes custom launch, menu, connection and controller-entry animations; appearance remains selected.

The guide offers practice resizing, two-finger contacts/click and gyro/recentering. It pauses controller output first and does not send practice gestures to a game. Sensor practice temporarily enables a sensor mode without replacing the normal saved gyro mode. Skip or completion is remembered; replay it through Settings or the Getting-started walkthrough button.

The controller shows animated Connecting, Ready and Reconnecting indicators. Ready and reconnect transitions provide brief phone vibration when haptics are enabled. Actual vibration feel still depends on the phone and needs a physical check.

- Matching adaptive app icon, including a monochrome layer for themed launchers.
- A roughly one-second vector controller outline animation and wordmark fade on cold launch.
- Fullscreen landscape controller, system-bar hiding and display-cutout padding.
- Keep-screen-awake while the controller is visible.
- Offline rear-camera QR scanner with autofocus and optional flashlight. Camera frames stay on the phone. Manual pairing remains available if camera access is declined.
- Optional short button haptics, with an on/off setting on the app home screen. This is touch feedback; game-generated rumble is not implemented yet.
- **Gyro on/off** in the controller toolbar toggles motion immediately and remembers your last mode. It starts off on a fresh install. **Gyro settings** offers GTA V aiming (right-stick camera) and tilt steering (left-stick X), 25–200% sensitivity, invert X/Y and recenter. Aiming uses native angular velocity, so the camera settles when the phone stops moving. By default it activates only while holding LT/L2; turn that option off for continuous camera control. Touching the right stick takes priority over gyro. Settings are saved across PC connections. Gyro input releases on pause, sensor loss, recenter or switching off. Physical sensor direction and GTA V sensitivity still need tuning on your OnePlus.
- Pausing or leaving the app releases inputs; the PC host also keeps its disconnect watchdog.
- Existing PS5 touchpad tracking/click, layout switching and size adjustment are retained.

The app connects over the PC's existing local network connection. Wi-Fi, USB tethering and Bluetooth PAN are available through that network. Direct Bluetooth HID/BLE pairing is not implemented. The PC still performs the virtual Xbox/DS4 device emulation.

## Source and build

The native Android shell is written in Java using the Android SDK, with the existing HTML controller inside WebView. This keeps the APK small and shares the controller UI with the desktop app. The live controller UI is served by the selected PC host, so future PC-side UI changes appear without rebuilding the APK. The offline preview is bundled at build time.

- `app/src/main/java/io/pocketpad/app/MainActivity.java`: pairing, WebView, lifecycle, haptics, rotation sensors and native bridge.
- `LaunchView.java`: frame-based launch animation drawn as vectors.
- `ScanActivity.java`: offline camera QR scanning using ZXing Core.
- `app/src/main/res`: adaptive/monochrome launcher icon and launch themes.
- `build.py`: standalone Android SDK build using aapt2, javac, D8, zipalign and apksigner. It copies the current `../web` files into the offline preview.
- `bootstrap.py`: downloads SDK build tools 35.0.0, Android 35 platform and ZXing 3.5.3, verifying their published checksums. Downloads stay inside `.tools`; system settings are not changed.

From the main controller folder:

```powershell
.\runtime\python.exe android\bootstrap.py
.\runtime\python.exe android\build.py --java-home 'C:\path\to\jdk'
```

Use JDK 17 or newer. The existing local STM32CubeMX JDK was used for the original build. A local personal-use signing key is retained at `.tools/pocketpad-local.jks`, with alias `pocketpad` and the standard local-development password `android`. Keep this file if you want future builds to install as updates. It is excluded from source/portable packages. This key is for personal sideloading, not public app distribution.

The APK signature and package metadata are checked by the build. Shared controller flows are verified in the desktop browser, including a simulated native bridge and offline preview. Physical Android installation, camera scanning, haptic feel and sensor direction have not been tested on the OnePlus yet.

## Third-party notices

QR decoding includes [ZXing Core 3.5.3](https://github.com/zxing/zxing), Apache License 2.0. License and notice files are bundled in the APK's `assets/licenses` folder. Android build tools remain development dependencies and are not bundled in the APK. The controller UI and icon are original PocketPad assets.
