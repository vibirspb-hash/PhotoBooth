#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "Этот скрипт нужно запускать на Linux-системе фотобудки."
  exit 1
fi

sudo apt-get update
sudo apt-get install -y \
  gphoto2 \
  cups \
  cups-client \
  printer-driver-gutenprint \
  rsync \
  unclutter-xfixes \
  xinput \
  xinput-calibrator

sudo systemctl enable --now cups

current_user="${SUDO_USER:-$USER}"
sudo usermod -aG lp,lpadmin "$current_user"

echo
echo "Драйверы установлены. Проверка Canon:"
gphoto2 --auto-detect || true

echo
echo "USB-устройства печати:"
lpinfo -v 2>/dev/null | grep -Ei 'dnp|rx1|gutenprint' || true

echo
echo "Драйверы DNP в Gutenprint:"
lpinfo -m 2>/dev/null | grep -Ei 'ds-?rx1|dsrx1' | head -n 10 || true

echo
echo "Перезагрузите систему, чтобы применилось членство в группах lp/lpadmin."
echo "После перезагрузки запустите: ./CONFIGURE_DNP_RX1.sh"
