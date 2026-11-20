# Release notes

## v2.2.0 — Virtual Touchpad & Keyboard · Desktop 2.2 + Android 1.3

- Black fullscreen phone touchpad with a mobile keyboard button and portrait/landscape support.
- Pointer movement, taps, double-click, hold/double-tap drag, right/middle click, two-axis scroll and pinch zoom.
- Three-finger app switching, Task View, desktop and Search; four-finger virtual desktops and Quick Settings.
- Unicode/emoji, IME composition, recent-text autocorrection and keyboard shortcuts with editing/navigation/function keys.
- Driver-independent Windows mouse/keyboard output over the existing paired phone connection. Mode changes, pause, disconnect and the watchdog release mouse holds.
- Android home shortcut, native keyboard bridge and updated APK/package checks.
- GTA V Driving / On foot toggle, proportional left-stick pedals/steering and right-stick camera; new sessions start on foot.
- Regression checks cover desktop-mode ownership, Windows input packet layout, gesture recognition, text composition and gamepad isolation. Physical phone/IME feel and application-specific zoom remain manual checks.


## v2.1.0 — PocketPad Desktop 2.1 + Android 1.2

First GitHub release. Made by **Sanjay**.

### Windows desktop

- Native WPF controller studio, local connection engine and live input monitor.
- Custom installer and uninstaller, branded artwork, app icon, shortcuts and startup animation.
- Xbox and PS5-style factory layouts plus two persistent custom slots.
- Drag-and-drop library, per-control resizing, overall HUD scale, JSON import/export and undo/redo.
- Dedicated Android download page with QR, local download link and APK export.
- Matte black, OLED black and translucent themes; rich animations enabled by default with an off switch.
- Connecting, ready and reconnecting states, plus a first-use practice walkthrough.
- Xbox 360/XInput output or DualShock 4 output with two native touch contacts and click.
- **Made by Sanjay** credit in Preferences and Windows publisher metadata.

### Android companion 1.2

- Matching fullscreen controller UI, native camera QR scanner, manual pairing and offline preview.
- Visible **Gyro on/off** button and dedicated gyro settings.
- GTA V aiming using rotation speed mapped to the right stick; optional LT/L2-only activation, enabled by default.
- Tilt steering, 25–200% sensitivity, invert X/Y and recenter.
- Right-stick touch takes priority over gyro; motion clears on pause, off, stale sensor data or recenter.
- Saved gyro preferences, adjustable phone control scale and optional haptic feedback.
- Launch animation, app icon, themes, first-use guide and animation-off setting.
- **Made by Sanjay** credit on the home screen.

### Distribution and validation

- Windows setup, complete portable ZIP, signed Android APK and SHA-256 checksum list.
- Public packages exclude personal saved presets and Android signing keys.
- Setup retries brief Windows folder locks before completing its atomic update, with rollback preserved.
- Verified APK contents/signature, host input checks, browser/controller workflow, gyro input behavior and isolated install/update/uninstall.
- Physical phone camera behavior, gyro direction/feel, Bluetooth PAN and actual GTA V gameplay remain manual checks.

### Compatibility notes

- Windows 10/11 x64, .NET Framework 4.8 and ViGEmBus for controller output; Android 8.0+ for the APK.
- PS5 is a HUD preset. The Windows PlayStation device is a virtual DualShock 4, rather than native DualSense hardware.
- Wi-Fi, hotspot and network tethering are supported paths. Bluetooth uses PAN networking, not direct Bluetooth HID/BLE pairing.
- No game-generated rumble, adaptive triggers or multiple simultaneous gamepads.
