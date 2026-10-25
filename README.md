# blippy

Easily tell Bluetooth device battery levels.

Blippy lives in the Windows tray and shows how much charge your controllers, headphones, mice and keyboards
have left. Glance at the taskbar and every device has its own little icon with its percentage on it. Click
one and a bouncy panel springs up with every device, a battery bar and a word for how it is doing.

If you have a PS5 DualSense, Blippy can also paint the battery onto the controller's lightbar: a full
controller glows in a color you pick, and it slides toward red as it drains.

> **Status: MVP.** It builds and its logic is tested, but it has not yet been tried against real
> controllers. See [What still needs a real-device check](#what-still-needs-a-real-device-check).

## Features

- **One tray icon per device**, showing the percentage on a tile colored by level (green, amber, red;
  lavender while charging). Or switch to the device's own icon with a draining bar under it. Or one summary
  icon for the lowest battery.
- **Hover** any icon for its name and exact level. **Click** for the flyout. **Right-click** for Check now,
  Settings and Quit.
- **The flyout** follows the light or dark Windows theme in the same lavender palette as flippy. Cards pop
  in one after another on springs, battery bars spring to their level while the number counts up, the
  charging bolt wiggles, buttons squish, the gear turns into a close button, and the header light drifts.
  When Windows has "Animation effects" turned off, everything just appears.
- **Notices connections right away.** Plugging in, unplugging, pairing or switching a device off triggers a
  check within a second (and once more a few seconds later, for pads that are slow to start reporting),
  instead of waiting for the next timed check.
- **DualSense lightbar gradient** (off by default): pick one of eight colors or any color you like. The
  settings show the gradient exactly as the controller will. Blippy only writes the lightbar when the color
  should change or the controller's connection changes (plugging a DualSense in or out resets it to blue),
  so a game that sets its own color is not fought every few seconds.
- **Low battery nudges** at 20% and 10%, once each, re-armed when the device charges.
- **Start with Windows**, refresh every 15 s, 30 s, 1 min or 2 min.

## Where the numbers come from

| Devices | Source |
|---|---|
| DualSense and DualSense Edge (Bluetooth or USB) | Read straight from the controller's HID input report. Windows does not report its battery anywhere else. Over Bluetooth Blippy reads the calibration feature report once to switch it to full reports, as the Linux driver does. |
| Xbox controllers, and other pads Windows knows | `Windows.Gaming.Input` battery reports. |
| Headphones, earbuds, mice, keyboards, BLE controllers | The battery level Windows shows in **Settings › Bluetooth & devices** (`DEVPKEY_Bluetooth_Battery`). |

When two sources see the same device (an Xbox controller is often seen by both of the last two) it is shown
once, from the more specific source. A DualSense charging over USB while it is still connected over
Bluetooth shows up to Windows as two devices; Blippy recognises them as one controller by its Bluetooth
address and shows it once. The DualSense reports in steps of 10% because that is all the
controller itself reports.

## Install

Grab `Blippy-win-x64` from the latest CI run's artifacts, unzip it anywhere and run `Blippy.exe`. It is a
single self-contained file; nothing to install. Windows 10 (2004) or newer.

New tray icons start out under the **^** arrow; drag them onto the taskbar so they are always visible.

Settings live in `%APPDATA%\Blippy\settings.json`, and a short log (useful if a device does not show up) in
`%LOCALAPPDATA%\Blippy\blippy.log`, which **Settings › Open log** opens.

## Build

You need the .NET 10 SDK. The app is Windows-only, but everything builds on Linux and macOS too, and the
core tests run anywhere.

```bash
dotnet build Blippy.slnx
dotnet test tests/Blippy.Core.Tests
dotnet run --project src/Blippy                          # Windows only
dotnet run --project src/Blippy -- --preview preview     # renders screenshots with sample devices, then exits
```

CI (`.github/workflows/ci.yml`, on `windows-latest`) runs the tests, publishes a single-file
`Blippy.exe`, runs it once with `--preview` to prove the UI loads on Windows, and uploads both the app and
the screenshots as artifacts.

## Layout

```
src/Blippy.Core/          Platform-free logic, all unit tested
  DualSense.cs              battery parsing and lightbar output reports (USB and Bluetooth, with CRC)
  LightbarGradient.cs       your color → red, blended through hue so green passes yellow, not brown
  ReadingMerger.cs          one entry per device across sources
  DeviceClassifier.cs       "WH-1000XM4 Hands-Free AG" → headphones called "WH-1000XM4"
  LowBatteryAlerts.cs       when to nudge
  Spring.cs                 damped spring math (same damping/stiffness/mass presets as flippy)
  Settings.cs               JSON settings with atomic save
src/Blippy/               The WPF tray app
  Providers/                DualSense HID, Windows.Gaming.Input, Windows Bluetooth battery property
  Tray/                     tray icons drawn at runtime, menu, nudges
  Ui/                       flyout window, device cards, styles, theme
  Motion/                   SpringEase for XAML, squish-on-press, pop/boing/float helpers
  Assets/Icons/             Tabler SVG icons (MIT)
tests/Blippy.Core.Tests/  xUnit tests
```

## What still needs a real-device check

These are written from documentation and the Linux driver, and are the first things to try on hardware:

- DualSense battery and lightbar over **Bluetooth** on Windows (report sizes as Windows exposes them, and the
  calibration read that switches the pad to full reports). USB is the simpler case.
- Whether Steam Input, DS4Windows or a game holding the DualSense blocks Blippy from reading or writing it.
- The Bluetooth "connected" and "present" properties used to hide paired devices that are switched off.
  If they turn out to be wrong, Blippy logs it and falls back to the battery property alone.
- Xbox controller reports through `Windows.Gaming.Input` while Blippy has no focused window.

Ideas for later: DualShock 4 support (same approach, different report), Nintendo Pro Controller, a
per-device "hide" option, and an installer.

## Licenses

Blippy's icons come from [Tabler Icons](https://tabler.io/icons) (MIT). See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the full list and the other dependencies.
