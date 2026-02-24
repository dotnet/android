# NDK Dependency and LLVM Toolchain: Decision History

This document traces the decision-making history behind two related architectural shifts in
dotnet/android:

1. **Bundling a custom LLVM toolchain** (`llc`, `llvm-mc`, `lld`, etc.) instead of relying on
   external tools
2. **Progressively removing the Android NDK requirement** for end-user builds

These changes happened incrementally over several years (2019--2026) and were driven by concrete
problems documented in commit messages. This document captures the *rationale* behind each
decision. For technical details on how the LLVM tools are used today, see
[`llvm-tools-usage.md`](../../llvm-tools-usage.md) in the repository root.


## Why Remove the NDK Dependency?

The Android NDK was originally required for AOT compilation, profiled AOT, linking, and stripping
native libraries. Over time, eight categories of problems motivated its removal:

### 1. Download Size

The NDK is a ~700--800 MB download that expands to ~3 GB installed. The bundled LLVM toolchain
replaces the needed functionality with ~8--20 MB of binaries.

> "Building Apps with Profiled AOT requires the Android NDK to be installed, but apps which have
> Profiled AOT enabled do NOT require it at deployment time. The Android NDK, however, is roughly
> 700-800MB in size (downloads) and 3GB+ installed."
> --- commit `f0b1e8a53` (2019-07, PR #3098)

### 2. Build Failures When NDK Absent But Not Needed

Users hit `error XA5104: Could not locate the Android NDK` even in scenarios that did not
actually use the NDK. This was especially frustrating when the NDK was only needed for optional
features like AOT.

> "Currently, `$(AndroidNdkDirectory)` is 'required', in that if the value cannot be determined
> from the environment, `ResolveAndroidTooling.Execute()` will log an error. However, the NDK is
> no longer needed unless Profiled AOT or AOT are in use."
> --- commit `c96d9f496` (2021-10, PR #6310)

> "We should no longer be blocking the build if the NDK is not found, as long as
> `$(RunAOTCompilation)` is false."
> --- commit `936315597` (2020-12, PR #5451)

### 3. Constant NDK Version Churn

Every major NDK release broke something, requiring workarounds and version-specific code paths.
A partial list of breakage:

| NDK Release | Breakage |
|-------------|----------|
| r14--r16 | Unified headers transition; required `__ANDROID_API__` macro changes |
| r17 | Removed ARMv5 (`armeabi`) support |
| r19 | Windows `.cmd` wrapper scripts for `clang` had quoting bugs |
| r22 | Deprecated GNU Binutils; made LLD the default linker |
| r23 | **Removed GNU Binutils entirely** |
| r24 | Removed standalone `readelf`; changed env var handling |

The accumulation of these workarounds led to a full rewrite of the NDK abstraction layer:

> "New code uses proper class hierarchy to support all of the NDK quirks. Old code was a huge
> mess of `if` statements."
> --- commit `71ae55668` (2021-05, PR #5862)

### 4. Windows Filename Encoding Bugs

GNU Binutils 2.36 could not handle non-ASCII characters in file paths on Windows. This was a
blocking issue for users with non-Latin usernames or project paths.

> "Uses GNU binutils built on our own. Reason for doing it is a bug in version 2.36 [...] which
> made our tests fail with paths containing 'special' (i.e. non-ASCII) characters."
> --- commit `fd5f31cc8` (2021-10, PR #6297)

The switch to the LLVM-based toolchain (`llvm-mc`, `lld`) resolved this class of problems
entirely.

### 5. macOS Compatibility

Older NDK versions shipped 32-bit tools that became incompatible with macOS Catalina (which
dropped 32-bit support). NDK binaries also had macOS notarization issues.

> "macOS Catalina removed the ability to run 32-bit applications, but the Android NDK r16b
> includes 32-bit utilities."
> --- commit `f0b1e8a53` (2019-07, PR #3098)

> "[Workaround for] NDK component notarization issues on macOS."
> --- commit `e0882ee7f` (2020-07)

### 6. CI/Infrastructure Pain

NDK downloads caused timeouts, `OutOfMemoryException` from parallel downloads, and added ~3 GB
of unnecessary test dependencies.

> "Don't require the NDK for running unit tests; this should reduce the CI machine requirements
> by ~3GB."
> --- commit `9675c26ef` (2022-07)

> "[Fix] `OutOfMemoryException` when downloading NDK components in parallel."
> --- commit `c9037b913` (2019-08)

### 7. Upstream Deprecation of GNU Binutils

When Android NDK r22 deprecated GNU Binutils and r23 removed them entirely, the project was
forced to either adopt the NDK's LLD or build its own toolchain.

> "NDK r23 is to remove GNU Binutils entirely [...] To that end, we need to build our own
> binutils."
> --- commit `fc3f02826` (2021-05, PR #5856)

### 8. NDK Not Needed for Most User Scenarios

With the gradual shift to bundled tools, the NDK became unnecessary for the vast majority of
builds. Keeping it as a requirement imposed an unnecessary burden on developers.

> "The NDK isn't needed when using the AOT and LLVM option (`$(AotAssemblies)` and
> `$(EnableLLVM)`), as we ship all needed `binutils`."
> --- commit `346a93301` (2022-09, PR #7375)


## Timeline of Key Changes

| Date | Commit | Change | Motivation |
|------|--------|--------|------------|
| 2019-07 | `f0b1e8a53` | Bundle NDK tools (`ld`, `strip`) for Profiled AOT | Avoid requiring full NDK download for end users |
| 2019-12 | `decfbccf3` | Embed app data in `libxamarin-app.so` | Startup performance; first motivation for native code generation |
| 2020-07 | `e0882ee7f` | NDK notarization workaround | macOS notarization failures |
| 2020-12 | `936315597` | Stop blocking builds without NDK | Users hit errors when NDK wasn't needed |
| 2021-05 | `71ae55668` | Rewrite `NdkUtils` class hierarchy | Previous code was unmaintainable |
| 2021-05 | `fc3f02826` | Build own GNU Binutils (2.36) | NDK r23 announced removal of GNU Binutils |
| 2021-07 | `8ca289588` | Bump to GNU Binutils 2.37 | Keep up with releases |
| 2021-10 | `fd5f31cc8` | Downgrade to GNU Binutils 2.35.2 | v2.36 had Windows filename encoding bugs |
| 2021-10 | `c96d9f496` | Make `$(AndroidNdkDirectory)` optional | NDK no longer needed for most builds |
| **2022-03-17** | **`b21cbf943`** | **Switch to LLVM-based toolchain** | Windows encoding bugs; LLVM handles them correctly. Introduces `llc`, `llvm-mc`, `lld`, and the `gas.cc` wrapper. |
| 2022-03-28 | `3d088a29c` | Revert LLVM toolchain switch | APK size increase concerns |
| **2022-03-29** | **`5271f3e10`** | **Switch from hand-written asm to LLVM IR** | Architecture portability; ABI correctness; first active use of `llc` |
| 2022-04-15 | `16bbf6f0b` | First LLVM-versioned binutils bump (14.0.1) | Confirms toolchain is now LLVM-based |
| 2022-09-08 | `346a93301` | NDK no longer needed for AOT+LLVM | Bundled tools cover all AOT scenarios |
| 2023-03-15 | `0e4c29aab` | Create stub `libc.so`/`libm.so` | Linker no longer needs NDK sysroot; final piece for NDK-free end-user builds |
| 2026-01-16 | `a1c7ecc45` | NativeAOT uses bundled `ld.lld` instead of NDK clang | Eliminates NDK requirement for NativeAOT linking |


## From GNU Binutils to LLVM

The transition from GNU Binutils to LLVM happened in three phases:

### Phase 1: Using NDK's GNU Binutils (pre-2021)

Initially, the project relied on the Android NDK's bundled GNU Binutils (`as`, `ld`, `strip`,
`objcopy`). These were standard components of the NDK and worked with the Mono AOT compiler's
expectations.

### Phase 2: Building Own GNU Binutils (2021)

When Android NDK r22 deprecated and r23 removed GNU Binutils, the project built its own from
source (initially 2.36, bumped to 2.37, then downgraded to 2.35.2 due to Windows bugs):

> "NDK r22 deprecated GNU Binutils. [...] It was possible to 'cheat' and use GNU Binutils from
> an older NDK to work with a newer NDK's sysroot, but even this workaround was eliminated in
> NDK r23."
> --- commit `fc3f02826`

### Phase 3: Switching to LLVM (2022)

The persistent Windows filename encoding bugs in GNU Binutils motivated a switch to LLVM-based
tools. This was introduced in commit `b21cbf943` (PR #6683):

> "As mono/mono & dotnet/runtime AOT expects a GNU Binutils-compatible toolchain, it was
> necessary to implement a GNU Assembler (`gas`) wrapper around the LLVM `llvm-mc` assembler,
> so that command lines used by the Mono AOT compiler keep working fine."

The switch was briefly reverted (`3d088a29c`) due to APK size increase concerns, then
re-introduced alongside the move to LLVM IR generation (`5271f3e10`).


## The LLVM IR Decision

Commit `5271f3e10` (2022-03-29, PR #6853) replaced hand-written, architecture-specific assembly
with LLVM IR generated by a custom C# framework (`LlvmIrGenerator`). This made `llc` a central
build tool:

> "LLVM takes care for us of generating valid native assembler code, making sure it adheres to
> all the ABI requirements of the target platform. It also makes it easier, in the future, to
> generate actual executable code instead of just data as we do now."

The original motivation for embedding data in native shared libraries dates to 2019 (commit
`decfbccf3`, PR #2718), driven by startup performance:

> "None of this data is *actually* dynamic --- it's known beforehand and could be stored as
> static data right in the `.apk`. [...] This allows us to dispense with memory allocation,
> loading (potentially large) files from the `.apk` (lots of I/O, slow), parsing text files to
> set environment variables (additional memory allocation), etc."


## NDK Removal Was Incremental

A common misconception is that the LLVM toolchain introduction and NDK removal happened
simultaneously. In reality, they were separate, gradual processes:

1. **2019**: First bundling of NDK tools --- NDK still required for most scenarios
2. **2021**: NDK made optional for non-AOT builds
3. **2022-03**: LLVM toolchain introduced --- NDK still required for AOT+LLVM and for sysroot
   headers/libraries
4. **2022-09**: NDK no longer required for AOT+LLVM scenarios
5. **2023-03**: Stub `libc.so`/`libm.so` created --- NDK sysroot no longer needed for linking
6. **2026-01**: NativeAOT linking switched to bundled `ld.lld` --- NDK eliminated for NativeAOT

Each step removed one specific NDK dependency, driven by a concrete problem or opportunity.


## Current State

As of early 2026:

- **Bundled toolchain**: LLVM 18.1.6 from
  [`dotnet/android-native-tools`](https://github.com/dotnet/android-native-tools), version
  `L_18.1.6-8.0.0-1`
- **Tools shipped to end users**: `llc`, `llvm-mc`, `as` (gas wrapper), `ld` (lld), `llvm-strip`,
  `llvm-objcopy`
- **NDK for internal builds**: NDK r28c is still used internally (via CMake) to compile the C++
  native runtime (`libmonodroid.so`, `libnaot-android.so`), but is **not** required for end-user
  builds
- **NDK advertised to users**: Version `26.3.11579264` is hardcoded in distributed props files
  for users who install the NDK for other purposes

For technical details on how `llc` and `llvm-mc` are used in the build pipeline, see
[`llvm-tools-usage.md`](../../llvm-tools-usage.md).
