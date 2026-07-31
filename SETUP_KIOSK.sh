#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "Настройку киоска нужно запускать внутри Debian Live."
  exit 1
fi

if [[ "$EUID" -eq 0 ]]; then
  echo "Запустите скрипт обычным пользователем, без sudo."
  exit 1
fi

source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target_dir="/opt/photobooth"
autostart_dir="$HOME/.config/autostart"

chmod +x "$source_dir"/*.sh
if [[ ! -x "$source_dir/PhotoBooth.Linux" ]]; then
  chmod +x "$source_dir/PhotoBooth.Linux"
fi

"$source_dir/INSTALL_LINUX_HARDWARE.sh"

sudo mkdir -p "$target_dir"
sudo cp -a "$source_dir/." "$target_dir/"
sudo chown -R "$USER":"$(id -gn)" "$target_dir"
chmod +x "$target_dir"/*.sh "$target_dir/PhotoBooth.Linux"

mkdir -p "$autostart_dir"
cat > "$autostart_dir/photobooth.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=PhotoBooth
Exec=$target_dir/START_KIOSK.sh
Terminal=false
OnlyShowIn=XFCE;
X-GNOME-Autostart-enabled=true
StartupNotify=false
EOF

if command -v xfconf-query >/dev/null 2>&1; then
  xfconf-query -c xfce4-power-manager -p /xfce4-power-manager/blank-on-ac -n -t int -s 0 || true
  xfconf-query -c xfce4-power-manager -p /xfce4-power-manager/dpms-enabled -n -t bool -s false || true
  xfconf-query -c xfce4-session -p /shutdown/LockScreen -n -t bool -s false || true
fi

echo
echo "PhotoBooth установлен в $target_dir."
echo "Автозапуск создан для пользователя $USER."
echo "Проверка готовности: $target_dir/VERIFY_KIOSK_BOOT.sh"
echo "Теперь перезагрузите компьютер и снова выберите Live system (persistence)."
