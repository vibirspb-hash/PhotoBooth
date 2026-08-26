#!/usr/bin/env bash
set -euo pipefail

image_path="${1:?Usage: VERIFY_USB_IMAGE.sh IMAGE}"
work_root="$(mktemp -d --tmpdir photobooth-usb-verify.XXXXXX)"
loop_device=""
map_prefix=""
mapped=false
mounted=false

cleanup() {
  set +e
  $mounted && umount "$work_root"
  $mapped && kpartx -d "$loop_device"
  [[ -z "$loop_device" ]] || losetup -d "$loop_device"
  rm -rf "$work_root"
}
trap cleanup EXIT

loop_device="$(losetup --find --show --read-only "$image_path")"
kpartx -as "$loop_device"
mapped=true
map_prefix="/dev/mapper/$(basename "$loop_device")p"

[[ "$(blkid -s TYPE -o value "${map_prefix}2")" == "vfat" ]]
[[ "$(blkid -s LABEL -o value "${map_prefix}3")" == "PHOTOBOOT" ]]
[[ "$(blkid -s TYPE -o value "${map_prefix}4")" == "vfat" ]]
[[ "$(blkid -s LABEL -o value "${map_prefix}4")" == "PHOTOBOOTH" ]]
[[ "$(blkid -s LABEL -o value "${map_prefix}5")" == "persistence" ]]

mount -o ro "${map_prefix}4" "$work_root"
mounted=true
grep -Fxq 'PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1' "$work_root/.photobooth-volume"
[[ -d "$work_root/Templates" && -d "$work_root/Output" && -d "$work_root/Diagnostics" ]]
umount "$work_root"
mounted=false

echo "Verified GPT USB image and Mac-compatible PHOTOBOOTH volume."
