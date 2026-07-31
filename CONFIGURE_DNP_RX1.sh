#!/usr/bin/env bash
set -euo pipefail

queue_name="DNP_RX1"
printer_uri="$(lpinfo -v 2>/dev/null | awk 'tolower($0) ~ /(dnp|ds-?rx1|dsrx1)/ {print $2; exit}')"
driver_name="$(lpinfo -m 2>/dev/null | awk 'tolower($0) ~ /(ds-?rx1|dsrx1)/ {print $1; exit}')"

if [[ -z "$printer_uri" ]]; then
  echo "DNP DS-RX1 не найден по USB. Включите принтер и проверьте кабель."
  exit 1
fi

if [[ -z "$driver_name" ]]; then
  echo "Драйвер DS-RX1 не найден. Проверьте пакет printer-driver-gutenprint."
  exit 1
fi

sudo lpadmin \
  -p "$queue_name" \
  -E \
  -v "$printer_uri" \
  -m "$driver_name"
sudo lpadmin -d "$queue_name"
sudo cupsenable "$queue_name"
sudo cupsaccept "$queue_name"

echo "Очередь $queue_name создана."
lpstat -p "$queue_name" -l
echo
echo "Доступные размеры бумаги:"
lpoptions -p "$queue_name" -l | grep -Ei 'PageSize|MediaSize' || true
