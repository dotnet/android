#!/bin/bash
# Reproduce the CoreCLRTrimmable build failure
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_PROJ="$SCRIPT_DIR/tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj"

export PATH="$HOME/android-toolchain/sdk/platform-tools:$PATH"

exec "$SCRIPT_DIR/dotnet-local.sh" build "$TEST_PROJ" \
    -c Release \
    -p:AndroidSdkDirectory="$HOME/android-toolchain/sdk" \
    -p:UseMonoRuntime=false \
    -p:_AndroidTypeMapImplementation=trimmable \
    -p:TestsFlavor=CoreCLRTrimmable \
    -t:SignAndroidPackage \
    -v:n
