# Third-party notices

## Icons: Tabler Icons (MIT)

Every icon in `src/Blippy/Assets/Icons/` is an unmodified SVG from [Tabler Icons](https://tabler.io/icons) v3.49.0
(`@tabler/icons` on npm), © 2020-2026 Paweł Kuna, under the MIT License. The full license text ships next to
the icons in `src/Blippy/Assets/Icons/LICENSE-tabler-icons.txt` and is copied next to `Blippy.exe` on build.

| File | Used for |
|---|---|
| `device-gamepad-2.svg` | Controllers (DualSense, Xbox, other gamepads) |
| `playstation-triangle.svg` | Badge on DualSense controllers |
| `brand-xbox.svg` | Badge on Xbox controllers |
| `headphones.svg` | Headphones |
| `headset.svg` | Headsets |
| `earphone-bluetooth.svg` | Earbuds |
| `mouse.svg` | Mice |
| `keyboard.svg` | Keyboards |
| `device-speaker.svg` | Speakers |
| `device-watch.svg` | Watches |
| `bluetooth.svg` | Anything else, and the tray icon before a device shows up |
| `bolt.svg` | Charging |
| `settings.svg`, `refresh.svg`, `x.svg` | Flyout buttons |

The PlayStation and Xbox marks are trademarks of Sony Interactive Entertainment and Microsoft. Blippy uses
them only to say which controller a battery belongs to and is not affiliated with either company.

## HidSharp (Apache 2.0)

[HidSharp](https://www.zer7.com/software/hidsharp) by James F. Bellinger, used to talk to the DualSense over HID.
Apache License 2.0.

## DualSense report layout

The DualSense battery and lightbar report layouts in `src/Blippy.Core/DualSense.cs` were written from the
documented structures in the Linux kernel's `drivers/hid/hid-playstation.c`. No code was copied.
