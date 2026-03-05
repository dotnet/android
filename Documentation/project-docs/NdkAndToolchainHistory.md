# NDK Dependency and LLVM Toolchain: Decision History

dotnet/android bundles a custom LLVM toolchain (`llc`, `llvm-mc`, `lld`) and does not require
the Android NDK for end-user builds. These two facts are the result of incremental decisions
made over 2019--2026, each driven by a specific NDK problem. This document captures the
rationale.


## Why the NDK Became Untenable

The NDK was originally required for AOT compilation tooling (`as`, `ld`, `strip`) and sysroot
libraries. Four categories of problems drove its removal:

### Size and availability

The NDK is a ~700--800 MB download expanding to ~3 GB installed. The bundled LLVM toolchain
replaces the needed functionality in ~8--20 MB. Requiring the full NDK was especially painful
for CI (download timeouts, `OutOfMemoryException` from parallel downloads, ~3 GB of unnecessary
test dependencies) and for end users who only needed Profiled AOT:

> "Building Apps with Profiled AOT requires the Android NDK to be installed, but apps which have
> Profiled AOT enabled do NOT require it at deployment time. The Android NDK, however, is roughly
> 700-800MB in size (downloads) and 3GB+ installed."
> --- commit `f0b1e8a53` (2019-07, PR #3098)

Users also hit `error XA5104: Could not locate the Android NDK` even in scenarios that didn't
use the NDK at all:

> "Currently, `$(AndroidNdkDirectory)` is 'required', in that if the value cannot be determined
> from the environment, `ResolveAndroidTooling.Execute()` will log an error. However, the NDK is
> no longer needed unless Profiled AOT or AOT are in use."
> --- commit `c96d9f496` (2021-10, PR #6310)

### Version churn and breakage

Every major NDK release broke something, requiring version-specific workarounds:

| NDK Release | Breakage |
|-------------|----------|
| r14--r16 | Unified headers transition; required `__ANDROID_API__` macro changes |
| r17 | Removed ARMv5 (`armeabi`) support |
| r19 | Windows `.cmd` wrapper scripts for `clang` had quoting bugs |
| r22 | Deprecated GNU Binutils; made LLD the default linker |
| r23 | **Removed GNU Binutils entirely** |
| r24 | Removed standalone `readelf`; changed env var handling |

The accumulation of these workarounds made the NDK abstraction layer unmaintainable:

> "New code uses proper class hierarchy to support all of the NDK quirks. Old code was a huge
> mess of `if` statements."
> --- commit `71ae55668` (2021-05, PR #5862)

### Windows filename encoding

GNU Binutils 2.36 could not handle non-ASCII characters in file paths on Windows --- a blocking
issue for users with non-Latin usernames or project paths. This bug directly motivated the
switch to LLVM, which handles Unicode paths correctly:

> "Uses GNU binutils built on our own. Reason for doing it is a bug in version 2.36 [...] which
> made our tests fail with paths containing 'special' (i.e. non-ASCII) characters."
> --- commit `fd5f31cc8` (2021-10, PR #6297)

### macOS compatibility

Older NDK versions shipped 32-bit tools incompatible with macOS Catalina, and NDK binaries had
notarization issues (`e0882ee7f`).


## From GNU Binutils to LLVM

The transition happened in three phases:

**Phase 1 -- NDK's GNU Binutils (pre-2021).** The project used the NDK's bundled `as`, `ld`,
`strip`, and `objcopy`. These matched the Mono AOT compiler's expectations.

**Phase 2 -- Own GNU Binutils build (2021).** NDK r22 deprecated and r23 removed GNU Binutils.
The project built its own from source rather than switching to LLVM because GNU Binutils was a
drop-in replacement --- the Mono AOT compiler already expected a GNU `gas`-compatible assembler
interface, so no wrapper or pipeline changes were needed. The initial build used version 2.36,
bumped to 2.37, then downgraded to 2.35.2 when 2.36's Windows filename encoding bugs were
discovered.

**Phase 3 -- LLVM (2022).** The encoding bugs could not be easily fixed in the GNU Binutils
codebase, forcing a switch to LLVM. Even then, the switch was cautious --- initially only the
assembler and strip were replaced, keeping GNU `gold` for linking because `lld` was too large:

> "GNU Binutils have problems dealing with diacritic characters on Windows platform. Fixing the
> issue in the Binutils codebase is not straightforward, so we decided to use portions of the
> LLVM toolchain (`llvm-mc`, the assembler, and `llvm-objcopy` which doubles as `strip`) instead,
> since they don't have said problem with diacritics."
>
> "The initial plan was to switch the linker to LLVM's `lld` as well, but since its binary comes
> up at 70mb (twice that on mac, with two architectures) we decided to keep using GNU ld (`gold`)
> for the moment."
> --- `xamarin-android-binutils` commit `6d4e3bbc` (2022-01-28)

Because the Mono AOT compiler expects a GNU `gas`-compatible interface, a C++ wrapper (`gas.cc`
in `dotnet/android-native-tools`) was written to translate `gas` CLI arguments to `llvm-mc`
arguments. The full switch (including `lld` for linking) landed in commit `b21cbf943`
(2022-03-17, PR #6683), was briefly reverted (`3d088a29c`) due to APK size concerns, then
re-introduced alongside the move to LLVM IR.

### Why not use the NDK's LLVM?

The NDK has shipped its own LLVM/Clang since r18 (2018), so a natural question is why the
project built its own LLVM tools instead of using the NDK's. No single commit states this
explicitly, but the reasoning is clear from the overall trajectory:

1. **The whole point was to eliminate the NDK dependency.** The project had been working since
   2019 to remove the NDK requirement for end-user builds (`f0b1e8a53`: "will not require that
   the full Android NDK be installed"). Using the NDK's `llvm-mc` or `lld` would have preserved
   exactly the dependency they were trying to remove.

2. **The NDK doesn't ship standalone `llvm-mc`.** The NDK provides `clang` (which uses an
   integrated assembler internally) but not a standalone `llvm-mc` binary. The Mono AOT compiler
   shells out to a standalone assembler, so the NDK's clang-integrated assembler wasn't usable
   without modifying Mono itself.

3. **NDK LLVM version churn.** The NDK's LLVM version changes with every NDK release (e.g.,
   LLVM 11 in r22, LLVM 12 in r23, LLVM 14 in r25). This is exactly the version instability
   that caused repeated breakage with the NDK (see `accc846e3`, `f361d9978`, `b21267f4e`).
   Building their own LLVM gave version control and stability.

4. **The NDK's own tools had bugs too.** NDK r22's Windows `.cmd` wrapper scripts for `clang`
   had quoting bugs with spaces in paths (`NdkToolsWithClangNoBinutils` in
   `src/Xamarin.Android.Build.Tasks/Utilities/NdkTools/WithClangNoBinutils.cs` documents this
   workaround at length). The NDK's LLVM wasn't necessarily more reliable than the NDK's
   binutils had been.


## The LLVM IR Decision

Twelve days after the LLVM toolchain landed, commit `5271f3e10` (PR #6853) replaced
hand-written, architecture-specific assembly with LLVM IR generated by a custom C# framework
(`src/Xamarin.Android.Build.Tasks/Utilities/LlvmIrGenerator/`). This made `llc` a central build
tool:

> "LLVM takes care for us of generating valid native assembler code, making sure it adheres to
> all the ABI requirements of the target platform. It also makes it easier, in the future, to
> generate actual executable code instead of just data as we do now."

The original motivation for embedding data in native shared libraries dates to 2019 (`decfbccf3`,
PR #2718) --- startup performance. File I/O, parsing, and dynamic allocation at startup were
replaced with statically-linked data accessed via `dlopen`.


## NDK Removal Was Incremental

The LLVM toolchain introduction and NDK removal were separate, gradual processes --- not a single
switch:

1. **2019**: First bundling of NDK tools --- NDK still required for most scenarios
2. **2021**: NDK made optional for non-AOT builds
3. **2022-03**: LLVM toolchain introduced --- NDK still required for AOT+LLVM and for sysroot
4. **2022-09**: NDK no longer required for AOT+LLVM (`346a93301`)
5. **2023-03**: Stub `libc.so`/`libm.so` created --- linker no longer needs NDK sysroot (`0e4c29aab`)
6. **2026-01**: NativeAOT linking switched to bundled `ld.lld` (`a1c7ecc45`)


## Timeline

| Date | Commit | Change | Motivation |
|------|--------|--------|------------|
| 2019-07 | `f0b1e8a53` | Bundle NDK tools (`ld`, `strip`) for Profiled AOT | Avoid requiring full NDK download |
| 2019-12 | `decfbccf3` | Embed app data in `libxamarin-app.so` | Startup performance; first native code generation |
| 2020-12 | `936315597` | Stop blocking builds without NDK | Users hit errors when NDK wasn't needed |
| 2021-05 | `71ae55668` | Rewrite `NdkUtils` class hierarchy | Previous code unmaintainable |
| 2021-05 | `fc3f02826` | Build own GNU Binutils | NDK r23 removing GNU Binutils |
| 2021-10 | `fd5f31cc8` | Downgrade to GNU Binutils 2.35.2 | v2.36 Windows filename encoding bugs |
| 2021-10 | `c96d9f496` | Make `$(AndroidNdkDirectory)` optional | NDK no longer needed for most builds |
| **2022-03-17** | **`b21cbf943`** | **Switch to LLVM-based toolchain** | Windows encoding bugs; introduces `llc`, `llvm-mc`, `lld`, `gas.cc` wrapper |
| 2022-03-28 | `3d088a29c` | Revert LLVM toolchain switch | APK size increase concerns |
| **2022-03-29** | **`5271f3e10`** | **Switch to LLVM IR generation** | Architecture portability; ABI correctness; first active use of `llc` |
| 2022-09-08 | `346a93301` | NDK no longer needed for AOT+LLVM | Bundled tools cover all AOT scenarios |
| 2023-03-15 | `0e4c29aab` | Create stub `libc.so`/`libm.so` | Linker no longer needs NDK sysroot |
| 2026-01-16 | `a1c7ecc45` | NativeAOT uses bundled `ld.lld` | Eliminates NDK for NativeAOT linking |


## Current State

The bundled toolchain ships `llc`, `llvm-mc`, `as` (gas wrapper), `ld` (lld), `llvm-strip`, and
`llvm-objcopy`, built from [`dotnet/android-native-tools`](https://github.com/dotnet/android-native-tools).
The LLVM version and toolchain package version are defined in
`build-tools/xaprepare/xaprepare/ConfigAndData/Configurables.cs`.

The NDK is still used internally (via CMake) to compile the C++ native runtime, but is **not**
required for end-user builds. The internal NDK version is configured in
`build-tools/xaprepare/xaprepare/ConfigAndData/BuildAndroidPlatforms.cs`.
