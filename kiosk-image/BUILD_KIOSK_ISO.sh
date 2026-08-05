#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This image must be built on Linux (GitHub Actions is supported)."
  exit 1
fi

if ! command -v sfdisk >/dev/null 2>&1; then
  if [[ "$EUID" -eq 0 ]]; then
    apt-get update
    apt-get install -y fdisk
  else
    sudo apt-get update
    sudo apt-get install -y fdisk
  fi
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
image_root="$repo_root/kiosk-image"
build_root="$image_root/build"
output_root="$image_root/output"
publish_root="$build_root/app"

rm -rf "$build_root" "$output_root"
mkdir -p "$build_root" "$output_root"

if [[ -n "${PHOTOBOOTH_PREPUBLISHED_DIR:-}" ]]; then
  mkdir -p "$publish_root"
  cp -a "$PHOTOBOOTH_PREPUBLISHED_DIR/." "$publish_root/"
else
  dotnet publish "$repo_root/PhotoBooth.Linux/PhotoBooth.Linux.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$publish_root"
fi

cd "$build_root"
lb config noauto \
  --mode debian \
  --distribution trixie \
  --architectures amd64 \
  --binary-images iso-hybrid \
  --debian-installer none \
  --archive-areas "main contrib non-free-firmware" \
  --bootappend-live "boot=live components persistence quiet splash locales=ru_RU.UTF-8 keyboard-layouts=ru username=user hostname=photobooth user-default-groups=audio,video,plugdev,netdev,lp,lpadmin,scanner"

mkdir -p config/bootloaders
cp -a /usr/share/live/build/bootloaders/isolinux config/bootloaders/
sed -i 's/^timeout .*/timeout 10/' config/bootloaders/isolinux/isolinux.cfg
cp -a /usr/share/live/build/bootloaders/grub-pc config/bootloaders/
sed -i '/^set default=0$/a set timeout=1' config/bootloaders/grub-pc/config.cfg

mkdir -p config/package-lists
cp "$image_root/packages.list.chroot" config/package-lists/photobooth.list.chroot

mkdir -p config/includes.chroot/opt/photobooth
cp -a "$publish_root/." config/includes.chroot/opt/photobooth/

mkdir -p config/includes.chroot/etc/lightdm/lightdm.conf.d
cp "$image_root/lightdm-photobooth.conf" \
  config/includes.chroot/etc/lightdm/lightdm.conf.d/50-photobooth.conf

mkdir -p config/includes.chroot/etc/xdg/openbox
cp "$image_root/openbox-autostart" config/includes.chroot/etc/xdg/openbox/autostart

mkdir -p config/includes.chroot/etc/X11/xorg.conf.d
cp "$image_root/99-keetouch-calibration.conf" \
  config/includes.chroot/etc/X11/xorg.conf.d/99-keetouch-calibration.conf

mkdir -p config/includes.chroot/usr/local/sbin
cp "$image_root/photobooth-first-boot" \
  config/includes.chroot/usr/local/sbin/photobooth-first-boot
cp "$image_root/photobooth-printer-setup" \
  config/includes.chroot/usr/local/sbin/photobooth-printer-setup
cp "$image_root/photobooth-install-touch-calibration" \
  config/includes.chroot/usr/local/sbin/photobooth-install-touch-calibration

mkdir -p config/includes.chroot/usr/local/bin
cp "$image_root/photobooth-touch-setup" \
  config/includes.chroot/usr/local/bin/photobooth-touch-setup
cp "$image_root/photobooth-touch-calibrate" \
  config/includes.chroot/usr/local/bin/photobooth-touch-calibrate
cp "$image_root/photobooth-hardware-diagnostics" \
  config/includes.chroot/usr/local/bin/photobooth-hardware-diagnostics

mkdir -p config/includes.chroot/etc/systemd/system
cp "$image_root/photobooth-persistence.service" \
  config/includes.chroot/etc/systemd/system/photobooth-persistence.service
cp "$image_root/photobooth-printer-setup.service" \
  config/includes.chroot/etc/systemd/system/photobooth-printer-setup.service

mkdir -p config/hooks/live
cat > config/hooks/live/010-photobooth-kiosk.hook.chroot <<'HOOK'
#!/bin/sh
set -eu
chmod +x /opt/photobooth/PhotoBooth.Linux /opt/photobooth/*.sh
chmod +x /usr/local/sbin/photobooth-first-boot
chmod +x /usr/local/sbin/photobooth-printer-setup
chmod +x /usr/local/sbin/photobooth-install-touch-calibration
chmod +x /usr/local/bin/photobooth-touch-setup
chmod +x /usr/local/bin/photobooth-touch-calibrate
chmod +x /usr/local/bin/photobooth-hardware-diagnostics
chown -R 1000:1000 /opt/photobooth
mkdir -p /media/user/PHOTOBOOTH
chown 1000:1000 /media/user/PHOTOBOOTH
printf 'LABEL=PHOTOBOOTH /media/user/PHOTOBOOTH vfat defaults,nofail,uid=1000,gid=1000,umask=0022 0 0\n' \
  >> /etc/fstab
printf 'user ALL=(root) NOPASSWD: /usr/bin/systemctl reboot\n' \
  > /etc/sudoers.d/photobooth-reboot
printf 'user ALL=(root) NOPASSWD: /usr/local/sbin/photobooth-first-boot\n' \
  > /etc/sudoers.d/photobooth-storage
printf 'user ALL=(root) NOPASSWD: /usr/local/sbin/photobooth-printer-setup\n' \
  > /etc/sudoers.d/photobooth-printer
printf 'user ALL=(root) NOPASSWD: /usr/local/sbin/photobooth-install-touch-calibration *\n' \
  > /etc/sudoers.d/photobooth-touch
chmod 440 /etc/sudoers.d/photobooth-reboot
chmod 440 /etc/sudoers.d/photobooth-storage
chmod 440 /etc/sudoers.d/photobooth-printer
chmod 440 /etc/sudoers.d/photobooth-touch
systemctl enable photobooth-persistence.service
systemctl enable photobooth-printer-setup.service
systemctl enable cups.service
systemctl enable lightdm.service
HOOK
chmod +x config/hooks/live/010-photobooth-kiosk.hook.chroot

if [[ "$EUID" -eq 0 ]]; then
  lb build
else
  sudo lb build
fi

grep -q '^timeout 10$' binary/isolinux/isolinux.cfg
grep -q '^set timeout=1$' binary/boot/grub/config.cfg

iso_path="$(find . -maxdepth 1 -type f -name '*.hybrid.iso' -print -quit)"
if [[ -z "$iso_path" ]]; then
  echo "The kiosk ISO was not produced."
  exit 1
fi

chmod +x "$image_root/VERIFY_PARTITION_LAYOUT.sh"
"$image_root/VERIFY_PARTITION_LAYOUT.sh" "$iso_path"

cp "$iso_path" "$output_root/PhotoBooth-Kiosk-amd64.iso"
(
  cd "$output_root"
  sha256sum PhotoBooth-Kiosk-amd64.iso > PhotoBooth-Kiosk-amd64.iso.sha256
  split -b 1500M -d -a 2 PhotoBooth-Kiosk-amd64.iso PhotoBooth-Kiosk-amd64.iso.part-
)

echo "Kiosk image created in $output_root"
