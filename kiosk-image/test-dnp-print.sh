#!/usr/bin/env bash
set -uo pipefail

export LC_ALL=C

queue_name="DNP_RX1"
setup_script="/usr/local/sbin/photobooth-printer-setup"
ppd_file="/usr/share/ppd/photobooth/DNP-DSRX1.ppd"
test_image="/opt/photobooth/test-print.jpg"
log_file="/tmp/dnp-print-test.log"

: >"$log_file"
exec > >(tee -a "$log_file") 2>&1

finish() {
  echo "Diagnostic log: $log_file"
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
  /usr/sbin/lpinfo -v 2>&1 || true
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
  fail "Run this test as root: sudo ./test-dnp-print.sh"
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

info "Submitting exactly one test image."
if ! run_capture lp -d "$queue_name" "$test_image"; then
  fail "Print submission failed (exit code $RUN_EXIT_CODE): $RUN_ERROR"
fi
job_id="$(printf '%s\n' "$RUN_OUTPUT" | grep -oE "${queue_name}-[0-9]+" | head -n 1)"
if [[ -z "$job_id" ]]; then
  fail "CUPS accepted no recognizable job ID. Output: $RUN_OUTPUT"
fi
ok "Print job submitted: $job_id"

info "Waiting up to 90 seconds while CUPS processes $job_id."
job_active=true
for _ in $(seq 1 18); do
  active_jobs="$(lpstat -o "$queue_name" 2>&1 || true)"
  queue_state="$(lpstat -p "$queue_name" -l 2>&1 || true)"
  echo "$queue_state"
  if [[ "$queue_state" == *"disabled"* ]]; then
    fail "Queue $queue_name became disabled while processing $job_id."
  fi
  if [[ "$active_jobs" != *"$job_id"* ]]; then
    job_active=false
    break
  fi
  info "Job $job_id is still active."
  sleep 5
done

echo "--- Final active jobs ---"
lpstat -o "$queue_name" 2>&1 || true
echo "--- Completed jobs ---"
lpstat -W completed -o "$queue_name" 2>&1 || true
echo "--- Final queue state ---"
lpstat -p "$queue_name" -l 2>&1 || true

if [[ "$job_active" == true ]]; then
  fail "Job $job_id is still active after 90 seconds."
fi

ok "CUPS no longer reports $job_id as active."
info "Confirm that the physical DNP DS-RX1 printed test-print.jpg."
info "CUPS completion alone is not proof of physical printing."
