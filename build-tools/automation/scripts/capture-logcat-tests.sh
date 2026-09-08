#!/usr/bin/env bash
set -euo pipefail

# Standalone, no device required: bash build-tools/automation/scripts/capture-logcat-tests.sh
script_dir="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
capture_pid=""
cleanup()
{
    if [ -n "$capture_pid" ]; then
        kill -TERM "$capture_pid" 2>/dev/null || :
        wait "$capture_pid" 2>/dev/null || :
    fi
    rm -rf "$work"
}
trap cleanup EXIT
mkdir -p "$work/bin"
cat > "$work/bin/adb" <<'ADB'
#!/usr/bin/env bash
set -eu
echo "$$" >> "$FAKE_ADB_PIDS"
echo "$1:${ADB_TRACE-unset}" >> "$FAKE_ADB_CALLS"
if [ "$1" = devices ]; then
    echo 'List of devices attached'
    case "$FAKE_ADB_MODE" in
        no-device) exit 0 ;;
        offline) printf 'emulator-5554\toffline\n'; exit 0 ;;
        unauthorized) printf 'emulator-5554\tunauthorized\n'; exit 0 ;;
        devices-failure) echo 'discovery failed' >&2; exit 7 ;;
        devices-timeout) exec sleep 60 ;;
    esac
    printf 'emulator-5554\tdevice\r\n'
else
    [ "$1" = logcat ] && [ "$2" = -d ]
    echo "logcat output: $FAKE_ADB_MODE"
    echo 'client trace' >&2
    case "$FAKE_ADB_MODE" in
        logcat-failure) exit 9 ;;
        logcat-timeout|cancel) exec sleep 60 ;;
    esac
fi
ADB
chmod +x "$work/bin/adb"
export PATH="$work/bin:$PATH"
unset ADB_TRACE
export FAKE_ADB_PIDS="$work/pids" FAKE_ADB_CALLS="$work/calls"

assert_contains()
{
    grep -Fq -- "$2" "$1" || { echo "Missing '$2' in $1" >&2; exit 1; }
}

assert_clients_stopped()
{
    while read -r client_pid; do
        if kill -0 "$client_pid" 2>/dev/null; then
            echo "Leaked adb client $client_pid" >&2
            exit 1
        fi
    done < "$FAKE_ADB_PIDS"
}

run_case()
{
    export FAKE_ADB_MODE="$1"
    local mode="$1" expected="$2" dest="${3:-$work/$1}" timeout="${4:-45}" status=0 before=$SECONDS
    if [ "$#" -ge 4 ]; then
        set -- "$dest" test "$timeout"
    else
        set -- "$dest" test
    fi
    : > "$FAKE_ADB_PIDS"
    : > "$FAKE_ADB_CALLS"
    bash "$script_dir/capture-logcat.sh" "$@" > "$work/console.txt" 2>&1 || status=$?
    if [ "$status" -ne "$expected" ]; then
        cat "$work/console.txt"
        echo "$mode: expected exit $expected, got $status" >&2
        exit 1
    fi
    [ "$((SECONDS - before))" -lt "$((timeout + 10))" ]
    if [ "$expected" -eq 124 ]; then
        [ "$((SECONDS - before))" -ge "$timeout" ]
    fi
    assert_clients_stopped
    assert_contains "$FAKE_ADB_CALLS" 'devices:unset'
    if [ "$expected" -ne 0 ]; then
        assert_contains "$work/console.txt" '##vso[task.setvariable variable=LogcatCaptureFailed]true'
    elif grep -q 'task.setvariable' "$work/console.txt"; then
        echo 'Successful capture should not force artifact upload' >&2
        exit 1
    fi
    echo "PASS $mode (exit $status)"
}

run_case success 0 "$work/artifacts with spaces"
assert_contains "$work/artifacts with spaces/logcat-test.txt" 'logcat output: success'
assert_contains "$FAKE_ADB_CALLS" 'logcat:adb,shell'
attempts=("$work/artifacts with spaces"/logcat-test-attempt-*)
assert_contains "${attempts[0]}/logcat.stderr.txt" 'client trace'
assert_contains "${attempts[0]}/diagnostics.txt" 'exit=0; timed_out=false; stdout_bytes='

for mode in no-device offline unauthorized; do
    run_case "$mode" 0
    assert_contains "$work/console.txt" 'logcat capture skipped: no connected device'
    [ ! -e "$work/$mode/logcat-test.txt" ]
    [ "$(wc -l < "$FAKE_ADB_CALLS")" -eq 1 ]
done
run_case devices-failure 7
assert_contains "$work/console.txt" 'discovery failed'
[ "$(wc -l < "$FAKE_ADB_CALLS")" -eq 1 ]
run_case logcat-failure 9
assert_contains "$work/logcat-failure/logcat-test.txt" 'logcat output: logcat-failure'
run_case devices-timeout 124 "$work/devices-timeout" 6
assert_contains "$work/console.txt" 'devices deadline exceeded'
[ "$(wc -l < "$FAKE_ADB_CALLS")" -eq 1 ]
run_case devices-timeout 124 "$work/default-deadline"
assert_contains "$work/console.txt" 'capture deadline=45s'

# Reinvocation models the task runner retry, not an internal retry loop.
run_case logcat-timeout 124 "$work/retry" 16
assert_contains "$work/console.txt" 'timed_out=true'
assert_contains "$work/console.txt" 'logcat progress;'
attempts=("$work/retry"/logcat-test-attempt-*)
first_attempt="${attempts[0]}"
cp "$first_attempt/diagnostics.txt" "$work/first-diagnostics.txt"
cp "$first_attempt/logcat.stderr.txt" "$work/first-stderr.txt"
run_case success 0 "$work/retry"
attempts=("$work/retry"/logcat-test-attempt-*)
[ "${#attempts[@]}" -eq 2 ]
assert_contains "$first_attempt/logcat.txt" 'logcat output: logcat-timeout'
cmp "$first_attempt/diagnostics.txt" "$work/first-diagnostics.txt"
cmp "$first_attempt/logcat.stderr.txt" "$work/first-stderr.txt"
assert_contains "$work/retry/logcat-test.txt" 'logcat output: success'
echo 'PASS retry artifact preservation'

run_case logcat-failure 9 "$work/exhausted"
attempts=("$work/exhausted"/logcat-test-attempt-*)
first_attempt="${attempts[0]}"
cp "$first_attempt/diagnostics.txt" "$work/first-diagnostics.txt"
run_case logcat-failure 9 "$work/exhausted"
attempts=("$work/exhausted"/logcat-test-attempt-*)
[ "${#attempts[@]}" -eq 2 ]
cmp "$first_attempt/diagnostics.txt" "$work/first-diagnostics.txt"
for attempt in "${attempts[@]}"; do
    assert_contains "$attempt/logcat.txt" 'logcat output: logcat-failure'
    assert_contains "$attempt/logcat.stderr.txt" 'client trace'
done
echo 'PASS exhausted retry failure signaling and artifacts'

export FAKE_ADB_MODE=cancel
: > "$FAKE_ADB_PIDS"
: > "$FAKE_ADB_CALLS"
bash "$script_dir/capture-logcat.sh" "$work/cancel" test 20 > "$work/console.txt" 2>&1 &
capture_pid=$!
for ((i = 0; i < 100; i++)); do
    if grep -q 'logcat:' "$FAKE_ADB_CALLS"; then
        break
    fi
    sleep 0.1
done
assert_contains "$FAKE_ADB_CALLS" 'logcat:adb,shell'
kill -TERM "$capture_pid"
status=0
wait "$capture_pid" || status=$?
capture_pid=""
[ "$status" -eq 143 ]
assert_clients_stopped
assert_contains "$work/console.txt" 'capture interrupted: SIGTERM'
attempts=("$work/cancel"/logcat-test-attempt-*)
assert_contains "${attempts[0]}/logcat.txt" 'logcat output: cancel'
echo 'PASS cancellation cleanup and partial artifacts'

status=0
bash "$script_dir/capture-logcat.sh" "$work/invalid" test 0 > "$work/console.txt" 2>&1 || status=$?
[ "$status" -eq 2 ]
assert_contains "$work/console.txt" 'Invalid capture timeout'
echo 'PASS invalid deadline'
