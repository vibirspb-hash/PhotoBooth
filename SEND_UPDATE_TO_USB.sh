#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C
export LANG=C

readonly marker_signature="PHOTOBOOTH_OFFLINE_UPDATE_VOLUME_V1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
target_sha="$(git -C "$repo_root" rev-parse HEAD)"
short_sha="${target_sha:0:8}"
staging_root=""
runtime_inventory_file=""

cleanup() {
  if [[ -n "$staging_root" && -d "$staging_root" ]]; then
    rm -rf -- "$staging_root"
  fi
}
trap cleanup EXIT

fail() {
  echo "[ERROR] $*" >&2
  exit 1
}

full_iso_required() {
  echo "FULL ISO REBUILD REQUIRED" >&2
  if [[ $# -gt 0 ]]; then
    printf '%s\n' "$@" >&2
  fi
  exit 2
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command is missing: $1"
}

find_photobooth_usb() {
  if [[ -n "${PHOTOBOOTH_USB_ROOT:-}" ]]; then
    [[ -d "$PHOTOBOOTH_USB_ROOT" ]] || return 1
    printf '%s\n' "$PHOTOBOOTH_USB_ROOT"
    return 0
  fi

  local candidate marker found
  found=""
  for candidate in /Volumes/*; do
    [[ -d "$candidate" && -f "$candidate/.photobooth-volume" ]] || continue
    marker="$(head -n 1 "$candidate/.photobooth-volume" | tr -d '\r\n')"
    [[ "$marker" == "$marker_signature" ]] || continue
    if [[ -n "$found" ]]; then
      fail "More than one marked PHOTOBOOTH USB volume was found."
    fi
    found="$candidate"
  done
  [[ -n "$found" ]] || return 1
  printf '%s\n' "$found"
}

is_application_source() {
  case "$1" in
    PhotoBooth.Linux/*|PhotoBooth/*|START_KIOSK.sh|\
    INSTALL_LINUX_HARDWARE.sh|CONFIGURE_DNP_RX1.sh|\
    CHECK_LINUX_HARDWARE.sh|SETUP_KIOSK.sh|VERIFY_KIOSK_BOOT.sh)
      return 0
      ;;
    *) return 1 ;;
  esac
}

is_source_only_file() {
  case "$1" in
    *.md|.gitignore|.gitattributes|SEND_UPDATE_TO_USB.sh|\
    kiosk-image/OFFLINE_UPDATES.md|kiosk-image/tests/*)
      return 0
      ;;
    *) return 1 ;;
  esac
}

is_full_iso_source() {
  case "$1" in
    .github/workflows/build-kiosk-iso.yml|\
    kiosk-image/BUILD_KIOSK_ISO.sh|\
    kiosk-image/JOIN_IMAGE_ON_MAC.sh|\
    kiosk-image/VERIFY_PARTITION_LAYOUT.sh|\
    kiosk-image/packages.list.chroot|\
    kiosk-image/photobooth-first-boot|\
    kiosk-image/photobooth-persistence.service|\
    kiosk-image/lightdm-photobooth.conf|\
    kiosk-image/openbox-autostart|\
    kiosk-image/DNP-DSRX1.ppd|\
    kiosk-image/test-print.jpg)
      return 0
      ;;
    PhotoBooth.Linux/config.json|PhotoBooth/Templates/*)
      return 0
      ;;
    *) return 1 ;;
  esac
}

is_mutable_published_path() {
  case "$1" in
    config.json|Templates|Templates/*|Output|Output/*|logs|logs/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

direct_mapping() {
  local source="$1"
  MAP_DESTINATION=""
  MAP_TYPE=""
  MAP_MODE=""
  MAP_OWNER="root"
  MAP_GROUP="root"
  MAP_COMPONENT=""
  MAP_RESTART=false
  MAP_REBOOT=false

  case "$source" in
    kiosk-image/photobooth-touch-setup)
      MAP_DESTINATION="/usr/local/bin/photobooth-touch-setup"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Touchscreen"; MAP_REBOOT=true
      ;;
    kiosk-image/photobooth-touch-calibrate)
      MAP_DESTINATION="/usr/local/bin/photobooth-touch-calibrate"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Touchscreen"
      ;;
    kiosk-image/photobooth-hardware-diagnostics)
      MAP_DESTINATION="/usr/local/bin/photobooth-hardware-diagnostics"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Diagnostics"
      ;;
    kiosk-image/photobooth-printer-setup)
      MAP_DESTINATION="/usr/local/sbin/photobooth-printer-setup"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Printer"; MAP_REBOOT=true
      ;;
    kiosk-image/photobooth-install-touch-calibration)
      MAP_DESTINATION="/usr/local/sbin/photobooth-install-touch-calibration"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Touchscreen"
      ;;
    kiosk-image/test-dnp-print.sh)
      MAP_DESTINATION="/usr/local/sbin/test-dnp-print"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Printer"
      ;;
    kiosk-image/photobooth-schedule-poweroff)
      MAP_DESTINATION="/usr/local/sbin/photobooth-schedule-poweroff"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="System"
      ;;
    kiosk-image/photobooth-set-time)
      MAP_DESTINATION="/usr/local/sbin/photobooth-set-time"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="System"
      ;;
    kiosk-image/photobooth-update)
      MAP_DESTINATION="/usr/local/sbin/photobooth-update"
      MAP_TYPE="shell"; MAP_MODE="0755"; MAP_COMPONENT="Updater"; MAP_REBOOT=true
      ;;
    kiosk-image/99-keetouch-calibration.conf)
      MAP_DESTINATION="/etc/X11/xorg.conf.d/99-keetouch-calibration.conf"
      MAP_TYPE="file"; MAP_MODE="0644"; MAP_COMPONENT="Touchscreen"; MAP_REBOOT=true
      ;;
    kiosk-image/photobooth-printer-setup.service)
      MAP_DESTINATION="/etc/systemd/system/photobooth-printer-setup.service"
      MAP_TYPE="systemd"; MAP_MODE="0644"; MAP_COMPONENT="Printer"; MAP_REBOOT=true
      ;;
    kiosk-image/photobooth-update-check.service)
      MAP_DESTINATION="/etc/systemd/system/photobooth-update-check.service"
      MAP_TYPE="systemd"; MAP_MODE="0644"; MAP_COMPONENT="Updater"; MAP_REBOOT=true
      ;;
    *)
      return 1
      ;;
  esac
}

add_payload() {
  local source_file="$1"
  local destination="$2"
  local type="$3"
  local mode="$4"
  local owner="$5"
  local group="$6"
  local component="$7"
  local payload_name checksum

  [[ -f "$source_file" && ! -L "$source_file" ]] ||
    fail "Payload source is not a regular file: $source_file"
  payload_index=$((payload_index + 1))
  payload_name="files/$(printf '%04d' "$payload_index")"
  cp "$source_file" "$staging_root/$payload_name"
  checksum="$(shasum -a 256 "$staging_root/$payload_name" | awk '{print $1}')"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' \
    "$payload_name" "$destination" "$type" "$checksum" \
    "$mode" "$owner" "$group" "$component" >>"$entries_file"
}

publish_application() {
  local publish_dir="$staging_root/published-app"
  local dotnet_command relative mode type destination checksum current_entry
  local current_inventory target_destinations
  [[ -f "$runtime_inventory_file" ]] ||
    fail "Runtime inventory is missing on the USB. Boot the new base image once before sending an application patch."
  current_inventory="$staging_root/current-app.tsv"
  target_destinations="$staging_root/target-app-destinations.txt"
  : >"$target_destinations"
  python3 - "$runtime_inventory_file" "$current_inventory" <<'PY'
import json
import re
import sys

source_path, output_path = sys.argv[1:]
with open(source_path, encoding="utf-8") as source:
    entries = json.load(source)
if not isinstance(entries, list):
    raise SystemExit("Runtime inventory must be a JSON array.")
seen = set()
with open(output_path, "w", encoding="utf-8") as output:
    for entry in entries:
        if not isinstance(entry, dict):
            raise SystemExit("Invalid runtime inventory entry.")
        destination = entry.get("destination", "")
        checksum = entry.get("sha256", "")
        mode = entry.get("mode", "")
        if (not re.fullmatch(r"/opt/photobooth/[A-Za-z0-9._+/-]+", destination)
                or ".." in destination or "//" in destination
                or not re.fullmatch(r"[0-9a-f]{64}", checksum)
                or mode not in {"0644", "0755"}
                or destination in seen):
            raise SystemExit(f"Invalid runtime inventory entry: {destination}")
        seen.add(destination)
        output.write(f"{destination}\t{checksum}\t{mode}\n")
PY
  dotnet_command="${PHOTOBOOTH_DOTNET:-$(command -v dotnet 2>/dev/null || true)}"
  if [[ -z "$dotnet_command" ]]; then
    local candidate
    for candidate in \
      "$HOME/.dotnet/dotnet" \
      /usr/local/share/dotnet/dotnet \
      /opt/homebrew/bin/dotnet \
      /opt/homebrew/share/dotnet/dotnet; do
      if [[ -x "$candidate" ]]; then
        dotnet_command="$candidate"
        break
      fi
    done
  fi
  [[ -n "$dotnet_command" ]] ||
    fail "dotnet was not found. Set PHOTOBOOTH_DOTNET to the .NET 8 executable."
  "$dotnet_command" publish "$repo_root/PhotoBooth.Linux/PhotoBooth.Linux.csproj" \
    -c Release -r linux-x64 --self-contained true -o "$publish_dir"

  if find "$publish_dir" -type l -print -quit | grep -q .; then
    full_iso_required "Published application contains symbolic links."
  fi

  while IFS= read -r -d '' published_file; do
    relative="${published_file#"$publish_dir/"}"
    is_mutable_published_path "$relative" && continue
    mode="0644"
    type="file"
    if [[ -x "$published_file" || "$relative" == "PhotoBooth.Linux" ||
          "$relative" == *.sh ]]; then
      mode="0755"
    fi
    destination="/opt/photobooth/$relative"
    printf '%s\n' "$destination" >>"$target_destinations"
    checksum="$(shasum -a 256 "$published_file" | awk '{print $1}')"
    current_entry="$(awk -F '\t' -v path="$destination" \
      '$1 == path {print $2 "\t" $3; exit}' "$current_inventory")"
    if [[ "$current_entry" == "$checksum"$'\t'"$mode" ]]; then
      continue
    fi
    add_payload "$published_file" "$destination" "$type" "$mode" \
      "user" "user" "Application"
  done < <(find "$publish_dir" -type f -print0)

  while IFS=$'\t' read -r destination checksum mode; do
    if ! grep -Fxq "$destination" "$target_destinations"; then
      full_iso_required "Application update would delete managed runtime file: $destination"
    fi
  done <"$current_inventory"
}

main() {
  require_command git
  require_command tar
  require_command shasum
  require_command python3

  local usb_root
  if ! usb_root="$(find_photobooth_usb)"; then
    echo "PHOTOBOOTH USB not found."
    echo "Insert the PhotoBooth USB and run SEND_UPDATE_TO_USB.sh again."
    exit 0
  fi

  if ! git -C "$repo_root" diff --quiet ||
     ! git -C "$repo_root" diff --cached --quiet; then
    fail "Commit or stash tracked changes before creating a hardware-test patch."
  fi

  local marker current_version_file current_sha
  marker="$(head -n 1 "$usb_root/.photobooth-volume" | tr -d '\r\n')"
  [[ "$marker" == "$marker_signature" ]] ||
    fail "The selected volume does not contain a valid PhotoBooth marker."
  current_version_file="$usb_root/Updates/current-version.txt"
  runtime_inventory_file="$usb_root/Updates/current/runtime-files.json"
  [[ -s "$current_version_file" ]] ||
    fail "The USB has not been initialized by the new PhotoBooth base image yet. Boot it once first."
  current_sha="$(head -n 1 "$current_version_file" | tr -d '\r\n')"
  [[ "$current_sha" =~ ^[0-9a-f]{40}$ ]] ||
    fail "Installed PhotoBooth version is invalid: $current_sha"
  git -C "$repo_root" cat-file -e "$current_sha^{commit}" 2>/dev/null ||
    fail "Installed commit $current_sha is not present in this repository."
  git -C "$repo_root" merge-base --is-ancestor "$current_sha" "$target_sha" ||
    full_iso_required "Installed version is not an ancestor of $target_sha."
  if [[ "$current_sha" == "$target_sha" ]]; then
    echo "PhotoBooth USB already contains commit $target_sha."
    exit 0
  fi

  staging_root="$(mktemp -d "${TMPDIR:-/tmp}/photobooth-patch.XXXXXX")"
  mkdir -p "$staging_root/files"
  entries_file="$staging_root/entries.tsv"
  : >"$entries_file"
  payload_index=0
  local app_changed=false restart_required=false reboot_required=false
  local status source old_path
  local -a rejected_files=()

  while IFS=$'\t' read -r status source old_path; do
    [[ -n "$source" ]] || continue
    case "$status" in
      D*|R*|C*)
        rejected_files+=("$status $source ${old_path:-}")
        continue
        ;;
    esac
    if is_full_iso_source "$source"; then
      rejected_files+=("$source")
    elif is_application_source "$source"; then
      app_changed=true
      restart_required=true
    elif direct_mapping "$source"; then
      add_payload "$repo_root/$source" "$MAP_DESTINATION" "$MAP_TYPE" \
        "$MAP_MODE" "$MAP_OWNER" "$MAP_GROUP" "$MAP_COMPONENT"
      [[ "$MAP_RESTART" == "true" ]] && restart_required=true
      [[ "$MAP_REBOOT" == "true" ]] && reboot_required=true
    elif is_source_only_file "$source"; then
      continue
    else
      rejected_files+=("$source (not classified by strict whitelist)")
    fi
  done < <(git -C "$repo_root" diff --name-status "$current_sha..$target_sha")

  if (( ${#rejected_files[@]} > 0 )); then
    full_iso_required "${rejected_files[@]}"
  fi

  if [[ "$app_changed" == "true" ]]; then
    publish_application
  fi
  (( payload_index > 0 )) ||
    fail "No deployable runtime changes were found between the two commits."

  local created_at manifest patch_name patch_path patch_checksum incoming_dir
  created_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  manifest="$staging_root/manifest.json"
  python3 - "$entries_file" "$manifest" "$target_sha" "$current_sha" \
    "$created_at" "$restart_required" "$reboot_required" <<'PY'
import csv
import json
import sys

entries_path, manifest_path, target, previous, created_at, restart, reboot = sys.argv[1:]
files = []
components = []
with open(entries_path, newline="", encoding="utf-8") as source:
    for row in csv.reader(source, delimiter="\t"):
        payload, destination, kind, checksum, mode, owner, group, component = row
        files.append({
            "source": payload,
            "destination": destination,
            "type": kind,
            "sha256": checksum,
            "mode": mode,
            "owner": owner,
            "group": group,
        })
        if component not in components:
            components.append(component)

manifest = {
    "format_version": 1,
    "patch_version": target[:8],
    "git_commit": target,
    "created_at": created_at,
    "expected_previous_version": previous,
    "restart_required": restart == "true",
    "reboot_required": reboot == "true",
    "components": components,
    "files": files,
}
with open(manifest_path, "w", encoding="utf-8") as destination:
    json.dump(manifest, destination, ensure_ascii=False, indent=2)
    destination.write("\n")
PY

  patch_name="PhotoBooth-Patch-$short_sha.tar.gz"
  patch_path="$staging_root/$patch_name"
  COPYFILE_DISABLE=1 tar -C "$staging_root" -czf "$patch_path" manifest.json files
  patch_checksum="$(shasum -a 256 "$patch_path" | awk '{print $1}')"

  incoming_dir="$usb_root/Updates/incoming"
  mkdir -p "$incoming_dir"
  if find "$incoming_dir" -maxdepth 1 -type f -name 'PhotoBooth-Patch-*.tar.gz' \
      -print -quit | grep -q .; then
    fail "A pending patch already exists on the USB. Install or remove it before sending another patch."
  fi
  local usb_temporary="$incoming_dir/.$patch_name.partial"
  local usb_patch="$incoming_dir/$patch_name"
  cp "$patch_path" "$usb_temporary"
  local usb_checksum
  usb_checksum="$(shasum -a 256 "$usb_temporary" | awk '{print $1}')"
  [[ "$usb_checksum" == "$patch_checksum" ]] ||
    fail "Checksum verification failed after copying the patch to USB."
  mv -f "$usb_temporary" "$usb_patch"
  printf '%s  %s\n' "$patch_checksum" "$patch_name" >"$usb_patch.sha256"
  sync

  echo
  echo "PhotoBooth update prepared successfully."
  echo
  echo "Patch:"
  echo "$patch_name"
  echo
  echo "USB:"
  echo "$(basename "$usb_root")"
  echo
  echo "Commit:"
  echo "$target_sha"
  echo
  echo "Changed components:"
  python3 - "$manifest" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as source:
    manifest = json.load(source)
for component in manifest["components"]:
    print(f"- {component}")
PY
  echo
  echo "Checksum:"
  echo "OK ($patch_checksum)"
  echo
  echo "You can safely eject the USB after macOS finishes writing."
}

main "$@"
