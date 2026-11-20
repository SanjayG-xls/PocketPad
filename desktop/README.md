# PocketPad Desktop 2.2

A native WPF Windows application with a self-contained installer. The desktop UI does not use a browser, WebView, Electron or a local web page. The existing Python service continues to serve the phone controller and virtual gamepad bridge in the background; it opens no console or browser during desktop use.

## New in Desktop 2.2

The phone now includes **Virtual mouse & keyboard**: a black touchpad, mobile keyboard typing, scrolling, pinch zoom, app switching and virtual desktop gestures. Use **Mouse & keyboard** in the phone toolbar or the Android 1.3 home shortcut. Mouse/keyboard output does not require the virtual gamepad driver.

## Install

Run **PocketPad-Setup.exe**, choose an installation folder, and finish the setup wizard. The default location is `%LOCALAPPDATA%\Programs\PocketPad`. Setup installs for the current Windows user, creates Start menu entries, offers a desktop shortcut, and registers **PocketPad Desktop** in Windows Installed apps / Apps & features. Installing the virtual device driver or changing a firewall rule may separately ask for administrator approval.

The setup wizard includes original graphics, the PocketPad logo and app icon, a dark theme, page transitions, progress reporting and a launch-on-finish option. Program files are prepared in a staging folder before an existing installation is replaced. Updates keep personal layout files.

Launch **PocketPad** from the Start menu or desktop. An animated launch overlay starts the local controller engine. The normal desktop workflow stays inside the native application:

- **Controller studio:** factory PS5/Xbox layouts and two custom slots; drag controls from the library, move them, resize from the corner, edit labels/mappings/appearance, change overall scale, undo/redo and import/export JSON.
- **Connect phone:** a native QR image, network adapter selection, Android-app download QR, driver state and helpers, disconnect button and private-network firewall helper.
- **Live input:** a native controller drawing, button/axis indicators and two-finger touchpad coordinates/click state.
- **Preferences:** animation and live-preview toggles, plus links to personal layouts, logs and the Android APK.

Arrow keys nudge the selected control; Shift uses 10-unit steps. Ctrl+Z / Ctrl+Y undo and redo. Delete removes the selected control. The phone app and its controller layouts remain compatible. Game-native touchpad input uses PlayStation/DS4 output; Xbox output provides the mouse fallback.

## Personal data and engine

### Experience settings in 2.1

**Preferences** includes Matte black, OLED black and Translucent themes, with matching accent colors. Theme settings stay separate from controller styles and saved button mappings. The rich-motion switch defaults to on and controls page reveals, hover effects, connection pulses and popup motion. Turning it off stops active custom animations immediately and persists the choice.

The **Mobile app** item is permanently visible in the sidebar. It contains the Android 1.2 APK download QR, network selection, copy-link button, local Save APK / Explorer actions, installation steps and a link back to controller pairing.

The first-use walkthrough is a floating interactive card, covering pairing, resizing, touch/click and gyro/recentering. Practice controls are separate from the real layout and never send game input. Completion and Skip are remembered. Replay is available in Preferences. New users get it automatically; installed users see it on the first 2.1 launch unless they already completed it.

Saved slots live in `%APPDATA%\PocketPad\data\presets.json`. Native drafts are saved alongside that file, and desktop preferences are in `%APPDATA%\PocketPad\desktop.json`. Logs are under `%APPDATA%\PocketPad\logs`.

On a first install, the personal installer can seed the existing layouts from this workspace; an already-saved profile is never overwritten. When launched directly from this source folder, the native app can migrate the old `data/presets.json` if no profile exists. Your original workspace data is retained.

The background engine normally uses TCP port 8765. If an older incompatible host already occupies it, the native application selects another free local port and shows the correct QR link. Scan the native app's current pairing QR. Normal close shuts down an engine it started, releases inputs, and detaches its virtual device. An independently running compatible engine is left alone.

The native renderer and phone renderer share the same layout format and physical control arrangement. The phone continues to receive inputs over Wi-Fi, hotspot, USB tethering or Bluetooth PAN. Direct Bluetooth HID/BLE is not added by the Windows installer.

## Uninstall

Choose **PocketPad Desktop → Uninstall** in Windows Settings, or **Uninstall PocketPad** from the Start menu. The branded uninstaller removes application files, shortcuts and the Windows uninstall entry. It preserves personal layouts by default; a separate checkbox removes them if selected. Close the application before updating or uninstalling it. The shared ViGEmBus driver is retained because other controller software may use it.

## Build and verification

The native application uses the .NET Framework 4.8 WPF libraries included on supported Windows 10/11 installations. The bundled Python runtime and dependencies require no separate Python installation.

```powershell
.\runtime\python.exe desktop\build.py
.\runtime\python.exe installer\build.py
.\runtime\python.exe installer\check.py
```

`desktop/build.py` generates the original icon/setup graphics and compiles `PocketPad.exe`. `installer/build.py` builds a separate uninstaller, embeds the runtime/application payload, then compiles the single-file `PocketPad-Setup.exe`. No setup compiler or SDK installation is required. The setup and application sources are original code in this workspace.

The isolated check installs in `installer/build/install-smoke/PocketPad`, verifies Windows shortcuts and a separate test uninstall registry entry, launches the installed native app, exercises layout saving and native QR rendering, applies an update, and uninstalls. It verifies that personal data survives and the original workspace slots are unchanged. Native screenshots are saved under the test folder's `qa` directory. These checks do not test a physical phone or a running game session.

The installable EXE is the recommended handoff. The portable ZIP also contains the native application and its UI assets; extract the complete folder before running it.
