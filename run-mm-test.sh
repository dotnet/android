#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_DLL="$SCRIPT_DIR/bin/TestRelease/net10.0/Xamarin.Android.Build.Tests.dll"
rm -rf "$SCRIPT_DIR/bin/TestRelease/temp/MarshalMethodsCollectionScanning"
exec "$SCRIPT_DIR/dotnet-local.sh" test "$TEST_DLL" \
    --filter "Name~MarshalMethodsCollectionScanning" \
    -- NUnit.NumberOfTestWorkers=1
