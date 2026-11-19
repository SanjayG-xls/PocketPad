# PocketPad Desktop 2.1

A native Windows desktop application that turns your phone into an Xbox or PlayStation-compatible PC gamepad. The PC interface runs in its own application window, without a browser or WebView. The Android companion and optional phone browser controller connect to the local background engine. Gameplay connections remain on your local network. Python and its dependencies are included.

Desktop 2.1 adds a branded setup wizard, native dark UI, original icons and setup graphics, launch animation, page transitions, shortcuts, Windows Installed apps registration and a matching uninstaller. It keeps the larger black DualSense/Xbox layouts and the Android companion. Existing saved layouts can be migrated into your Windows profile on first install.

## Start playing

1. Run **PocketPad-Setup.exe**, choose a destination and finish the installation. It offers a desktop shortcut and installs a Start menu entry and uninstaller.
2. Open **PocketPad** from the desktop or Start menu. Its native window starts the controller engine silently. Keep the application open while playing; you can minimize it.
3. If the app reports that the controller driver is missing, use **Connect phone → Install virtual controller driver**. Complete the bundled signed ViGEmBus installer and restart Windows if requested. Normal use of the app does not need administrator privileges.
4. Put the PC and phone on the same Wi-Fi, or connect your phone to a PC hotspot or a tethering network. Open **Connect phone** and select the correct PC network address.
5. In the Android companion, scan the native app's pairing QR. Alternatively use your phone camera to open the optional browser controller. Turn the phone sideways and tap **Start controller**. Choose Xbox output for XInput games, or PS5/DS4 for games with DS4 support before opening a game.

If the phone cannot open the link, try another PC network address in the pairing panel. The native **Connect phone** screen also includes a private-network firewall helper. For portable use, **Allow Private Network.cmd** provides the same optional helper. See the [native desktop guide](../desktop/README.md) for install, update, uninstall and data details.

## Android companion app

Each controller session starts in **On foot** mode: L moves your character and R controls the camera. After entering a vehicle, tap **On foot** in the toolbar to enable **Driving**: L up sends proportional RT/R2 acceleration, L left/right steers, and L down sends proportional LT/L2 brake/reverse. R controls the camera. Steering and pedals use independent drag distances, so turning does not reduce throttle. Release L to release both pedals and center the wheel. After leaving the vehicle, tap **Driving** to restore **On foot** movement. PocketPad cannot detect the game's vehicle state, so switch modes manually; pedal mappings can trigger attacks while on foot. Driving mode is not restored when reopening the controller. Switching modes, pausing, and disconnecting release active inputs.

Desktop **2.1** adds a permanent **Mobile app** sidebar page with the Android download QR, instructions, copy-link and Save APK actions. The companion update is **Android 1.2**; install the updated APK to get the new native settings, connection haptics and walkthrough support.

## Appearance and getting started

- **Matte black**, **OLED black** and **Translucent** themes are available on PC and mobile, each with matching accents. They are device appearance settings and do not change saved button mappings. OLED uses true-black backgrounds; Translucent layers surfaces within the application.
- **Rich animations are on by default** on both platforms: launch reveals, larger page/menu transitions, connection rings, hover/touch effects and staggered controller entry. Turn them off in PC **Preferences**, Android home **Settings**, or the controller toolbar's **Settings** menu. The choice is saved on that device. Existing explicit animation-off preferences are kept.
- Connection indicators distinguish **Connecting**, **Ready** and **Reconnecting**. The Android app gives short ready/reconnect haptic cues when haptics are enabled. A temporary network interruption releases inputs and resumes previously active play after reconnection while the app remains in the foreground. Paused or backgrounded play is not resumed automatically.
- A first-use guide covers pairing, sizing, two-finger touch/click and gyro/recentering. Its practice area does not send game input or modify a saved layout. Skip it or replay it from Settings / Preferences. Gyro practice uses a temporary sensor mode and restores the normal mode afterward.

## Android companion details

Android 1.2 adds a visible **Gyro on/off** button and **Gyro settings**. Choose **GTA V aiming** for phone movement mapped to the right stick, with LT/L2-only activation by default; use the thumb stick for big turns. Choose **Driving** for tilt steering. Sensitivity, invert axes and recenter are available. Fresh installs start with gyro off, and settings persist across connections. The camera settles when the phone stops moving; manual right-stick input takes priority. Actual GTA V tuning on the OnePlus remains a physical-device check.


**PocketPad-Android.apk** is an installable Android app with the same controller UI, a matching adaptive icon, and a short animated launch screen. It adds offline camera QR scanning, optional button haptics, gyro steering/aiming, fullscreen landscape and keep-screen-awake during play. Game rumble is not included yet.

Copy the APK to your phone and open it, or select **Connect phone → Install Android companion** to show the download QR code. After installing, open the app and scan the PC's **Connect controller** QR. Keep the PC application running. The app's **Preview controllers** option works offline without sending game inputs. Your previous saved layouts remain on the PC.

See [Android setup and source guide](../android/README.md) for installation, controls, permissions and rebuilding the APK. The APK was built and signature-verified; physical installation, camera behavior, vibration and gyro direction still need a check on your phone.

## Layout studio

- **Xbox** and **PS5** are factory presets, using the stick/D-pad arrangement in the supplied controller images. Xbox has colored ABXY letters and a faceted D-pad; PS5 has white outlined symbols, separate directional buttons, aligned lower sticks and a central blue-edged touchpad. Controls are approximately 30–50% larger than version 1.0, depending on type.
- The Xbox preset outputs an Xbox 360/XInput device. The PS5 preset outputs a virtual DualShock 4 with two-finger touch input and touchpad click. ViGEmBus supports DS4, rather than native DualSense emulation. Adaptive triggers, a real microphone/mute function, and DualSense-specific features are not emulated. The small PS5 mute mark is visual only. The Xbox share-style button maps to View/Back because the emulated Xbox 360 has no dedicated Share input.
- Two additional **Custom** slots are stored on the PC. Select a built-in preset, edit it, and use **Save layout**. Saving to an occupied custom slot replaces it. Factory presets remain available.
- Drag controls from the library onto the canvas, or tap them to add. Move controls by dragging them. Select one to change its label, mapping, position, size, color, opacity, shape, or joystick deadzone.
- Every control has adjustable width and height, including sticks, D-pads, face buttons, shoulder controls and the touchpad. Select a control and use its **Size** slider, enter width/height, or drag its corner handle. The **All controls** slider below the canvas scales the whole layout, including icons. Save these changes into a custom slot.
- On the phone, **Size** changes the scale on that phone immediately and remembers it in that browser. It combines with the saved layout scale. **Reset to layout size** returns the phone multiplier to 100%. At large settings, controls may overlap; use the editor to move them apart.
- The library covers all gamepad buttons, both sticks, stick clicks, the directional pad, analog triggers, axis sliders, a two-finger touchpad, a separate touchpad click button, and decorative HUD text. Steering, throttle and brake sliders map to gamepad axes; set the matching bindings in your game.
- Use **Done** or click empty canvas space for layout-wide theme, accent and background settings. Clear the canvas to start from scratch.
- **Ctrl+Z**, **Ctrl+Y**, and **Delete** work in the desktop editor. Unsaved drafts are recovered in the same browser; saved custom layouts are available to any connected phone.
- Export/import JSON files for backups and alternate layouts. Four slots remain available at a time: two built-in and two custom. Saved changes immediately update the phone.
- Use **Layouts** on the phone to switch between saved presets during a session. Switching releases all current inputs.
- The native **Live input** page shows received buttons, axes, both touch contacts and click state, with a native drawing of the controller. The desktop app does not open a browser preview. The layout canvas can also display live phone input.

## Touchpad

Slide one or two fingers over the PS5 touch surface to send independent contact positions. Use a quick **two-finger tap** to click, or hold the separate small **Touchpad click** button below the surface. The click button can be moved, resized, duplicated or added from the library. Phone glass has no mechanical click, so these gestures provide the click action while allowing the surface to track two fingers independently.

With **PlayStation / DS4 output**, contacts and click are sent as native DS4 extended HID reports. A game must support PlayStation/DS4 touchpad input for gestures to do anything; Steam Input settings can change how the device is mapped. This does not create native PS5 hardware.

With **Xbox / XInput output**, the touch surface moves the Windows mouse and the touchpad click performs a left mouse click. Add the touchpad from the library if you want it in an Xbox layout. The desktop controller style and PC output type are separate settings under **Layout settings**. Save the layout to apply output changes; Windows detaches the previous virtual device and connects the selected type. Choose the output before opening a game, or restart a game that does not detect device changes.

## Connection options

**Wi-Fi / hotspot:** the main supported method. The QR code opens the phone controller over the local network; scanning a QR code is a pairing shortcut, not a separate radio transport.

**Bluetooth tethering / PAN:** supported when the phone and Windows provide a Bluetooth network connection. Pair the devices in system settings, enable Bluetooth tethering on the phone, and join its Personal Area Network from Windows. Start/restart PocketPad afterward so it discovers the network interface, then select that PC IP in the pairing panel. Open its link on the phone. This uses HTTP/WebSocket over the Bluetooth network. Direct Bluetooth HID or BLE pairing is not implemented. Availability depends on your phone and Windows; use Wi-Fi if PAN is unavailable.

**USB tethering:** also works as a local IP network. Enable USB tethering, restart PocketPad to discover the adapter, and use its PC network address.

The phone and PC need to be able to reach one another. Guest networks, AP/client isolation, and some VPNs can prevent this. Use a PC hotspot or choose the correct network adapter. Internet is not needed during play. The app serves all UI assets and QR generation locally.

## Troubleshooting

- **Driver setup needed:** run the bundled installer, then restart the host. The connection panel shows the real driver error; a paired phone alone does not mean gamepad emulation is available.
- **Check the device:** press Win+R, run `joy.cpl`, select the Xbox 360 or Wireless Controller device, and open Properties. Move the phone's sticks and press buttons. If Windows sees input but a game does not, check the selected PC output, enable gamepad input in that game, restart it after connecting PocketPad, and check Steam Input settings. `joy.cpl` does not display native touch coordinates; use PocketPad's Inputs view for the received contacts.
- **Old QR code stops working:** every host start generates a new pairing token. Scan the current code again.
- **Another phone is playing:** pause/close the first phone page, or use Disconnect phone in the PC connection panel. Only one device drives the virtual controller at a time.
- **Phone locks or switches apps:** held controls are released, and play pauses when the page is hidden. Tap Start controller to resume. Screen wake lock and fullscreen depend on browser support; wake lock often requires HTTPS, so adjust your phone's screen timeout if needed. Landscape controls also work without fullscreen.
- **Port in use:** close the other host window. Advanced users can run `Start PocketPad.cmd --port 8766`; adjust firewall rules if changing the port.
- **Game compatibility:** requires XInput or DS4 support according to the chosen output. Game-specific anti-cheat or controller restrictions may affect recognition. Rumble, macros, keyboard mappings, native Bluetooth pairing, and multiple simultaneous gamepads are not included. Mouse output is limited to the touchpad fallback.

## Files and development

- `host.py`: aiohttp HTTP/WebSocket host, QR codes, preset persistence, Xbox/DS4 bridge, native touch report packing and mouse fallback.
- `web/`: offline HTML/CSS/JavaScript interface and built-in presets.
- `%APPDATA%\PocketPad\data\presets.json`: installed/native app's two custom slots and selected layout. The source folder's `data/presets.json` is the earlier storage location and is preserved during migration.
- `runtime/`: portable Python 3.12 and dependencies; do not move it away from the application.
- `tools/ViGEmBus_1.22.0_x64_x86_arm64.exe`: official signed driver installer.
- `desktop/`: native WPF editor, connection UI, animation, controller renderer, icon/setup artwork generator and build script.
- `installer/`: native setup wizard, uninstaller, embedded-payload build script and isolated installation check.

The normal entry point is `PocketPad.exe` or the installed shortcut. Developers can run `runtime\python.exe host.py --no-browser` to start only the background engine. `--mock` explicitly monitors inputs without creating a virtual controller; the interface labels this mode. Test-only runs do not verify real game output. The native app also accepts `--mock` for development.

The host resets all inputs on disconnect, preset switches, and after 600 ms without input packets. The phone sends complete input snapshots, supports simultaneous touches, and reconnects after network interruptions. It listens on port 8765 of the PC's network adapters for personal local-network use.

Focused checks are included in `tools/check_host.py` (pairing, presets, input mapping, DS4 touch bytes, settings and release), `tools/check_browser.py` (desktop/phone workflow, sizing, resize handles, two-finger tracking and click using installed Microsoft Edge), `tools/check_device.py` (a real Windows XInput device and centered input), and `tools/check_ds4.py` (real ViGEmBus acceptance of extended two-contact DS4 reports and release). They passed during development. Actual phone hardware, Bluetooth PAN, and a running PC game have not been tested here. The browser check uses an isolated test-mode host and does not modify your custom slots.

`PocketPad-Portable.zip` is a ready-to-extract copy of the application. Extract the whole archive before running it. Keep the executable, `runtime`, `web`, and `tools` together. Your personal saved slots in `data` are excluded from that archive; export or back them up separately.

The recommended installation is **PocketPad-Setup.exe**. It installs per user under `%LOCALAPPDATA%\Programs\PocketPad` by default, keeps personal data under `%APPDATA%\PocketPad`, and can be removed through Windows Installed apps. The branded uninstaller keeps your layouts by default. The portable version must also retain the `desktop` UI/assets folder and `PocketPad.exe.config`.

## Dependencies and reference

The interface was inspired by the phone controller and drag-and-drop workflow at [RojX](https://rojx.io). It uses original code and UI assets.

- [aiohttp](https://docs.aiohttp.org/) — HTTP / WebSocket server (Apache-2.0).
- [qrcode](https://github.com/lincolnloop/python-qrcode) — local SVG QR generation (BSD).
- [vgamepad](https://github.com/yannbouteiller/vgamepad) — Xbox 360 / DualShock 4 emulation through ViGEmBus (MIT). Native touch reports follow [ViGEmClient's packed DS4 report layout](https://github.com/nefarius/ViGEmClient/blob/master/include/ViGEm/Common.h).
- [ViGEmBus 1.22.0](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0) — required Windows virtual gamepad driver. This upstream project is archived; the final signed installer is bundled for local use.
- [Python](https://www.python.org/) — portable runtime (PSF license, included in `runtime/LICENSE.txt`). Dependency license notices are retained inside `runtime/Lib/site-packages`.
