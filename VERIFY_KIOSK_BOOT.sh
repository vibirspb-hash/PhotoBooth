#!/usr/bin/env bash
set -u

app_dir="/opt/photobooth"
errors=0

check() {
  local label="$1"
  shift
  if "$@" >/dev/null 2>&1; then
    printf 'OK   %s\n' "$label"
  else
    printf 'FAIL %s\n' "$label"
    errors=$((errors + 1))
  fi
}

echo "PhotoBooth: проверка загрузочной системы"
echo "Режим загрузки: $([[ -d /sys/firmware/efi ]] && echo UEFI || echo BIOS/Legacy)"
echo

check "Приложение установлено" test -x "$app_dir/PhotoBooth.Linux"
check "Скрипт запуска установлен" test -x "$app_dir/START_KIOSK.sh"
check "Автозапуск пользователя создан" test -f "$HOME/.config/autostart/photobooth.desktop"
check "gPhoto2 установлен" command -v gphoto2
check "CUPS установлен" command -v lpstat
check "Служба печати запущена" systemctl is-active --quiet cups

if findmnt -rn -o FSTYPE / 2>/dev/null | grep -Eq 'overlay|aufs'; then
  echo "OK   Debian Live запущен с overlay-файловой системой"
else
  echo "INFO Корневая система не похожа на Live overlay. Для обычной установки это нормально."
fi

echo
if [[ "$errors" -eq 0 ]]; then
  echo "Система готова к автоматическому запуску PhotoBooth."
  exit 0
fi

echo "Найдено проблем: $errors. Повторите SETUP_KIOSK.sh или проверьте отмеченные пункты."
exit 1
