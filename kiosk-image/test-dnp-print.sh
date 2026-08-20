#!/usr/bin/env bash
set -uo pipefail

export LC_ALL=C

queue_name="DNP_RX1"
page_size="w288h432"
resolution="300dpi"
setup_script="/usr/local/sbin/photobooth-printer-setup"
ppd_file="/usr/share/ppd/photobooth/DNP-DSRX1.ppd"
test_image="/opt/photobooth/test-print.jpg"
log_file="/tmp/dnp-print-test.log"

: >"$log_file"
exec > >(tee -a "$log_file") 2>&1

finish() {
  echo "Diagnostic log: $log_file"

  local data_root="/media/user/PHOTOBOOTH"
  local diagnostics_dir="$data_root/Diagnostics"
  local timestamped_log
  if mountpoint -q "$data_root"; then
    timestamped_log="$diagnostics_dir/dnp-print-test-$(date +%Y%m%d-%H%M%S).log"
    if ! mkdir -p "$diagnostics_dir" ||
       ! cp "$log_file" "$diagnostics_dir/dnp-print-test.log" ||
       ! cp "$log_file" "$timestamped_log"; then
      echo "[ERROR] Failed to copy the diagnostic log to the PHOTOBOOTH data partition." >&2
      return
    fi
    chown user:user "$diagnostics_dir/dnp-print-test.log" "$timestamped_log" || true
    sync "$diagnostics_dir/dnp-print-test.log" "$timestamped_log" || true
    echo "Persistent diagnostic log: $diagnostics_dir/dnp-print-test.log"
    echo "Archived diagnostic log: $timestamped_log"
  else
    echo "[ERROR] PHOTOBOOTH data partition is not mounted; the diagnostic log was not copied to the USB drive." >&2
  fi
}
trap finish EXIT

info() {
  echo "[INFO] $*"
}

ok() {
  echo "[OK] $*"
}

error() {
  echo "[ERROR] $*" >&2
}

print_command() {
  printf '[INFO] Command:'
  printf ' %q' "$@"
  printf '\n'
}

run_capture() {
  local stdout_file stderr_file exit_code
  stdout_file="$(mktemp)"
  stderr_file="$(mktemp)"
  print_command "$@"
  "$@" >"$stdout_file" 2>"$stderr_file"
  exit_code=$?
  [[ ! -s "$stdout_file" ]] || cat "$stdout_file"
  [[ ! -s "$stderr_file" ]] || cat "$stderr_file" >&2
  RUN_OUTPUT="$(cat "$stdout_file")"
  RUN_ERROR="$(cat "$stderr_file")"
  RUN_EXIT_CODE="$exit_code"
  rm -f "$stdout_file" "$stderr_file"
  return "$exit_code"
}

dump_diagnostics() {
  info "CUPS/Gutenprint diagnostics follow."
  echo "--- lsusb ---"
  lsusb 2>&1 || true
  echo "--- lpstat -t ---"
  lpstat -t 2>&1 || true
  echo "--- lpinfo -v ---"
  timeout 10 /usr/sbin/lpinfo -v 2>&1 || true
  echo "--- DNP RX1 options ---"
  lpoptions -p "$queue_name" -l 2>&1 || true
  echo "--- Gutenprint RX1 backend ---"
  timeout 15 /usr/lib/cups/backend/gutenprint53+usb 2>&1 || true
  echo "--- CUPS service ---"
  systemctl status cups.service --no-pager 2>&1 || true
  echo "--- CUPS journal ---"
  journalctl -u cups.service -n 120 --no-pager 2>&1 || true
  echo "--- CUPS error_log ---"
  tail -n 200 /var/log/cups/error_log 2>&1 || true
  echo "--- Kernel USB messages ---"
  journalctl -k -n 160 --no-pager 2>&1 || true
}

fail() {
  error "$*"
  dump_diagnostics
  exit 1
}

if [[ "$EUID" -ne 0 ]]; then
  fail "Run this test as root: sudo /usr/local/sbin/test-dnp-print"
fi

info "DNP DS-RX1 manual print test"
info "No PhotoBooth application code participates in this test."

if [[ ! -x "$setup_script" ]]; then
  fail "Printer setup script is missing: $setup_script"
fi

if [[ ! -s "$ppd_file" ]]; then
  fail "Prebuilt RX1 PPD is missing: $ppd_file"
fi
ok "Prebuilt RX1 PPD found: $ppd_file"

if [[ ! -s "$test_image" ]]; then
  fail "Test image is missing: $test_image"
fi
ok "Test image found: $test_image"

info "Detecting RX1 and creating or repairing CUPS queue $queue_name."
print_command "$setup_script"
"$setup_script"
setup_exit_code=$?
if [[ "$setup_exit_code" -ne 0 ]]; then
  fail "Printer setup failed (exit code $setup_exit_code)."
fi
ok "Printer setup completed."

if ! run_capture lpstat -r; then
  fail "lpstat -r failed (exit code $RUN_EXIT_CODE): $RUN_ERROR"
fi
ok "CUPS scheduler status checked."

if ! run_capture lpstat -p "$queue_name"; then
  fail "Queue check failed (exit code $RUN_EXIT_CODE): $RUN_ERROR"
fi
if [[ "$RUN_OUTPUT" == *"disabled"* ]]; then
  fail "Queue $queue_name is disabled."
fi
ok "Queue $queue_name exists and is enabled."

if ! run_capture lpstat -v "$queue_name"; then
  fail "Queue URI check failed (exit code $RUN_EXIT_CODE): $RUN_ERROR"
fi
queue_uri="${RUN_OUTPUT#*: }"
if [[ ! "$queue_uri" =~ ^gutenprint[0-9]+\+usb://dnp-dsrx1/ ]]; then
  fail "Queue $queue_name uses an unexpected URI: $queue_uri"
fi
ok "Actual queue URI: $queue_uri"

if ! run_capture lpoptions -p "$queue_name" -l; then
  fail "Unable to list RX1 PPD options (exit code $RUN_EXIT_CODE): $RUN_ERROR"
fi
if ! grep -q "^\\*PageSize $page_size/" "$ppd_file"; then
  fail "The installed RX1 PPD does not offer internal PageSize=$page_size."
fi
if ! grep -q "^\\*Resolution $resolution/" "$ppd_file"; then
  fail "The installed RX1 PPD does not offer internal Resolution=$resolution."
fi
ok "Confirmed RX1 media option: PageSize=$page_size (4x6)."
ok "Confirmed RX1 resolution option: Resolution=$resolution."

info "Submitting exactly one test image."
if ! run_capture lp -d "$queue_name" \
    -o "PageSize=$page_size" -o "Resolution=$resolution" "$test_image"; then
  fail "Print submission failed (exit code $RUN_EXIT_CODE): $RUN_ERROR"
fi
job_id="$(printf '%s\n' "$RUN_OUTPUT" | grep -oE "${queue_name}-[0-9]+" | head -n 1)"
if [[ -z "$job_id" ]]; then
  fail "CUPS accepted no recognizable job ID. Output: $RUN_OUTPUT"
fi
ok "Print job submitted: $job_id"

info "Waiting up to 90 seconds while CUPS processes $job_id."
job_completed=false
job_seen_active=false

job_is_listed() {
  local jobs="$1"
  printf '%s\n' "$jobs" |
    awk -v target="$job_id" '$1 == target { found = 1 } END { exit !found }'
}

for _ in $(seq 1 18); do
  if ! run_capture timeout 10 lpstat -p "$queue_name" -l; then
    fail "Unable to query queue state while processing $job_id (exit code $RUN_EXIT_CODE): $RUN_ERROR"
  fi
  queue_state="$RUN_OUTPUT"
  if [[ "$queue_state" == *"disabled"* ]]; then
    fail "Queue $queue_name became disabled while processing $job_id."
  fi

  if ! run_capture timeout 10 lpstat -W not-completed -o "$queue_name"; then
    fail "Unable to query active CUPS jobs for $job_id (exit code $RUN_EXIT_CODE): $RUN_ERROR"
  fi
  active_jobs="$RUN_OUTPUT"
  if job_is_listed "$active_jobs"; then
    job_seen_active=true
    info "CUPS still lists $job_id as active."
    sleep 5
    continue
  fi

  if ! run_capture timeout 10 lpstat -W completed -o "$queue_name"; then
    fail "Unable to query completed CUPS jobs for $job_id (exit code $RUN_EXIT_CODE): $RUN_ERROR"
  fi
  completed_jobs="$RUN_OUTPUT"
  if job_is_listed "$completed_jobs"; then
    job_completed=true
    break
  fi

  if [[ "$job_seen_active" == true ]]; then
    info "$job_id left the active list but is not in completed history yet; waiting."
  else
    info "$job_id is not visible in CUPS history yet; waiting."
  fi
  sleep 5
done

if [[ "$job_completed" != true ]]; then
  info "Final completed-jobs fallback check for $job_id."
  if ! run_capture timeout 10 lpstat -W completed -o "$queue_name"; then
    fail "Final completed-job query failed for $job_id (exit code $RUN_EXIT_CODE): $RUN_ERROR"
  fi
  if job_is_listed "$RUN_OUTPUT"; then
    job_completed=true
  fi
fi

echo "--- Final active jobs ---"
timeout 10 lpstat -W not-completed -o "$queue_name" 2>&1 || true
echo "--- Completed jobs ---"
timeout 10 lpstat -W completed -o "$queue_name" 2>&1 || true
echo "--- Final queue state ---"
timeout 10 lpstat -p "$queue_name" -l 2>&1 || true

if [[ "$job_completed" != true ]]; then
  fail "CUPS did not report $job_id as completed within 90 seconds."
fi

ok "CUPS reports $job_id as completed."
info "Confirm that the physical DNP DS-RX1 printed test-print.jpg."
info "CUPS completion alone is not proof of physical printing."
