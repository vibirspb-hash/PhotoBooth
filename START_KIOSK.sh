#!/usr/bin/env bash
set -u

app_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if command -v xset >/dev/null 2>&1; then
  xset s off
  xset s noblank
  xset -dpms
fi

if command -v unclutter >/dev/null 2>&1; then
  unclutter --timeout 0 --hide-on-touch --start-hidden --fork
fi

sleep 3
media_root="/media/$USER"
data_root="$media_root/PHOTOBOOTH"
template_source=""
export PHOTOBOOTH_STORAGE_REQUIRED=1

if ! mountpoint -q "$data_root"; then
  sudo -n /usr/local/sbin/photobooth-first-boot || true
fi

if mountpoint -q "$data_root"; then
  mkdir -p "$data_root/Output" "$data_root/Templates" "$data_root/Diagnostics"
  export PHOTOBOOTH_DATA_ROOT="$data_root"
fi

/usr/local/bin/photobooth-touch-setup || true
sudo -n /usr/local/sbin/photobooth-printer-setup || true
/usr/local/bin/photobooth-hardware-diagnostics || true

if [[ -d "$media_root" ]]; then
  own_templates="$data_root/Templates"
  if [[ -n "$(find "$own_templates" -maxdepth 1 -type f -iname '*.json' -print -quit 2>/dev/null)" ]] ||
      [[ -n "$(find "$own_templates" -mindepth 1 -maxdepth 1 -type d -print -quit 2>/dev/null)" ]]; then
    template_source="$own_templates"
  else
    template_source="$(find "$media_root" -mindepth 2 -maxdepth 2 -type d -iname Templates ! -path "$own_templates" -print -quit 2>/dev/null)"
  fi
fi

if [[ -n "$template_source" ]]; then
  mkdir -p "$app_dir/Templates"
  rsync -a --delete "$template_source/" "$app_dir/Templates/"
fi

cd "$app_dir"
mkdir -p "$app_dir/logs"

while true; do
  "$app_dir/PhotoBooth.Linux" >> "$app_dir/logs/kiosk.log" 2>&1
  exit_code=$?

  # A normal exit is intentional (for example, from the protected settings).
  if [[ "$exit_code" -eq 0 ]]; then
    exit 0
  fi

  echo "$(date --iso-8601=seconds) PhotoBooth завершился с кодом $exit_code. Перезапуск через 3 секунды." \
    >> "$app_dir/logs/kiosk.log"
  sleep 3
done
