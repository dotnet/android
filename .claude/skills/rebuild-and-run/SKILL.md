---
name: rebuild-and-run
description: Build the .NET for Android product and run sample applications using the local SDK
---

# Rebuild and Run .NET for Android

Instructions for building the .NET for Android product and running sample applications.

## Prerequisites

- macOS (Darwin) or Linux
- Android SDK and NDK installed (see `Documentation/building/unix/instructions.md`)
- .NET SDK (the local build uses its own SDK at `bin/Debug/dotnet/`)

## Full Product Build

### 1. Prepare the Build Environment

```bash
make prepare
```

This downloads dependencies, configures submodules, and creates necessary directories.

### 2. Link android-deps Workload Manifest

After `make prepare`, link the workload manifest so the local SDK recognizes it:

```bash
ln -sf "$(pwd)/bin/Debug/lib/sdk-manifests/11.0.100-alpha.1/android.deps.workload" \
    bin/Debug/dotnet/sdk-manifests/11.0.100-alpha.1/
```

### 3. Build Everything

```bash
make all
```

This builds:
- Native runtime libraries (MonoVM, CoreCLR support)
- Managed assemblies (Mono.Android, build tasks)
- SDK packs and workload manifests
- Tools and utilities

Build output goes to `bin/Debug/`.

## Building Individual Projects

For faster iteration when working on specific components:

```bash
# Build the main build tasks solution
./dotnet-local.sh build Xamarin.Android.Build.Tasks.sln -v:q

# Build a specific project
./dotnet-local.sh build src/Xamarin.Android.Build.Tasks/Xamarin.Android.Build.Tasks.csproj
```

After building, copy updated files to the packs directory:

```bash
# Copy updated DLL
cp bin/TestDebug/net10.0/Xamarin.Android.Build.Tasks.dll \
   bin/Debug/lib/packs/Microsoft.Android.Sdk.Darwin/36.1.99/tools/

# Copy updated targets (if modified)
cp src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/targets/*.targets \
   bin/Debug/lib/packs/Microsoft.Android.Sdk.Darwin/36.1.99/targets/
```

## Running the HelloWorld Sample

### Build the Sample

```bash
./dotnet-local.sh build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj \
    -c Release -r android-arm64
```

### Build and Install on Device

```bash
./dotnet-local.sh build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj \
    -c Release -r android-arm64 -t:Install
```

### Build, Install, and Run

```bash
./dotnet-local.sh build samples/HelloWorld/HelloWorld/HelloWorld.DotNet.csproj \
    -c Release -r android-arm64 -t:Run
```

### Available Runtime Identifiers

- `android-arm64` - 64-bit ARM devices (most modern phones)
- `android-arm` - 32-bit ARM devices
- `android-x64` - 64-bit x86 emulators
- `android-x86` - 32-bit x86 emulators

## Build Outputs

After a successful build:

| Path | Description |
|------|-------------|
| `bin/Debug/dotnet/` | Local .NET SDK installation |
| `bin/Debug/lib/packs/Microsoft.Android.Sdk.Darwin/36.1.99/` | Android SDK pack |
| `samples/HelloWorld/HelloWorld/bin/Release/net11.0-android/android-arm64/` | Sample app output |
| `*.apk` | Android application package |

## TypeMap Assembly Generation

The build generates a TypeMap assembly containing proxy types for Java interop:

```
Generated TypeMap assembly: obj/Release/net11.0-android/android-arm64/typemap/_Microsoft.Android.TypeMaps.dll
```

This assembly and the generated LLVM IR files (`marshal_methods_*.ll`) enable efficient Java-to-.NET type mapping at runtime.

## Troubleshooting

### "Workload 'android-deps' is not recognized"

Run the symlink command from step 2 above.

### Build Fails on Restore

```bash
# Clean and retry
make clean
make prepare
make all
```

### Sample Build Fails with Missing Workload

Ensure the full product build completed successfully before building samples.

### Viewing Build Logs

```bash
# Build with binary log
./dotnet-local.sh build <project> -bl:build.binlog

# View with MSBuild Structured Log Viewer
# https://msbuildlog.com/
```
