# PhotoBooth Linux

This project is the Linux/Avalonia port of the existing WPF application.
The Windows project remains available in `PhotoBooth/`.

## Current milestone

- Starts as a borderless fullscreen kiosk.
- Creates or resumes event sessions.
- Uses the existing `config.json`.
- Discovers the existing PNG and JSON templates.
- Shows and selects real template previews.
- Protects settings with PIN `2016`.
- Shows the built-in usage instructions.
- Uses Canon EOS cameras through gPhoto2 when available, including Live View and capture.
- Falls back to the complete demo capture flow when hardware is unavailable.
- Animates accepted photos into the shot rail.
- Composes the final PNG from the selected template and accepted photos.
- Prints one to three copies to a detected DNP DS-RX1 CUPS queue.
- Falls back to simulated printing when CUPS or the printer is unavailable.
- Shows the current session's print history and supports reprinting.
- Returns to the home screen after printing.
- Publishes as a self-contained `linux-x64` application.

Captured originals are saved under the active session in `Output/.../Photos/`.
Composed print files are saved in `Output/.../Prints/`.

## Build

```bash
./BUILD_LINUX.sh
```

The output is written to `PhotoBooth-Linux-x64/`.

For a windowed development launch:

```bash
PHOTOBOOTH_WINDOWED=1 dotnet run --project PhotoBooth.Linux/PhotoBooth.Linux.csproj
```

For a non-graphical workflow check:

```bash
dotnet run --project PhotoBooth.Linux/PhotoBooth.Linux.csproj -- --smoke-test
```

## Linux runtime packages

The first kiosk image will use Debian with X11. Avalonia requires:

```bash
sudo apt install libx11-6 libice6 libsm6 libfontconfig1
```

Install camera and printer support on the kiosk once:

```bash
./INSTALL_LINUX_HARDWARE.sh
sudo reboot
./CONFIGURE_DNP_RX1.sh
./CHECK_LINUX_HARDWARE.sh
```

`config.json` uses real hardware by default and keeps
`HardwareFallbackToDemo` enabled. Set `DemoMode` to `true` to force the
emulator. The default RX1 10x15 media key is `w288h432`; verify it with
`lpoptions -p DNP_RX1 -l` on the actual printer before the first event.

## Bootable USB

Use the Russian step-by-step guide included in the publish directory:
`BOOTABLE_USB_WINDOWS_RU.md`. It creates a Debian Live Xfce USB with
persistence. Run `SETUP_KIOSK.sh` once inside Debian to install hardware
packages, copy the application and enable fullscreen autostart.
