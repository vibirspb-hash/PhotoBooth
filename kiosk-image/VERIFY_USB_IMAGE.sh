#!/usr/bin/env bash
set -euo pipefail

image_path="${1:?Usage: VERIFY_USB_IMAGE.sh IMAGE}"
work_root="$(mktemp -d --tmpdir photobooth-usb-verify.XXXXXX)"
loop_device=""
mounted=false

cleanup() {
  set +e
  $mounted && umount "$work_root"
  [[ -z "$loop_device" ]] || losetup -d "$loop_device"
  rm -rf "$work_root"
}
trap cleanup EXIT

loop_device="$(losetup --find --show --partscan --read-only "$image_path")"
udevadm settle

[[ "$(blkid -s TYPE -o value "${loop_device}p2")" == "vfat" ]]
[[ "$(blkid -s LABEL -o value "${loop_device}p3")" == "PHOTOBOOT" ]]
[[ "$(blkid -s TYPE -o value "${loop_device}p4")" == "vfat" ]]
[[ "$(blkid -s LABEL -o value "${loop_device}p4")" == "PHOTOBOOTH" ]]
[[ "$(blkid -s LABEL -o value "${loop_device}p5")" == "persistence" ]]

mount -o ro "${loop_device}p4" "$work_root"
mounted=true
grep -Fxq 'PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1' "$work_root/.photobooth-volume"
[[ -d "$work_root/Templates" && -d "$work_root/Output" && -d "$work_root/Diagnostics" ]]
umount "$work_root"
mounted=false

echo "Verified GPT USB image and Mac-compatible PHOTOBOOTH volume."
