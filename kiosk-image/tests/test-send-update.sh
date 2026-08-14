#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C
export LANG=C

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/photobooth-send-test.XXXXXX")"
test_repo="$test_root/repo"
usb_root="$test_root/PHOTOBOOTH"

cleanup() {
  rm -rf -- "$test_root"
}
trap cleanup EXIT

fail() {
  echo "[TEST ERROR] $*" >&2
  exit 1
}

mkdir -p "$test_repo/kiosk-image" "$usb_root/Updates/incoming"
cp "$repo_root/SEND_UPDATE_TO_USB.sh" "$test_repo/SEND_UPDATE_TO_USB.sh"
printf '#!/usr/bin/env bash\necho old\n' \
  >"$test_repo/kiosk-image/photobooth-set-time"
chmod +x "$test_repo/SEND_UPDATE_TO_USB.sh" \
  "$test_repo/kiosk-image/photobooth-set-time"

git -C "$test_repo" init -q
git -C "$test_repo" config user.name "PhotoBooth Test"
git -C "$test_repo" config user.email "photobooth-test@example.invalid"
git -C "$test_repo" add SEND_UPDATE_TO_USB.sh kiosk-image/photobooth-set-time
git -C "$test_repo" commit -qm "Base"
base_sha="$(git -C "$test_repo" rev-parse HEAD)"

printf '#!/usr/bin/env bash\necho updated\n' \
  >"$test_repo/kiosk-image/photobooth-set-time"
git -C "$test_repo" add kiosk-image/photobooth-set-time
git -C "$test_repo" commit -qm "Update allowed runtime file"
target_sha="$(git -C "$test_repo" rev-parse HEAD)"

printf '%s\nvolume_id=test\n' \
  "PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1" >"$usb_root/.photobooth-volume"
printf '%s\n' "$base_sha" >"$usb_root/Updates/current-version.txt"

sender_output="$(
  cd "$test_repo"
  PHOTOBOOTH_USB_ROOT="$usb_root" ./SEND_UPDATE_TO_USB.sh
)"
grep -Fq "PhotoBooth update prepared successfully." <<<"$sender_output" ||
  fail "Sender did not report success."
grep -Fq "Checksum:" <<<"$sender_output" || fail "Sender omitted checksum status."

patch="$(find "$usb_root/Updates/incoming" -name 'PhotoBooth-Patch-*.tar.gz' -type f -print -quit)"
[[ -f "$patch" && -f "$patch.sha256" ]] || fail "Patch or checksum was not copied."
expected_checksum="$(awk 'NR == 1 {print $1}' "$patch.sha256")"
actual_checksum="$(shasum -a 256 "$patch" | awk '{print $1}')"
[[ "$expected_checksum" == "$actual_checksum" ]] ||
  fail "Patch checksum on fake USB does not match."

manifest="$test_root/manifest.json"
tar -xOf "$patch" manifest.json >"$manifest"
[[ "$(jq -r '.expected_previous_version' "$manifest")" == "$base_sha" ]] ||
  fail "Manifest previous version is wrong."
[[ "$(jq -r '.git_commit' "$manifest")" == "$target_sha" ]] ||
  fail "Manifest target version is wrong."
[[ "$(jq -r '.files[0].destination' "$manifest")" == \
   "/usr/local/sbin/photobooth-set-time" ]] ||
  fail "Manifest destination is wrong."

app_usb="$test_root/PHOTOBOOTH-APP"
fake_dotnet="$test_root/fake-dotnet"
mkdir -p "$app_usb/Updates/incoming" "$app_usb/Updates/current" \
  "$test_repo/PhotoBooth.Linux"
printf '%s\nvolume_id=app-test\n' \
  "PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1" >"$app_usb/.photobooth-volume"
printf '%s\n' "$target_sha" >"$app_usb/Updates/current-version.txt"
old_app="$test_root/old-app"
same_library="$test_root/libsame.so"
printf 'old application\n' >"$old_app"
printf 'same library\n' >"$same_library"
old_app_checksum="$(shasum -a 256 "$old_app" | awk '{print $1}')"
same_checksum="$(shasum -a 256 "$same_library" | awk '{print $1}')"
jq -n --arg old "$old_app_checksum" --arg same "$same_checksum" '[
  {destination: "/opt/photobooth/PhotoBooth.Linux", sha256: $old, mode: "0755"},
  {destination: "/opt/photobooth/libsame.so", sha256: $same, mode: "0644"}
]' >"$app_usb/Updates/current/runtime-files.json"
printf 'application source changed\n' >"$test_repo/PhotoBooth.Linux/Test.cs"
git -C "$test_repo" add PhotoBooth.Linux/Test.cs
git -C "$test_repo" commit -qm "Change application"
app_target_sha="$(git -C "$test_repo" rev-parse HEAD)"

printf '%s\n' \
  '#!/usr/bin/env bash' \
  'set -euo pipefail' \
  'output=""' \
  'while (($#)); do' \
  '  if [[ "$1" == "-o" ]]; then' \
  '    shift' \
  '    output="$1"' \
  '  fi' \
  '  shift' \
  'done' \
  'mkdir -p "$output"' \
  "printf 'new application\\n' >\"\$output/PhotoBooth.Linux\"" \
  "printf 'same library\\n' >\"\$output/libsame.so\"" \
  >"$fake_dotnet"
chmod +x "$fake_dotnet"
(
  cd "$test_repo"
  PHOTOBOOTH_USB_ROOT="$app_usb" PHOTOBOOTH_DOTNET="$fake_dotnet" \
    ./SEND_UPDATE_TO_USB.sh >/dev/null
)
app_patch="$(find "$app_usb/Updates/incoming" -name 'PhotoBooth-Patch-*.tar.gz' -type f -print -quit)"
app_manifest="$test_root/app-manifest.json"
tar -xOf "$app_patch" manifest.json >"$app_manifest"
[[ "$(jq -r '.git_commit' "$app_manifest")" == "$app_target_sha" ]] ||
  fail "Application patch target is wrong."
[[ "$(jq -r '.files | length' "$app_manifest")" == "1" ]] ||
  fail "Application patch included unchanged publish files."
[[ "$(jq -r '.files[0].destination' "$app_manifest")" == \
   "/opt/photobooth/PhotoBooth.Linux" ]] ||
  fail "Application patch omitted the changed executable."

printf 'linux-image-amd64\n' >"$test_repo/kiosk-image/packages.list.chroot"
git -C "$test_repo" add kiosk-image/packages.list.chroot
git -C "$test_repo" commit -qm "Change base image packages"
set +e
full_iso_output="$(
  cd "$test_repo"
  PHOTOBOOTH_USB_ROOT="$usb_root" ./SEND_UPDATE_TO_USB.sh 2>&1
)"
full_iso_code=$?
set -e
[[ "$full_iso_code" -eq 2 ]] || fail "Full ISO change returned $full_iso_code instead of 2."
grep -Fq "FULL ISO REBUILD REQUIRED" <<<"$full_iso_output" ||
  fail "Full ISO change was not rejected clearly."

echo "[TEST OK] macOS sender package, checksum and full-ISO gate checks passed."
