# PhotoBooth Offline Updates

The offline updater is installed by the base kiosk ISO. The first boot creates
the writable `PHOTOBOOTH` volume and its update directories without deleting
`Templates`, `Output`, sessions, or diagnostics.

## Layout

```text
PHOTOBOOTH/
  .photobooth-volume
  Templates/
  Output/
  Diagnostics/
  Updates/
    incoming/
    installed/
    backups/
    current/
      runtime-files.json
    logs/
    current-version.txt
    known-good-version.txt
```

The Linux live system already uses an ext4 `persistence` partition with
`/ union`. Files installed into `/opt`, `/usr/local`, and `/etc` therefore
survive a reboot. The stable squashfs remains unchanged and continues to be the
recovery base image.

## Send A Patch From macOS

Commit the change, insert the same PhotoBooth boot USB, and run:

```bash
cd "/Users/idalex/Documents/Фотобудка 2/PhotoBooth"
./SEND_UPDATE_TO_USB.sh
```

The script identifies the FAT volume by `.photobooth-volume`, reads the
installed commit from `Updates/current-version.txt`, checks every changed file
against a strict whitelist, builds `PhotoBooth-Patch-<sha>.tar.gz`, copies it to
`Updates/incoming`, and verifies the checksum on the USB. It does not eject the
USB automatically.

For application changes, the boot service records checksums of managed
`/opt/photobooth` files in `Updates/current/runtime-files.json`. The Mac sender
compares the new `dotnet publish` output with that inventory, so unchanged
self-contained runtime libraries are not included. Mutable `config.json`,
event templates, output, and logs are never overwritten by an application
patch.

If the USB is absent, the script exits safely with:

```text
PHOTOBOOTH USB not found.
Insert the PhotoBooth USB and run SEND_UPDATE_TO_USB.sh again.
```

Application updates require a .NET 8 SDK. If `dotnet` is not on `PATH`, run:

```bash
PHOTOBOOTH_DOTNET=/absolute/path/to/dotnet ./SEND_UPDATE_TO_USB.sh
```

## Install On The Photo Booth

The boot service validates a pending package and logs the result, but never
installs it silently. Open a terminal on the booth and run:

```bash
sudo photobooth-update --status
sudo photobooth-update --dry-run /media/user/PHOTOBOOTH/Updates/incoming/PhotoBooth-Patch-XXXXXXXX.tar.gz
sudo photobooth-update --install-pending
sudo systemctl reboot
```

The installer verifies the package checksum, manifest, expected previous Git
commit, destination whitelist, file checksums, metadata, and shell syntax. It
then backs up every destination and installs from a staged directory using
atomic file replacements.

## Rollback And Known Good

After a successful hardware test:

```bash
sudo photobooth-update --mark-known-good
```

Restore the immediately previous update:

```bash
sudo photobooth-update --rollback
```

Restore through the retained backup chain to the last marked version:

```bash
sudo photobooth-update --rollback-known-good
```

Backups are retained under `PHOTOBOOTH/Updates/backups`. A transaction journal
is written and synced before any runtime file changes. If power is lost during
installation, the boot check restores that backup before PhotoBooth starts. If
recovery itself cannot complete, kiosk startup remains blocked and the journal
is kept for diagnosis instead of starting a partially updated application.

Logs are stored in `PHOTOBOOTH/Updates/logs`; the last concise result is in
`PHOTOBOOTH/Updates/current/last-status.txt`.

## Full ISO Required

`SEND_UPDATE_TO_USB.sh` stops with `FULL ISO REBUILD REQUIRED` for changes to:

- Debian packages or `packages.list.chroot`;
- kernel, bootloader, GRUB, isolinux, or live-build configuration;
- partition creation, filesystem layout, or persistence architecture;
- the vendored DNP PPD or base test-print asset;
- any deleted, renamed, unknown, or non-whitelisted runtime file.

The existing `kiosk-image/BUILD_KIOSK_ISO.sh` and GitHub Actions workflow remain
the production and recovery path for these changes.

## Verification

Run the infrastructure test from the repository:

```bash
./kiosk-image/tests/test-offline-update.sh
```

It covers dry-run, checksum rejection, whitelist rejection, shell syntax
rejection, installation, backup, one-step rollback, known-good rollback,
transaction failure, and next-boot recovery. Hardware behavior must still be
verified on the real booth after installing a patch.

The macOS package-builder test is:

```bash
./kiosk-image/tests/test-send-update.sh
```
