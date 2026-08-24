#!/usr/bin/env bash
set -Eeuo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
gold_tag="dnp-two-strips-working-2026-08-24"

fail() {
  echo
  echo "ОШИБКА: $*" >&2
  echo
  read -r -p "Нажмите Enter, чтобы закрыть окно..." _ || true
  exit 1
}

[[ "$(uname -s)" == "Darwin" ]] || fail "Команда предназначена только для Mac."
command -v git >/dev/null 2>&1 || fail "На Mac не найден git."
[[ -x "$repo_root/SEND_UPDATE_TO_USB.sh" ]] || fail "Не найден механизм обновления PhotoBooth."

git -C "$repo_root" rev-parse -q --verify "refs/tags/$gold_tag" >/dev/null ||
  fail "Не найден защищённый тег золотой версии $gold_tag."
[[ -z "$(git -C "$repo_root" status --porcelain --untracked-files=no)" ]] ||
  fail "Сначала нужно сохранить текущие изменения отдельным коммитом."

gold_sha="$(git -C "$repo_root" rev-parse "$gold_tag^{commit}")"
current_sha="$(git -C "$repo_root" rev-parse HEAD)"
git -C "$repo_root" merge-base --is-ancestor "$gold_sha" "$current_sha" ||
  fail "Текущая версия не является продолжением золотой сборки."

echo
echo "Золотая версия сохранена: $gold_tag (${gold_sha:0:8})"
echo "Тестовая версия:          ${current_sha:0:8}"
echo
echo "Изменения относительно золотой версии:"
git -C "$repo_root" diff --name-only "$gold_sha..$current_sha" | sed 's/^/  - /'
echo
echo "Команда не стирает образ флешки. Она кладёт маленькое обновление"
echo "только на размеченный накопитель PHOTOBOOTH. Перед установкой система"
echo "сама создаст резервную копию изменяемых файлов."
echo
read -r -p "Для продолжения введите ТЕСТ: " answer
[[ "$answer" == "ТЕСТ" ]] || fail "Операция отменена."

"$repo_root/SEND_UPDATE_TO_USB.sh"

echo
echo "Готово. Безопасно извлеките флешку и загрузите Lenovo для проверки."
read -r -p "Нажмите Enter, чтобы закрыть окно..." _ || true
