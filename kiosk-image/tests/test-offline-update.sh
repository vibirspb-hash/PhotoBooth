#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C
export LANG=C

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
updater="$repo_root/kiosk-image/photobooth-update"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/photobooth-update-test.XXXXXX")"
data_root="$test_root/PHOTOBOOTH"
install_root="$test_root/root"
base_file="$test_root/base-version.txt"
test_bin="$test_root/bin"
base_sha="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
target_sha="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
next_sha="cccccccccccccccccccccccccccccccccccccccc"

cleanup() {
  rm -rf -- "$test_root"
}
trap cleanup EXIT

fail() {
  echo "[TEST ERROR] $*" >&2
  exit 1
}

assert_file_contains() {
  local file="$1"
  local expected="$2"
  grep -Fq "$expected" "$file" ||
    fail "$file does not contain: $expected"
}

sha256_file() {
  sha256sum "$1" | awk '{print $1}'
}

write_manifest() {
  local manifest="$1"
  local previous="$2"
  local target="$3"
  local entries="$4"
  jq -n \
    --arg previous "$previous" \
    --arg target "$target" \
    --argjson files "$entries" \
    '{
      format_version: 1,
      patch_version: $target[0:8],
      git_commit: $target,
      created_at: "2026-08-14T12:00:00Z",
      expected_previous_version: $previous,
      restart_required: false,
      reboot_required: true,
      components: ["Touchscreen"],
      files: $files
    }' >"$manifest"
}

package_patch() {
  local work="$1"
  local output="$2"
  tar -C "$work" -czf "$output" manifest.json files
  printf '%s  %s\n' "$(sha256_file "$output")" "$(basename "$output")" \
    >"$output.sha256"
}

run_updater() {
  PHOTOBOOTH_DATA_ROOT="$data_root" \
  PHOTOBOOTH_BASE_VERSION_FILE="$base_file" \
  PHOTOBOOTH_UPDATE_INSTALL_ROOT="$install_root" \
  PHOTOBOOTH_UPDATE_LOCK_FILE="$test_root/update.lock" \
  PHOTOBOOTH_UPDATE_ALLOW_UNMOUNTED=1 \
  PHOTOBOOTH_UPDATE_TEST_PATH="$test_bin" \
  PHOTOBOOTH_UPDATE_TEST_NO_TEE=1 \
    bash "$updater" "$@"
}

make_single_file_patch() {
  local work="$1"
  local output="$2"
  local previous="$3"
  local target="$4"
  local destination="$5"
  local content="$6"
  mkdir -p "$work/files"
  printf '%s\n' "$content" >"$work/files/0001"
  local checksum entries
  checksum="$(sha256_file "$work/files/0001")"
  entries="$(jq -n \
    --arg checksum "$checksum" \
    --arg destination "$destination" \
    '[{
      source: "files/0001",
      destination: $destination,
      type: "shell",
      sha256: $checksum,
      mode: "0755",
      owner: "root",
      group: "root"
    }]')"
  write_manifest "$work/manifest.json" "$previous" "$target" "$entries"
  package_patch "$work" "$output"
}

mkdir -p "$data_root/Updates/incoming" "$install_root/usr/local/bin" "$test_bin"
printf '%s\n' "$base_sha" >"$base_file"
printf '%s\nvolume_id=test\n' \
  "PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1" >"$data_root/.photobooth-volume"
printf '#!/usr/bin/env bash\nexit 0\n' >"$test_bin/flock"
chmod +x "$test_bin/flock"
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'set -euo pipefail' \
  'last=""' \
  'for argument in "$@"; do last="$argument"; done' \
  'if [[ -n "${PHOTOBOOTH_TEST_FAIL_MV_TARGET:-}" && "$last" == "$PHOTOBOOTH_TEST_FAIL_MV_TARGET" ]]; then' \
  '  exit 72' \
  'fi' \
  'exec /bin/mv "$@"' \
  >"$test_bin/mv"
chmod +x "$test_bin/mv"
printf '#!/usr/bin/env bash\necho original\n' \
  >"$install_root/usr/local/bin/photobooth-touch-setup"
chmod 0755 "$install_root/usr/local/bin/photobooth-touch-setup"

valid_work="$test_root/valid"
valid_patch="$data_root/Updates/incoming/PhotoBooth-Patch-valid.tar.gz"
make_single_file_patch "$valid_work" "$valid_patch" "$base_sha" "$target_sha" \
  "/usr/local/bin/photobooth-touch-setup" \
  $'#!/usr/bin/env bash\necho updated'

dry_run_output="$(run_updater --dry-run "$valid_patch")"
grep -Fq "Patch valid: YES" <<<"$dry_run_output" || fail "Dry-run failed."
grep -Fq "No files were changed." <<<"$dry_run_output" || fail "Dry-run mutated state."
assert_file_contains "$install_root/usr/local/bin/photobooth-touch-setup" "original"

run_updater --install "$valid_patch"
assert_file_contains "$install_root/usr/local/bin/photobooth-touch-setup" "updated"
[[ "$(head -n 1 "$data_root/Updates/current-version.txt")" == "$target_sha" ]] ||
  fail "Installed version was not recorded."
find "$data_root/Updates/installed" -name 'PhotoBooth-Patch-valid.tar.gz' -type f \
  -print -quit | grep -q . || fail "Installed patch was not archived."
find "$data_root/Updates/backups" -name backup-manifest.json -type f \
  -print -quit | grep -q . || fail "Backup was not created."

run_updater --mark-known-good
[[ "$(head -n 1 "$data_root/Updates/known-good-version.txt")" == "$target_sha" ]] ||
  fail "Known-good version was not recorded."

run_updater --rollback
assert_file_contains "$install_root/usr/local/bin/photobooth-touch-setup" "original"
[[ "$(head -n 1 "$data_root/Updates/current-version.txt")" == "$base_sha" ]] ||
  fail "Rollback did not restore the previous version."

run_updater --mark-known-good
chain_b_work="$test_root/chain-b"
chain_b_patch="$data_root/Updates/incoming/PhotoBooth-Patch-chain-b.tar.gz"
make_single_file_patch "$chain_b_work" "$chain_b_patch" "$base_sha" "$target_sha" \
  "/usr/local/bin/photobooth-touch-setup" \
  $'#!/usr/bin/env bash\necho chain-b'
run_updater --install "$chain_b_patch"

chain_c_work="$test_root/chain-c"
chain_c_patch="$data_root/Updates/incoming/PhotoBooth-Patch-chain-c.tar.gz"
make_single_file_patch "$chain_c_work" "$chain_c_patch" "$target_sha" "$next_sha" \
  "/usr/local/bin/photobooth-touch-setup" \
  $'#!/usr/bin/env bash\necho chain-c'
run_updater --install "$chain_c_patch"
run_updater --rollback-known-good
assert_file_contains "$install_root/usr/local/bin/photobooth-touch-setup" "original"
[[ "$(head -n 1 "$data_root/Updates/current-version.txt")" == "$base_sha" ]] ||
  fail "Known-good rollback did not restore the marked version."

bad_checksum_work="$test_root/bad-checksum"
bad_checksum_patch="$data_root/Updates/incoming/PhotoBooth-Patch-bad-checksum.tar.gz"
make_single_file_patch "$bad_checksum_work" "$bad_checksum_patch" "$base_sha" \
  "$target_sha" "/usr/local/bin/photobooth-touch-setup" \
  $'#!/usr/bin/env bash\necho checksum'
printf '%064d  %s\n' 0 "$(basename "$bad_checksum_patch")" \
  >"$bad_checksum_patch.sha256"
if run_updater --dry-run "$bad_checksum_patch" >/dev/null 2>&1; then
  fail "Invalid package checksum was accepted."
fi

bad_path_work="$test_root/bad-path"
bad_path_patch="$data_root/Updates/incoming/PhotoBooth-Patch-bad-path.tar.gz"
make_single_file_patch "$bad_path_work" "$bad_path_patch" "$base_sha" "$target_sha" \
  "/etc/passwd" $'#!/usr/bin/env bash\necho unsafe'
if run_updater --dry-run "$bad_path_patch" >/dev/null 2>&1; then
  fail "Non-whitelisted destination was accepted."
fi

bad_shell_work="$test_root/bad-shell"
bad_shell_patch="$data_root/Updates/incoming/PhotoBooth-Patch-bad-shell.tar.gz"
make_single_file_patch "$bad_shell_work" "$bad_shell_patch" "$base_sha" "$target_sha" \
  "/usr/local/bin/photobooth-touch-setup" $'#!/usr/bin/env bash\nif then'
if run_updater --dry-run "$bad_shell_patch" >/dev/null 2>&1; then
  fail "Invalid shell syntax was accepted."
fi

transaction_work="$test_root/transaction"
transaction_patch="$data_root/Updates/incoming/PhotoBooth-Patch-transaction.tar.gz"
mkdir -p "$transaction_work/files"
printf '#!/usr/bin/env bash\necho partial\n' >"$transaction_work/files/0001"
printf '[Unit]\nDescription=Transaction test\n' >"$transaction_work/files/0002"
checksum_one="$(sha256_file "$transaction_work/files/0001")"
checksum_two="$(sha256_file "$transaction_work/files/0002")"
transaction_entries="$(jq -n \
  --arg one "$checksum_one" --arg two "$checksum_two" '[
    {
      source: "files/0001",
      destination: "/usr/local/bin/photobooth-touch-setup",
      type: "shell", sha256: $one, mode: "0755", owner: "root", group: "root"
    },
    {
      source: "files/0002",
      destination: "/etc/systemd/system/photobooth-update-check.service",
      type: "systemd", sha256: $two, mode: "0644", owner: "root", group: "root"
    }
  ]')"
write_manifest "$transaction_work/manifest.json" "$base_sha" "$next_sha" \
  "$transaction_entries"
package_patch "$transaction_work" "$transaction_patch"

if PHOTOBOOTH_TEST_FAIL_MV_TARGET="$install_root/etc/systemd/system/photobooth-update-check.service" \
    run_updater --install "$transaction_patch" >/dev/null 2>&1; then
  fail "Simulated partial installation unexpectedly succeeded."
fi
assert_file_contains "$install_root/usr/local/bin/photobooth-touch-setup" "original"
[[ "$(head -n 1 "$data_root/Updates/current-version.txt")" == "$base_sha" ]] ||
  fail "Failed transaction changed current-version."
[[ -f "$transaction_patch" ]] || fail "Failed patch was not left in incoming."
assert_file_contains "$data_root/Updates/current/last-status.txt" "FAILED"

recovery_backup="$(
  sed -E 's/^.* backup=([^ ]+) exit=.*$/\1/' \
    "$data_root/Updates/current/last-status.txt"
)"
[[ -f "$recovery_backup/backup-manifest.json" ]] ||
  fail "Recovery test backup was not found."
recovery_backup_id="$(basename "$recovery_backup")"
mkdir -p "$install_root/etc/systemd/system"
mkdir -p "$install_root/opt/photobooth/Templates"
printf 'managed runtime\n' >"$install_root/opt/photobooth/PhotoBooth.Linux.dll"
printf 'operator settings\n' >"$install_root/opt/photobooth/config.json"
printf 'event template\n' >"$install_root/opt/photobooth/Templates/event.json"
printf '#!/usr/bin/env bash\necho interrupted\n' \
  >"$install_root/usr/local/bin/photobooth-touch-setup"
printf '%s\n' "$next_sha" >"$data_root/Updates/current-version.txt"
jq -n \
  --arg backup_id "$recovery_backup_id" \
  --arg from_version "$base_sha" \
  --arg target_version "$next_sha" \
  '{format_version: 1, state: "installing", backup_id: $backup_id,
    from_version: $from_version, target_version: $target_version,
    patch_name: "PhotoBooth-Patch-transaction.tar.gz",
    started_at: "2026-08-14T12:00:00Z"}' \
  >"$data_root/Updates/current/transaction.json"
run_updater --boot-check
assert_file_contains "$install_root/usr/local/bin/photobooth-touch-setup" "original"
[[ "$(head -n 1 "$data_root/Updates/current-version.txt")" == "$base_sha" ]] ||
  fail "Boot recovery did not restore current-version."
[[ ! -f "$data_root/Updates/current/transaction.json" ]] ||
  fail "Boot recovery did not remove the completed journal."
[[ "$(jq -r 'length' "$data_root/Updates/current/runtime-files.json")" == "1" ]] ||
  fail "Runtime inventory included mutable application data."
[[ "$(jq -r '.[0].destination' "$data_root/Updates/current/runtime-files.json")" == \
   "/opt/photobooth/PhotoBooth.Linux.dll" ]] ||
  fail "Runtime inventory omitted the managed application file."

echo "[TEST OK] Offline update dry-run, install, rollback and fail-safe checks passed."
