#!/usr/bin/env bash
set -Eeuo pipefail

fail() {
  echo
  echo "ОШИБКА: $*" >&2
  echo
  read -r -p "Нажмите Enter, чтобы закрыть окно..." _ || true
  exit 1
}

[[ "$(uname -s)" == "Darwin" ]] || fail "Команда предназначена только для Mac."
for utility in diskutil shasum stat head; do
  command -v "$utility" >/dev/null 2>&1 || fail "Не найдена системная команда $utility."
done

iso_path="${1:-}"
if [[ -z "$iso_path" ]]; then
  echo
  echo "Перетащите в это окно распакованный USB-образ .img и нажмите Enter:"
  IFS= read -r iso_path
fi
iso_path="${iso_path#\'}"
iso_path="${iso_path%\'}"
iso_path="${iso_path#\"}"
iso_path="${iso_path%\"}"
[[ -f "$iso_path" ]] || fail "Файл образа не найден: $iso_path"
[[ "$iso_path" == *.img ]] || fail "Нужен распакованный файл с расширением .img."

iso_size="$(stat -f '%z' "$iso_path")"
[[ "$iso_size" =~ ^[0-9]+$ && "$iso_size" -gt 0 ]] || fail "Не удалось определить размер образа."
iso_sha="$(shasum -a 256 "$iso_path" | awk '{print $1}')"

echo
echo "USB-образ:       $(basename "$iso_path")"
echo "Размер:           $iso_size байт"
echo "SHA-256:          $iso_sha"
echo
echo "Подключённые внешние физические диски:"
diskutil list external physical
echo
read -r -p "Введите номер ТЕСТОВОЙ флешки (например disk4): " disk_id
[[ "$disk_id" =~ ^disk[0-9]+$ ]] || fail "Неверное имя диска."

disk_info="$(diskutil info "/dev/$disk_id")"
grep -Eq 'Device / Media Name:|Disk Size:' <<<"$disk_info" || fail "Диск /dev/$disk_id не найден."
grep -Eq 'Internal:[[:space:]]+No' <<<"$disk_info" || fail "Защита остановила запись: выбранный диск не внешний."
grep -Eq 'Whole:[[:space:]]+Yes' <<<"$disk_info" || fail "Нужно выбрать весь диск, а не раздел."

echo
echo "БУДЕТ ПОЛНОСТЬЮ СТЁРТ ТОЛЬКО /dev/$disk_id"
grep -E 'Device / Media Name:|Disk Size:|Removable Media:' <<<"$disk_info" || true
echo
read -r -p "Для подтверждения введите: СТЕРЕТЬ $disk_id : " confirmation
[[ "$confirmation" == "СТЕРЕТЬ $disk_id" ]] || fail "Запись отменена."

diskutil unmountDisk "/dev/$disk_id" >/dev/null || fail "Не удалось отключить разделы флешки."
echo
echo "Запись началась. Введите пароль Mac, если система его запросит."
sudo dd if="$iso_path" of="/dev/r$disk_id" bs=4m
sync

blocks=$(( (iso_size + 1048575) / 1048576 ))
set +o pipefail
written_sha="$(sudo dd if="/dev/r$disk_id" bs=1m count="$blocks" 2>/dev/null | head -c "$iso_size" | shasum -a 256 | awk '{print $1}')"
set -o pipefail
[[ "$written_sha" == "$iso_sha" ]] || fail "Контрольная сумма флешки не совпала. Не используйте её."

diskutil eject "/dev/$disk_id" >/dev/null || true
echo
echo "ГОТОВО: флешка записана и проверена."
echo "SHA-256 совпал: $written_sha"
echo "После повторного подключения Mac должен показать том PHOTOBOOTH."
read -r -p "Нажмите Enter, чтобы закрыть окно..." _ || true
