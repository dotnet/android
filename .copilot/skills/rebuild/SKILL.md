# Rebuild SDK and Run Sample

This skill rebuilds the .NET for Android SDK and runs the HelloWorld sample to test changes.

## When to Use

Use this skill when:
- You've made changes to `src/Mono.Android/` (runtime library)
- You've made changes to `src/Xamarin.Android.Build.Tasks/` (MSBuild tasks)
- You've made changes to the ILLinker (`src/Microsoft.Android.Sdk.ILLink/`)
- You want to test your changes with a real Android app

## Prerequisites

1. **Initial setup completed**: Run `make prepare` once after cloning the repo
2. **Android emulator/device**: Must be running and accessible via `adb devices`
3. **Debug build exists**: The `bin/Debug/dotnet/` directory must exist with the android workload installed

## How to Rebuild and Run

Run the rebuild script from the repo root:

```bash
./rebuild-sample.sh
```

This script:
1. Rebuilds `Mono.Android.csproj` (Debug config)
2. Rebuilds `Xamarin.Android.Build.Tasks.csproj` (Debug config)
3. Verifies the android workload is installed
4. Cleans the HelloWorld sample
5. Builds the sample for `android-arm64` with CoreCLR
6. Deploys and runs on the connected device

## Manual Steps (if needed)

If the script doesn't exist or you need finer control:

```bash
# Set environment for local workload
export DOTNETSDK_WORKLOAD_MANIFEST_ROOTS="$(pwd)/bin/Debug/lib/sdk-manifests"
export DOTNETSDK_WORKLOAD_PACK_ROOTS="$(pwd)/bin/Debug/lib"
DOTNET="./bin/Debug/dotnet/dotnet"

# Rebuild SDK components
$DOTNET build src/Mono.Android/Mono.Android.csproj -c Debug
$DOTNET build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj -c Debug

# Clean and build sample
rm -rf samples/HelloWorld/HelloWorld/{bin,obj} samples/HelloWorld/HelloLibrary/{bin,obj}
$DOTNET build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj -c Release -r android-arm64 -p:UseMonoRuntime=false

# Run on device
$DOTNET build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj -c Release -r android-arm64 -t:Run
```

## Important Notes

- **TFM**: Sample projects must use `net11.0-android` (not `net10.0-android`)
- **Configuration**: Always use the **Debug** dotnet (`bin/Debug/dotnet/`) - it has the local workload installed
- **Runtime**: Use `-p:UseMonoRuntime=false` for CoreCLR, omit for MonoVM
- **Binlog**: Add `-bl:sample.binlog` to capture detailed build logs for debugging

## Troubleshooting

### "Workloads must be installed: android"
The workload isn't properly configured. Check:
```bash
./bin/Debug/dotnet/dotnet workload list
```
If `android` isn't listed, run `make prepare` again.

### Build fails with missing files
The SDK packs may be incomplete. Rebuild with:
```bash
./dotnet-local.sh build build-tools/create-packs/Microsoft.Android.Sdk.proj -t:ConfigureLocalWorkload -c Debug
```

### App doesn't show changes
Clean the sample completely:
```bash
rm -rf samples/HelloWorld/HelloWorld/{bin,obj} samples/HelloWorld/HelloLibrary/{bin,obj}
```
