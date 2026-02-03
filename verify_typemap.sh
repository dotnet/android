#!/bin/bash
set -e

echo "=== Verifying TypeMap V3 Fix ==="

echo "1. Uninstalling stale app..."
adb uninstall Mono.Android.NET_Tests || true

echo "2. Rebuilding Mono.Android..."
./dotnet-local.sh build src/Mono.Android/Mono.Android.csproj

echo "3. Rebuilding Tests (with V3 enabled)..."
./dotnet-local.sh build tests/Mono.Android-Tests/Mono.Android-Tests/Mono.Android.NET-Tests.csproj \
  -t:Install \
  -p:UseTypemapV3=true \
  -p:AndroidEnableMarshalMethods=true \
  -p:AndroidSupportedAbis="arm64-v8a;x86_64"

echo "4. Running Tests..."
adb shell am instrument -w Mono.Android.NET_Tests/xamarin.android.runtimetests.NUnitInstrumentation

echo "=== Verification Complete ==="
