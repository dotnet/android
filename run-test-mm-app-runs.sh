#!/bin/bash
# Run the MarshalMethodsAppRuns device test
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_DLL="$SCRIPT_DIR/bin/TestRelease/MSBuildDeviceIntegration/net10.0/MSBuildDeviceIntegration.dll"

export PATH="$HOME/android-toolchain/sdk/platform-tools:$PATH"

if [ ! -f "$TEST_DLL" ]; then
    echo "ERROR: Test DLL not found at $TEST_DLL"
    exit 1
fi

exec "$SCRIPT_DIR/dotnet-local.sh" test "$TEST_DLL" \
    --filter "Name~MarshalMethodsAppRuns" \
    -- NUnit.NumberOfTestWorkers=1
