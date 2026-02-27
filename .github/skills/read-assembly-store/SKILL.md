---
name: read-assembly-store
description: Read and inspect .NET assembly stores from Android APK, AAB, or raw store files. Use this when users want to inspect, examine, or list assemblies in an APK, AAB, assembly store, or manifest file.
---

# Read Assembly Store

Read and inspect .NET assembly stores from Android APK, AAB, or raw store files using the `assembly-store-reader-mk2` tool in this repository.

## What It Does

Lists all .NET managed assemblies embedded in an Android app, showing per-assembly metadata (PE image offset/size, debug data, config data, name hashes) grouped by target architecture.

Supports these input types:
- **APK files** (`.apk`)
- **AAB files** (`.aab`)
- **Assembly store files** (`libassembly-store.so`, `assemblies.blob`)
- **Store manifest files** (`base_assemblies.manifest`)
- **Store base names** (e.g. `base` or `base_assemblies`)

## How to Run

From the repository root, run the tool using `dotnet-local` to ensure the correct SDK is used.

**On Windows:**

```powershell
.\dotnet-local.cmd run --project .\tools\assembly-store-reader-mk2\ -- [OPTIONS] <file>
```

**On macOS/Linux:**

```bash
./dotnet-local.sh run --project ./tools/assembly-store-reader-mk2/ -- [OPTIONS] <file>
```

### Options

- `-a`, `--arch=ARCHITECTURES` — Comma-separated list of architectures to limit output to (e.g. `Arm64`, `x64`, `Arm`, `X86`). Aliases like `aarch64`, `arm32`, `armv7a`, `armv8a` also work.
- `-h`, `--help` — Show help.

## Instructions

1. Determine the user's operating system from the environment context.
2. Ask the user which file to inspect if not already specified.
3. Run the appropriate command from the **repository root**, passing the file path and any options after `--`.
4. Report the output to the user, summarizing the assemblies found per architecture.

## Examples

Inspect an APK:

```powershell
.\dotnet-local.cmd run --project .\tools\assembly-store-reader-mk2\ -- path\to\app.apk
```

Inspect an APK, arm64 only:

```powershell
.\dotnet-local.cmd run --project .\tools\assembly-store-reader-mk2\ -- --arch=Arm64 path\to\app.apk
```

Inspect a raw store file:

```powershell
.\dotnet-local.cmd run --project .\tools\assembly-store-reader-mk2\ -- path\to\libassembly-store.so
```
