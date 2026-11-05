#!/bin/bash
set -euo pipefail

log_file=$(mktemp)
trap 'rm -f "$log_file"' EXIT

for attempt in 1 2 3; do
    status=0
    pwsh "$@" > "$log_file" 2>&1 || status=$?
    cat "$log_file"

    if [ "$status" -eq 0 ]; then
        exit 0
    fi

    # dotnet/android#12652: this signature occurs before the script executes.
    # Never replay a script that failed after starting to modify test results.
    if [ "$attempt" -eq 3 ] ||
        ! grep -Fq "Call to 'procargs' failed with errno 5" "$log_file" ||
        ! grep -Fq 'Microsoft.PowerShell.ManagedPSEntry+StartupException' "$log_file"; then
        exit "$status"
    fi

    echo "##vso[task.logissue type=warning]PowerShell startup failed (attempt $attempt/3); retrying."
    sleep "$((attempt * attempt))"
done
