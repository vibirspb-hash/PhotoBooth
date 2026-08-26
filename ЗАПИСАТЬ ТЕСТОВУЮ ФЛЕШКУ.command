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
for utility in diskutil plutil shasum stat; do
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
grep -Eq 'Internal:[[:space:]]+No|Device Location:[[:space:]]+External' <<<"$disk_info" || fail "Защита остановила запись: выбранный диск не внешний."
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

# macOS automatically mounts PHOTOBOOTH and writes Spotlight/FSEvents metadata
# to its FAT32 filesystem. Verify every other byte of the image, then validate
# the writable volume separately so those expected changes do not cause a
# false checksum failure.
data_device="/dev/${disk_id}s4"
for _ in {1..10}; do
  diskutil info "$data_device" >/dev/null 2>&1 && break
  sleep 1
done
diskutil info "$data_device" >/dev/null 2>&1 || fail "После записи не найден раздел PHOTOBOOTH."

data_plist="$(diskutil info -plist "$data_device")"
data_offset="$(plutil -extract PartitionMapPartitionOffset raw -o - - <<<"$data_plist")"
data_size="$(plutil -extract Size raw -o - - <<<"$data_plist")"
mib=1048576
[[ "$data_offset" =~ ^[0-9]+$ && "$data_size" =~ ^[0-9]+$ ]] || fail "Не удалось прочитать границы PHOTOBOOTH."
(( iso_size % mib == 0 && data_offset % mib == 0 && data_size % mib == 0 )) || fail "Границы раздела не выровнены для безопасной проверки."

leading_blocks=$(( data_offset / mib ))
trailing_skip=$(( (data_offset + data_size) / mib ))
total_blocks=$(( iso_size / mib ))
trailing_blocks=$(( total_blocks - trailing_skip ))
(( leading_blocks > 0 && trailing_blocks > 0 )) || fail "Некорректные границы проверяемых областей."

diskutil unmountDisk "/dev/$disk_id" >/dev/null || fail "Не удалось отключить разделы перед проверкой."
image_stable_sha="$({
  dd if="$iso_path" bs=1m count="$leading_blocks" 2>/dev/null
  dd if="$iso_path" bs=1m skip="$trailing_skip" count="$trailing_blocks" 2>/dev/null
} | shasum -a 256 | awk '{print $1}')"
written_stable_sha="$({
  sudo dd if="/dev/r$disk_id" bs=1m count="$leading_blocks" 2>/dev/null
  sudo dd if="/dev/r$disk_id" bs=1m skip="$trailing_skip" count="$trailing_blocks" 2>/dev/null
} | shasum -a 256 | awk '{print $1}')"
[[ "$written_stable_sha" == "$image_stable_sha" ]] || fail "Неизменяемые области флешки не совпали с образом. Не используйте её."

diskutil mount "$data_device" >/dev/null || fail "Не удалось подключить раздел PHOTOBOOTH для проверки."
data_plist="$(diskutil info -plist "$data_device")"
mount_point="$(plutil -extract MountPoint raw -o - - <<<"$data_plist")"
[[ -d "$mount_point/Templates" && -d "$mount_point/Output" && -f "$mount_point/.photobooth-volume" ]] || fail "На PHOTOBOOTH отсутствует обязательная структура папок."

diskutil eject "/dev/$disk_id" >/dev/null || true
echo
echo "ГОТОВО: флешка записана и проверена."
echo "SHA-256 неизменяемых областей совпал: $written_stable_sha"
echo "Раздел PHOTOBOOTH и его папки проверены отдельно."
echo "После повторного подключения Mac должен показать том PHOTOBOOTH."
read -r -p "Нажмите Enter, чтобы закрыть окно..." _ || true
