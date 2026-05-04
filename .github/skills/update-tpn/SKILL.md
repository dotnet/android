---
name: update-tpn
description: >-
  Audit and update the THIRD-PARTY-NOTICES.TXT file. Use when the user asks to
  "update TPNs", "audit third-party notices", "check third-party licenses",
  or after adding/removing a dependency. Scans submodules, vendored code, NuGet
  packages, and native libraries to ensure the TPN file is complete and accurate.
---

# Update Third-Party Notices

Audit and regenerate the `THIRD-PARTY-NOTICES.TXT` file at the repo root.

This file is checked in and shipped as-is in the product NuGet packages.
There is no code generation step — this skill replaces the old xaprepare TPN infrastructure.

## File Format

The file uses the **MicrosoftOSS** header format:

```
xamarin-android

THIRD - PARTY SOFTWARE NOTICES AND INFORMATION
Do Not Translate or Localize

This project incorporates components from the projects listed below.
The original copyright notices and the licenses under which Microsoft
received such components are set forth below.
Microsoft reserves all rights not expressly granted herein, whether by
implication, estoppel or otherwise.

1.  name (url)
2.  name (url)
...

%% name NOTICES AND INFORMATION BEGIN HERE
==========================================
<license text>

==========================================
END OF name NOTICES AND INFORMATION
```

Entries are sorted case-insensitively by name. Each entry has a numbered TOC line and a license section.

## Audit Workflow

### Step 1 — Inventory all dependencies

Scan these sources to build a complete list of third-party dependencies:

#### Git Submodules
Read `.gitmodules` for all submodules. Current submodules and their license files:

| Submodule | URL | License File |
|-----------|-----|-------------|
| Java.Interop | https://github.com/dotnet/java-interop | `external/Java.Interop/LICENSE` |
| lz4 | https://github.com/dotnet/lz4 (fork of https://github.com/lz4/lz4) | `external/lz4/lib/LICENSE` |
| xxHash | https://github.com/Cyan4973/xxHash | `external/xxHash/LICENSE` |
| constexpr-xxh3 | https://github.com/chys87/constexpr-xxh3 | `external/constexpr-xxh3/LICENSE` |
| robin-map | https://github.com/xamarin/robin-map (fork of https://github.com/Tessil/robin-map) | `external/robin-map/LICENSE` |
| libunwind | https://github.com/libunwind/libunwind | `external/libunwind/LICENSE` |
| xamarin-android-tools | https://github.com/dotnet/android-tools | (not a third-party dep) |
| android-api-docs | https://github.com/dotnet/android-api-docs | (not a third-party dep) |
| debugger-libs | https://github.com/mono/debugger-libs | (not a third-party dep — internal) |

#### Vendored Source (`src-ThirdParty/`)
List contents of `src-ThirdParty/` directory. Current vendored code and license sources:

| Directory | Name | License Source |
|-----------|------|---------------|
| `android-platform-tools-base/` | android/platform/tools/base | https://android.googlesource.com/platform/tools/base/+/refs/heads/main/sdk-common/NOTICE (Apache 2.0) |
| `bazel/` | bazelbuild/bazel | https://github.com/bazelbuild/bazel/ (Apache 2.0) |
| `bionic/` | google/bionic | https://android.googlesource.com/platform/bionic/ (Apache 2.0) |
| `crc32.net/` | force-net/crc32.net | https://github.com/force-net/Crc32.NET (MIT) |
| `NUnitLite/` | nunit/nunitlite | https://github.com/nunit/nunitlite/ (MIT) |
| `StrongNameSigner/` | brutaldev/StrongNameSigner | https://github.com/brutaldev/StrongNameSigner/ (Apache 2.0) |

Note: `Mono.Security.Cryptography/`, `System.Diagnostics.CodeAnalysis/`, `System.Runtime.CompilerServices/`, and `dotnet/` are Microsoft-owned and do not need TPN entries.

#### NuGet Packages
Search `.csproj` files for `<PackageReference>` elements. Current third-party NuGet packages needing TPNs:

| Package | Name in TPN | License URL |
|---------|------------|-------------|
| ELFSharp | KonradKuczynski/ELFSharp | https://elfsharp.it/ (MIT + LLVM) |
| K4os.Compression.LZ4 | MiloszKrajewski/K4os.Compression.LZ4 | https://github.com/MiloszKrajewski/K4os.Compression.LZ4/ (MIT) |
| Xamarin.LibZipSharp | xamarin/LibZipSharp | https://github.com/xamarin/LibZipSharp/ (MIT) |
| Irony | IronyProject/Irony | https://github.com/IronyProject/Irony (MIT) |
| Newtonsoft.Json | JamesNK/Newtonsoft.Json | https://github.com/JamesNK/Newtonsoft.Json (MIT) |
| NuGet.ProjectModel | NuGet/NuGet.Client | https://github.com/NuGet/NuGet.Client (Apache 2.0) |
| Mono.Cecil | mono/cecil | https://github.com/mono/cecil/ (MIT) |
| Microsoft.Xml.SgmlReader | lovettchris/SgmlReader | https://github.com/lovettchris/SgmlReader/ (Apache 2.0) |

Note: Microsoft-owned NuGet packages (Microsoft.*, System.*) do not need TPN entries.

#### Vendored Linker Code
The `src/Xamarin.Android.Build.Tasks/Linker/External/` directory contains vendored code from the Mono linker:

| Source | Name in TPN | License URL |
|--------|------------|-------------|
| Linker/External/ | mono/linker | https://github.com/mono/linker/ (MIT) |

#### Native Libraries (from CMakeLists.txt)
Check `src/native/` CMakeLists.txt files for references to external native code. The submodules above (lz4, xxHash, libunwind, robin-map) are compiled into native libraries.

#### Android SDK Tools
These are downloaded and shipped with the SDK:

| Tool | Name in TPN | License URL |
|------|------------|-------------|
| aapt2 | google/aapt2 | https://mvnrepository.com/artifact/com.android.tools.build/aapt2 (Apache 2.0) |
| bundletool | google/bundletool | https://github.com/google/bundletool (Apache 2.0) |
| r8 | google/r8 | https://r8.googlesource.com/r8/ (BSD-3-Clause) |
| binutils | gnu/binutils | https://sourceware.org/git/?p=binutils-gdb.git;a=tree;hb=HEAD (GPLv3) |

#### libzip (via LibZipSharp NuGet)
LibZipSharp bundles libzip internally:

| Source | Name in TPN | License Location |
|--------|------------|-----------------|
| libzip (in LibZipSharp NuGet) | nih-at/libzip | LibZipSharp NuGet `Licences/libzip/LICENSE` or https://github.com/nih-at/libzip/ (BSD-3-Clause) |

### Step 2 — Cross-reference

Compare the inventory against the current entries in `THIRD-PARTY-NOTICES.TXT`:
- **Missing entries**: Dependencies found in Step 1 but not in the TPN file
- **Stale entries**: TPN entries for dependencies no longer used
- **Incorrect info**: Wrong URLs, outdated license text

### Step 3 — Update the file

For each change:
1. Add/remove the TOC line (maintain sorted order and renumber)
2. Add/remove the license section (maintain sorted order)
3. For new entries, fetch the license text from the source URL or LICENSE file

### Step 4 — Verify

Run `grep -cP "^\d+\." THIRD-PARTY-NOTICES.TXT` (or `Select-String "^\d+\." THIRD-PARTY-NOTICES.TXT` on Windows) to confirm the entry count and ordering.
