#!/bin/bash
set -e

# Go to repo root
cd "$(dirname "$0")"

# Use Debug configuration - has the android workload installed
CONFIG=Debug

echo "=== Using $CONFIG configuration ==="

# Set environment for local workload
export DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="$(pwd)/bin/${CONFIG}/lib/sdk-manifests"
export DOTNETSDK_WORKLOAD_PACK_ROOTS="$(pwd)/bin/${CONFIG}/lib"
DOTNET="./bin/${CONFIG}/dotnet/dotnet"

# Verify dotnet exists
if [ ! -x "$DOTNET" ]; then
    echo "Error: $DOTNET not found. Run 'make prepare' first."
    exit 1
fi

echo "=== 1. Rebuilding Mono.Android (runtime library) ==="
$DOTNET build src/Mono.Android/Mono.Android.csproj -c $CONFIG

echo "=== 2. Rebuilding Microsoft.Android.Sdk.ILLink (ILLink linker steps) ==="
$DOTNET build src/Microsoft.Android.Sdk.ILLink/Microsoft.Android.Sdk.ILLink.csproj -c $CONFIG

echo "=== 3. Rebuilding Xamarin.Android.Build.Tasks (MSBuild tasks) ==="
$DOTNET build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj -c $CONFIG

echo "=== 4. Verifying workload status ==="
$DOTNET workload list

echo "=== 5. Cleaning sample ==="
rm -rf samples/HelloWorld/HelloWorld/bin samples/HelloWorld/HelloWorld/obj
rm -rf samples/HelloWorld/HelloLibrary/bin samples/HelloWorld/HelloLibrary/obj

echo "=== 6. Building sample ==="
$DOTNET build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj \
    -c Release \
    -r android-arm64 \
    -p:UseMonoRuntime=false \
    -bl:sample.binlog

echo "=== 7. Running sample ==="
$DOTNET build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj \
    -c Release \
    -r android-arm64 \
    -t:Run

echo "Done!"
