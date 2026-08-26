#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "The USB image must be built on Linux."
  exit 1
fi

iso_path="${1:?Usage: BUILD_USB_IMAGE.sh ISO OUTPUT_IMAGE [TEMPLATES_DIR]}"
output_path="${2:?Usage: BUILD_USB_IMAGE.sh ISO OUTPUT_IMAGE [TEMPLATES_DIR]}"
templates_dir="${3:-}"
image_size_mib="${PHOTOBOOTH_USB_IMAGE_SIZE_MIB:-6144}"

for utility in losetup sgdisk mkfs.vfat mkfs.ext4 grub-install mount umount; do
  command -v "$utility" >/dev/null 2>&1 || {
    echo "Required utility is missing: $utility" >&2
    exit 1
  }
done

if (( image_size_mib < 5120 )); then
  echo "PHOTOBOOTH_USB_IMAGE_SIZE_MIB must be at least 5120 MiB." >&2
  exit 1
fi

work_root="$(mktemp -d --tmpdir photobooth-usb-image.XXXXXX)"
loop_device=""
mounted_live=false
mounted_data=false
mounted_efi=false
mounted_iso=false
mounted_persistence=false

cleanup() {
  set +e
  $mounted_iso && umount "$work_root/iso"
  $mounted_persistence && umount "$work_root/persistence"
  $mounted_efi && umount "$work_root/live/boot/efi"
  $mounted_data && umount "$work_root/data"
  $mounted_live && umount "$work_root/live"
  [[ -z "$loop_device" ]] || losetup -d "$loop_device"
  rm -rf "$work_root"
}
trap cleanup EXIT

mkdir -p "$(dirname "$output_path")" "$work_root/iso" "$work_root/live" \
  "$work_root/data" "$work_root/persistence"
truncate -s "${image_size_mib}M" "$output_path"

# A conventional GPT is intentionally used instead of the Apple-partition-map
# embedded in Debian's hybrid ISO. macOS can therefore mount PHOTOBOOTH normally.
sgdisk --zap-all "$output_path"
sgdisk \
  --new=1:1MiB:+2MiB --typecode=1:EF02 --change-name=1:BIOS-BOOT \
  --new=2:0:+128MiB --typecode=2:EF00 --change-name=2:EFI \
  --new=3:0:+2176MiB --typecode=3:8300 --change-name=3:PHOTOBOOT \
  --new=4:0:+1024MiB --typecode=4:0700 --change-name=4:PHOTOBOOTH \
  --new=5:0:0 --typecode=5:8300 --change-name=5:persistence \
  "$output_path"

loop_device="$(losetup --find --show --partscan "$output_path")"

mkfs.vfat -F 32 -n EFI "${loop_device}p2"
mkfs.ext4 -F -L PHOTOBOOT "${loop_device}p3"
mkfs.vfat -F 32 -n PHOTOBOOTH "${loop_device}p4"
mkfs.ext4 -F -L persistence "${loop_device}p5"

mount "${loop_device}p3" "$work_root/live"
mounted_live=true
mkdir -p "$work_root/live/boot/efi"
mount "${loop_device}p2" "$work_root/live/boot/efi"
mounted_efi=true
mount "${loop_device}p4" "$work_root/data"
mounted_data=true
mount -o loop,ro "$iso_path" "$work_root/iso"
mounted_iso=true

cp -a "$work_root/iso/." "$work_root/live/"
mkdir -p "$work_root/live/boot/grub"
cat > "$work_root/live/boot/grub/grub.cfg" <<'EOF'
set default=0
set timeout=1

menuentry "PhotoBooth" {
    search --no-floppy --label PHOTOBOOT --set=root
    linux /live/vmlinuz boot=live components persistence quiet splash locales=ru_RU.UTF-8 keyboard-layouts=ru username=user hostname=photobooth user-default-groups=audio,video,plugdev,netdev,lp,lpadmin,scanner
    initrd /live/initrd.img
}
EOF

grub-install --target=i386-pc --boot-directory="$work_root/live/boot" \
  --recheck "$loop_device"
grub-install --target=x86_64-efi \
  --efi-directory="$work_root/live/boot/efi" \
  --boot-directory="$work_root/live/boot" \
  --removable --no-nvram --recheck

mkdir -p "$work_root/data/Templates" \
  "$work_root/data/Output" \
  "$work_root/data/Diagnostics" \
  "$work_root/data/Updates/incoming" \
  "$work_root/data/Updates/current" \
  "$work_root/data/Updates/backups" \
  "$work_root/data/Updates/installed" \
  "$work_root/data/Updates/logs"

if [[ -n "$templates_dir" && -d "$templates_dir" ]]; then
  cp -a "$templates_dir/." "$work_root/data/Templates/"
fi

cat > "$work_root/data/.photobooth-volume" <<EOF
PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1
volume_id=$(cat /proc/sys/kernel/random/uuid)
EOF
cat > "$work_root/data/КАК_ДОБАВИТЬ_РАМКИ.txt" <<'EOF'
Скопируйте все рамки прямо в общую папку Templates.
Каждая рамка состоит из двух файлов: PNG и JSON.
Имена файлов одной пары до расширения должны полностью совпадать.

Правильно:
Templates/1.png
Templates/1.json
Templates/2.png
Templates/2.json

После копирования безопасно извлеките том PHOTOBOOTH.
Новые рамки загрузятся при следующем запуске фотобудки.
EOF
mount "${loop_device}p5" "$work_root/persistence"
mounted_persistence=true
printf '/ union\n' > "$work_root/persistence/persistence.conf"
umount "$work_root/persistence"
mounted_persistence=false

sync
umount "$work_root/iso"
mounted_iso=false
umount "$work_root/live/boot/efi"
mounted_efi=false
umount "$work_root/data"
mounted_data=false
umount "$work_root/live"
mounted_live=false
losetup -d "$loop_device"
loop_device=""

echo "Ready USB image: $output_path"
