#!/usr/bin/env bash
set -euo pipefail

# Usage: bash capture-logcat.sh DEST TEST_NAME [TIMEOUT_SECONDS]
# The deadline covers both adb commands. Azure retries the task, not this script.
# A unique directory is needed because Azure does not expose the task retry count.
dest="${1:?Destination directory is required}"
test_name="${2:?Test name is required}"
timeout_seconds="${3:-45}"
if [[ ! "$timeout_seconds" =~ ^[1-9][0-9]*$ ]]; then
    echo "Invalid capture timeout: $timeout_seconds" >&2
    exit 2
fi

mkdir -p "$dest"
attempt="$(mktemp -d "$dest/logcat-$test_name-attempt-XXXXXX")"
started=$SECONDS
adb_pid=""

record()
{
    printf '%s %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$*" | tee -a "$attempt/diagnostics.txt"
}

cleanup()
{
    local status=$?
    if [ -n "$adb_pid" ]; then
        if kill -0 "$adb_pid" 2>/dev/null; then
            kill -KILL "$adb_pid" || echo "Failed to stop adb client $adb_pid" >&2
        fi
        # Reap the owned client; failure is expected after killing it.
        wait "$adb_pid" 2>/dev/null || :
    fi
    if [ "$status" -ne 0 ]; then
        # Keep failed-attempt artifacts even if the Azure task retry succeeds.
        echo '##vso[task.setvariable variable=LogcatCaptureFailed]true'
    fi
}
trap cleanup EXIT
trap 'record "capture interrupted: SIGINT"; exit 130' INT
trap 'record "capture interrupted: SIGTERM"; exit 143' TERM

run_adb()
{
    local phase="$1" stdout="$2" stderr="$3"
    shift 3
    local command_started=$SECONDS next_progress=$((SECONDS + 10)) timed_out=false
    record "$phase start; capture deadline=${timeout_seconds}s; artifacts=$attempt"
    "$@" > "$stdout" 2> "$stderr" &
    adb_pid=$!
    while kill -0 "$adb_pid" 2>/dev/null; do
        if [ "$((SECONDS - started))" -ge "$timeout_seconds" ]; then
            timed_out=true
            record "$phase deadline exceeded; stopping adb client pid=$adb_pid"
            kill -KILL "$adb_pid" || echo "adb client $adb_pid exited before termination" >&2
            break
        fi
        if [ "$SECONDS" -ge "$next_progress" ]; then
            record "$phase progress; elapsed=$((SECONDS - command_started))s; stdout_bytes=$(wc -c < "$stdout"); stderr_bytes=$(wc -c < "$stderr")"
            next_progress=$((SECONDS + 10))
        fi
        sleep 1
    done
    adb_status=0
    wait "$adb_pid" || adb_status=$?
    adb_pid=""
    record "$phase end; elapsed=$((SECONDS - command_started))s; exit=$adb_status; timed_out=$timed_out; stdout_bytes=$(wc -c < "$stdout"); stderr_bytes=$(wc -c < "$stderr")"
    if [ "$timed_out" = true ]; then
        adb_status=124
    fi
}

run_adb devices "$attempt/devices.txt" "$attempt/devices.stderr.txt" adb devices
cat "$attempt/devices.txt" "$attempt/devices.stderr.txt"
if [ "$adb_status" -ne 0 ]; then
    record "logcat capture failed: adb devices status=$adb_status"
    exit "$adb_status"
fi
if ! awk 'NR > 1 && ($2 == "device" || $2 == "device\r") { found=1 } END { exit !found }' "$attempt/devices.txt"; then
    record "logcat capture skipped: no connected device"
    exit 0
fi

# Trace only this client, not the adb server or other pipeline commands.
run_adb logcat "$attempt/logcat.txt" "$attempt/logcat.stderr.txt" env ADB_TRACE=adb,shell adb logcat -d
# Keep the existing artifact name for consumers, including partial failed output.
cp "$attempt/logcat.txt" "$dest/logcat-$test_name.txt"
record "logcat capture finished: status=$adb_status; total_elapsed=$((SECONDS - started))s"
exit "$adb_status"
