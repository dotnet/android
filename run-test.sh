#!/bin/bash
# Run the CustomApplicationClassAndMultiDex(True,MonoVM) test locally
# This test failed in CI with a build failure related to marshal methods pipeline changes.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
TEST_DLL="$REPO_ROOT/bin/TestRelease/net10.0/Xamarin.Android.Build.Tests.dll"

if [ ! -f "$TEST_DLL" ]; then
    echo "ERROR: Test DLL not found at $TEST_DLL"
    echo "You may need to build the tests first."
    exit 1
fi

echo "Running: CustomApplicationClassAndMultiDex(True,MonoVM)"
exec "$REPO_ROOT/dotnet-local.sh" test "$TEST_DLL" \
    --filter "Name~CustomApplicationClassAndMultiDex" \
    -- NUnit.NumberOfTestWorkers=1
