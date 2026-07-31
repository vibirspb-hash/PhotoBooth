#!/usr/bin/env bash
set -u

echo "=== Canon ==="
if command -v gphoto2 >/dev/null 2>&1; then
  gphoto2 --auto-detect
else
  echo "gphoto2 не установлен"
fi

echo
echo "=== CUPS ==="
if command -v lpstat >/dev/null 2>&1; then
  lpstat -r
  lpstat -p
else
  echo "cups-client не установлен"
fi

echo
echo "=== DNP USB ==="
if command -v lpinfo >/dev/null 2>&1; then
  lpinfo -v | grep -Ei 'dnp|rx1|gutenprint' || echo "DNP не найден"
else
  echo "lpinfo не установлен"
fi
