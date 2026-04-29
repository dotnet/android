#!/bin/bash
# Wrapper script to run the SimilarAndroidXAssemblyNames(False,MonoVM) test case locally.
# Usage: ./run-test-similar-androidx.sh [Configuration]
#   Configuration defaults to "Release"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${1:-Release}"
TFM="net10.0"
DLL="$SCRIPT_DIR/bin/Test${CONFIG}/${TFM}/Xamarin.Android.Build.Tests.dll"

if [[ ! -f "$DLL" ]]; then
    echo "ERROR: Test DLL not found at: $DLL"
    echo "       You may need to build the tests first, or pass a different Configuration."
    exit 1
fi

echo "=== Running: SimilarAndroidXAssemblyNames(False,MonoVM) ==="
echo "    DLL: $DLL"
echo ""

exec "$SCRIPT_DIR/dotnet-local.sh" test "$DLL" \
    --logger "console;verbosity=detailed" \
    -- NUnit.Where="method == SimilarAndroidXAssemblyNames and test =~ /False,MonoVM/"
