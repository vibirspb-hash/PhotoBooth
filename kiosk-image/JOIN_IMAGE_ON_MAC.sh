#!/usr/bin/env bash
set -euo pipefail

parts_dir="${1:-.}"
output_path="${2:-PhotoBooth-Kiosk-amd64.iso}"

if ! compgen -G "$parts_dir/PhotoBooth-Kiosk-amd64.iso.part-*" >/dev/null; then
  echo "Image parts were not found in $parts_dir"
  exit 1
fi

cat "$parts_dir"/PhotoBooth-Kiosk-amd64.iso.part-* > "$output_path"

if [[ -f "$parts_dir/PhotoBooth-Kiosk-amd64.iso.sha256" ]]; then
  expected="$(awk '{print $1}' "$parts_dir/PhotoBooth-Kiosk-amd64.iso.sha256")"
  actual="$(env LC_ALL=C shasum -a 256 "$output_path" | awk '{print $1}')"
  if [[ "$expected" != "$actual" ]]; then
    echo "Checksum mismatch. The image is incomplete."
    exit 1
  fi
fi

echo "Ready: $output_path"
