# PhotoBooth USB image

The GitHub artifact contains a complete GPT disk image rather than a hybrid
ISO. It is ready before the first boot and contains:

1. `BIOS-BOOT` — GRUB support for legacy BIOS;
2. `EFI` — UEFI boot files;
3. `PHOTOBOOT` — the Debian live system;
4. `PHOTOBOOTH` — a 1 GiB FAT32 exchange volume visible in macOS, Windows and
   Linux;
5. `persistence` — the Linux persistence overlay.

The image is 6144 MiB so it fits ordinary drives sold as 8 GB. Any space left
after the image is intentionally unallocated and does not affect operation.

## Writing on macOS

1. Download the GitHub artifact.
2. Double-click `PhotoBooth-Kiosk-amd64.img.gz` to obtain the `.img` file.
3. Run `ЗАПИСАТЬ ТЕСТОВУЮ ФЛЕШКУ.command` and drag the `.img` into its Terminal
   window.
4. After verification and ejection, reconnect the drive. Finder should show
   the `PHOTOBOOTH` volume.

Copy all frames directly into the shared `Templates` folder. Each frame
consists of a PNG and JSON pair. The two base names must match exactly because
the application pairs them by filename:

```text
PHOTOBOOTH/Templates/1.png
PHOTOBOOTH/Templates/1.json
PHOTOBOOTH/Templates/2.png
PHOTOBOOTH/Templates/2.json
```

For example, `frame.png` with `1.json` is not a valid pair. The preinstalled
Triumph frame is included as `Templates/triumph.png` and
`Templates/triumph.json`.

The home screen branding can be replaced from macOS without changing the
application. Put a 16:9 JPEG background (1280x720 through 3840x2160, up to
10 MiB) and a PNG logo into these fixed paths:

```text
PHOTOBOOTH/Branding/home-background.jpg
PHOTOBOOTH/Branding/home-logo.png
```

Missing, damaged or oversized files are ignored and the built-in Triumph
design is used instead. Branding changes are loaded on the next kiosk start.

Always eject `PHOTOBOOTH` before removing the drive. Changes to templates are
loaded on the next kiosk start.
