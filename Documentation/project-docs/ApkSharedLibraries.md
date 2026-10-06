# Shared libraries in .NET for Android applications

Applications contain a number of shared libraries which are placed in the
per-rid directories inside APK/AAB archives (`lib/ABI/lib*.so`).  The libraries
have different purposes and come from different sources:

  1. .NET PAL (Platform Abstraction Layer), used by various Base Class Library
     assemblies.
  2. .NET runtime
  3. ReadyToRun images
  4. .NET for Android runtime and support libraries
  5. .NET for Android data payload libraries

Most of those libraries have fairly obvious purpose and layout, this document
focuses on `.NET for Android` data payload libraries.

# `.NET for Android` data payload libraries

## Android packaging introduction

Android allows applications to ship ABI-specific code inside the APK/AAB archives in
order to enable applications which need some sort of native code, while otherwise written
in a managed language like C#, Java or Kotlin.  These libraries must be compiled to target
the platforms supported by Android and they must somehow co-exist in the same APK/AAB
archive (they always have the same name, just target a different platform/ABI).  The way
chosen by Android to implement it is to place the per-ABI libraries in the `lib/{ABI}/`
directory of the archive.

All of the libraries placed in the `lib/{ABI}` directories are expected to be ELF shared
library images, as required by the Android Linux kernel.

## .NET for Android runtime, libraries and data

`.NET for Android` runtime is composed of two libraries, one being the pre-compiled runtime
itself (`libmonodroid.so` in the APK) and another library being built together with the
application, containing application-specific dynamically generated code (`libxamarin-app.so`
in the APK).  These two libraries together contain all the code and data to make the application
run properly on all the supported targets.

In addition to the above, `.NET for Android` ships a number of managed assemblies.  For a number
of years (starting with `Mono for Android`, through `Xamarin.Android`), all the assemblies had
been completely platform agnostic and, thus, were shipped in a custom directory in the APK archive
named `assemblies/`.  However, at some point during transition to `dotnet/runtime` and its BCL, a
handful of managed libraries became platform specific and, thus, had to be shipped in a way that took
the platform requirement into account.  As all those libraries shared the same name across platforms,
we had to find a way to package them so that they wouldn't conflict with each other.  Thus the
`assemblies/` directory gained a subdirectory per ABI, which contained the platform specific assemblies.
Later on, the same was implemented in [assembly stores](AssemblyStores.md) - they would contain both kinds
of managed assemblies.

The downside of packaging all the assemblies (or assembly stores) in the `assemblies/` directory was that
all the platforms would get copies of platform specific assemblies for the other supported ABIs, thus wasting
storage on the end user devices.

Introduction of platform specific assemblies posed another problem.  We discovered that in some instances, the
dotnet linker/trimmer would generate assemblies that might fail on certain platforms without us having any
prior warning.  The solution to this was to make **all** the assemblies platform specific, making sure that
whatever the trimmer did, we'd always have the correct assembly loaded on the right platform.

Making all assemblies platform specific, however, poses a problem of APK/AAB size - all of the assemblies would
exist in X copies and we couldn't allow such a big increase of archive size.  Thus, all the assemblies (and also
assembly stores as well as a runtime configuration blob file) were moved to the `lib/{ABI}/` directories and
"masqueraded" as ELF shared libraries, by giving them the `lib*.so` names.  However, the files were still managed
assemblies, not valid ELF images.

Earlier this year, however, Google [announced](https://android-developers.googleblog.com/2024/08/adding-16-kb-page-size-to-android.html) that
Android 15 will enable shared libraries aligned to 16k instead of the "traditional" 4k and, at some point, the alignment
will become a requirement for submission to the Play Store.  This made us suspect that the libraries in `lib/{ABI}/` will
be actually verified to be valid ELF images at some point and we decided to proactively turn our data files shipped in
those directories into actual ELF shared libraries.  The way it is done is described in the following section.

## Discrete data payload stub library

> **Note:** this layout is used for discrete managed payload files.
> Assembly stores use a loadable shared library instead, as described
> in [Assembly store payload library](#assembly-store-payload-library).

ELF binaries consist of a number of sections, which contain code, data (read-only and read-write), debug symbols etc.
However, the ELF specification doesn't dictate names of any of those sections and, thus, developers are free to lay out
ELF binaries any way they see fit, as long as the binary conforms to the ELF specification and the operating system
requirements.  This gave us the idea of placing discrete data files (assemblies, debug data, config files etc)
in a custom section inside the ELF image.  The resulting file would pass any verification Android will perform at some
point and, at the same time, it won't slow down our operation because we can still load data directly from the shared
library (by using the `mmap(2)` Unix call) without having to load the ELF image into memory.

To implement that, we added to our distribution a "stub" of a shared ELF library, which is essentially a small, valid
but otherwise empty ELF image.  This stub is built together with the rest of the `.NET for Android` runtime and its
layout is discovered and remembered, so that at runtime we can quickly move to the location where our data lives and
load it as we see fit.  The runtime `mmap`s the entire file, looks at the file header and finds the start of payload
section, then stores that location in a pointer for further use.

The way the data is placed in the ELF image is by appending a new section, called `payload`, to the stub binary at
application build time.  This is done by using the `llvm-objcopy` utility, which we ship, and then the result is
packaged into the `lib/{ABI}/` directory.  The section is properly aligned, the entire file is a valid ELF image.

One downside of this approach is that if one were to run the `llvm-strip` or `strip` utility on the resulting
shared libray, the `payload` section (as it uses a "non-standard" name) would be considered by the strip utility
to be unnecessary and summarily removed.

### Assembly store payload library

The runtime uses a different, simpler mechanism for assembly stores. Its prebuilt native host (`libmonodroid.so`) is
shared by every application and build configuration, so it cannot rely on a baked-in `DT_NEEDED` dependency on the
assembly store (Debug/FastDev builds don't ship one).  Instead, the assembly store wrapper library is produced so
that the store payload lives in a **loadable** ELF section (`SHF_ALLOC`, covered by a `PT_LOAD` segment) that is
pointed at by an exported dynamic symbol named `_assembly_store`.  At runtime the host simply calls
`dlopen("libassembly-store.so", …)` followed by `dlsym(handle, "_assembly_store")` and lets the dynamic linker
locate and map the payload out of the APK — there is no ZIP scanning and no manual ELF section-header parsing.

Because the section is allocatable and referenced by a dynamic symbol, this layout survives `strip`/`llvm-strip`.

This wrapper is produced by
[`WrapAssemblyStoresAsSharedLibraries`](../../src/Microsoft.Android.Build.Tasks/Tasks/WrapAssemblyStoresAsSharedLibraries.cs)
in the modern `Microsoft.Android.Build.Tasks` assembly. Its
[`AssemblyStoreElfWriter`](../../src/Microsoft.Android.Build.Tasks/Utilities/AssemblyStoreElfWriter.cs)
writes the ELF headers and dynamic metadata directly in managed code and stream-copies the raw store
bytes unchanged. No assembler, linker, native dependency or executable code is needed for this wrapper.
The existing assembly-store setting selects this task unconditionally for stores; discrete files
continue to use `DSOWrapperGenerator` and `llvm-objcopy`. Other application-specific LLVM generation
and bundled native tools are unaffected.

### CoreCLR application bootstrap and native libraries

CoreCLR applications generate `net/dot/android/AppBootstrapConfig.java` instead of embedding
environment variables, bundled system properties, CoreCLR hosting and package settings,
and the native library cache in `environment.<abi>.ll`. The fixed `libmonodroid.so` host reads the Java
configuration through JNI when it loads. The build task encodes the strings once as
NUL-terminated UTF-8 in a Java `byte[]`, with an `int[]` containing the environment,
system-property, runtime-property and library counts followed by string offsets (package
name first, then each group in that order). The host makes one bulk copy of those bytes
into process-lifetime native storage and constructs the pointer tables CoreCLR requires
from the offsets; it performs no JNI string conversion or per-string copy.
Literal byte-array initializers become DEX bulk array-data payloads, rather than
per-byte Java assignments. Blobs larger than 4 KiB use bounded initializer methods and
one Java chunk-concatenation step to stay below the per-method bytecode limit.
Java retains library names as strings for JNI-aware loading.
Its native library state records the canonical
packaged name, whether the library has `JNI_OnLoad`, the preload policy, and the loaded handle.
Library aliases (`lib` prefix, `.so` suffix, and `.dll.so` variants) are resolved against
those records when loading, without a generated hash/index array. Java preloads the
JNI libraries after CoreCLR initialization; non-preloaded JNI libraries are still loaded
with `JNI_OnLoad` support on demand. The CoreCLR R8 keep rules preserve the Java entry points
used by JNI.

The per-ABI `environment.<abi>.ll` object is still generated for assembly-store runtime
state, and the separate compression and JNI-remapping native objects are unchanged. This
does not remove LLVM/llc, native-stub linkage, or the bundled native toolchain.

The writer's ELF inspection tests use the Android NDK's `llvm-readobj`, `llvm-nm`,
`llvm-strip`, and `llvm-objcopy`, with no additional managed ELF parser dependency.
These tests are categorized as `RequiresAndroidNdk` and resolve the NDK on the
executing host: `TEST_ANDROID_NDK_PATH`, then `ANDROID_NDK_LATEST_HOME`, then
`android-toolchain/ndk` under that host's user profile. A build-time
`$(AndroidNdkDirectory)` is only a final fallback if it still exists there.
The existing CI setup installs the NDK before these tests; no additional install
step is required. A missing toolchain still fails explicitly rather than silently
skipping inspection, while the stream/input-validation tests need no native tools.
The .NET 11 test runner uses `Process.RunAndCaptureText` to drain stdout and stderr
together, with one timeout covering output capture and process exit. The shared
.NET 10 packaging tests use cancellation-aware concurrent reads with the same bound.

The wrapper is a little-endian `ET_DYN` image with a single read-only `PT_LOAD` segment,
a read-only `PT_DYNAMIC` segment, `PT_PHDR`, and a non-executable `PT_GNU_STACK`.
The payload and load segment use 16 KiB alignment on 64-bit ABIs and 4 KiB on 32-bit ABIs.
For `armeabi-v7a`, ELF flags `0x05000200` specify EABI5 and the base (softfp) calling convention,
not an ARMv5 instruction-set requirement. ZIP-entry alignment is a separate packaging concern.

Only `_assembly_store` is exported, as a default-visible global object symbol. Its standard ELF
symbol size describes the payload for inspection; the runtime does not read it.
The existing native API trusts the build-generated XABA v3 store: there is no `_assembly_store_end`,
external payload length, replacement size field, or runtime ELF-header walk. Existing format,
descriptor and decompression checks remain unchanged; this is not a parser for untrusted store bytes.
The section is still named `payload`, so the extraction command
(`llvm-objcopy --dump-section=payload=...`) shown below works for both layouts.

### Layout of the discrete (stub) payload library

> **Note:** the sample output in this section is the discrete payload layout, produced by `DSOWrapperGenerator`.
> Its `payload` section is **non-loadable** (no `A` flag, `Address` `0`) and there is no `_assembly_store`
> dynamic symbol. For the assembly store layout see the [next section](#layout-of-the-assembly-store-payload-library).

In order to examine content of our "payload" ELF shared library, one can run the `llvm-readelf` utility which is
shipped with the Android NDK (and also part of native developer tools on macOS and Linux distributions which have
the LLVM Clang toolchain installed), or the `readelf` utility which is part of GNU binutils.

File used in the samples below is a discrete managed assembly, wrapped in an ELF image for the Arm64
(`AArch64`) architecture.

The first command verifies that the file is a valid ELF image and shows the header information, including the
target platform/abi/machine:

```shell
$ llvm-readelf --file-header lib_Test.dll.so
ELF Header:
  Magic:   7f 45 4c 46 02 01 01 00 00 00 00 00 00 00 00 00
  Class:                             ELF64
  Data:                              2's complement, little endian
  Version:                           1 (current)
  OS/ABI:                            UNIX - System V
  ABI Version:                       0
  Type:                              DYN (Shared object file)
  Machine:                           AArch64
  Version:                           0x1
  Entry point address:               0x0
  Start of program headers:          64 (bytes into file)
  Start of section headers:          849480 (bytes into file)
  Flags:                             0x0
  Size of this header:               64 (bytes)
  Size of program headers:           56 (bytes)
  Number of program headers:         8
  Size of section headers:           64 (bytes)
  Number of section headers:         11
  Section header string table index: 9
```

The second command lists the sections contained within the ELF image, their alignment, sizes and offsets
into the file where the sections begin:

```shell
$ llvm-readelf --section-headers lib_Test.dll.so
There are 11 section headers, starting at offset 0xcf648:

Section Headers:
  [Nr] Name              Type            Address          Off    Size   ES Flg Lk Inf Al
  [ 0]                   NULL            0000000000000000 000000 000000 00      0   0  0
  [ 1] .note.gnu.build-id NOTE           0000000000000200 000200 000024 00   A  0   0  4
  [ 2] .dynsym           DYNSYM          0000000000000228 000228 000030 18   A  5   1  8
  [ 3] .gnu.hash         GNU_HASH        0000000000000258 000258 000020 00   A  2   0  8
  [ 4] .hash             HASH            0000000000000278 000278 000018 04   A  2   0  4
  [ 5] .dynstr           STRTAB          0000000000000290 000290 000032 00   A  0   0  1
  [ 6] .dynamic          DYNAMIC         00000000000042c8 0002c8 0000b0 10  WA  5   0  8
  [ 7] .relro_padding    NOBITS          0000000000004378 000378 000c88 00  WA  0   0  1
  [ 8] .data             PROGBITS        0000000000008378 000378 000001 00  WA  0   0  1
  [ 9] .shstrtab         STRTAB          0000000000000000 000379 00005e 00      0   0  1
  [10] payload           PROGBITS        0000000000000000 004000 0cb647 00      0   0 16384
Key to Flags:
  W (write), A (alloc), X (execute), M (merge), S (strings), I (info),
  L (link order), O (extra OS processing required), G (group), T (TLS),
  C (compressed), x (unknown), o (OS specific), E (exclude),
  R (retain), p (processor specific)
```

Of interest to us is the presence of the `payload` section, its starting offset (it will usually
be `0x4000`, that is 16k into the file but it might be a multiple of the value, if the stub ever
grows) and its size will, obviously, differ depending on the payload.

The information above is sufficient to verify that the file is valid `.NET for Android` payload
shared library.

In order to extract payload from the ELF image, one can use the following command:

```shell
$ llvm-objcopy --dump-section=payload=payload.bin lib_Test.dll.so
$ ls -gG payload.bin
-rw-rw-r-- 1 833095 Sep 12 11:32 payload.bin
```

To verify the size is correct, we can convert the section size indicated in the section headers
output from hexadecimal to decimal:

```shell
$ printf "%d\n" 0x0cb647
833095
```

The extracted payload should match the original assembly:

```shell
$ cmp payload.bin Test.dll
```

### Layout of the assembly store payload library

The assembly store wrapper differs from the discrete payload stub in two ways that matter to the runtime:

  1. The `payload` section is **allocatable** (`SHF_ALLOC`, shown as the `A` flag) and is assigned a
     virtual address, so it is covered by a `PT_LOAD` program header and mapped into memory by the
     dynamic linker as part of `dlopen()`.
  2. An exported dynamic symbol, `_assembly_store`, points at the beginning of the payload, so the runtime
     can retrieve the payload address with a single `dlsym()` call.

The section header listing shows the `payload` section carrying the `A` flag and a non-zero address (compare
with the discrete payload listing above, where `payload` has no flags and address `0`):

```shell
$ llvm-readelf --section-headers libassembly-store.so
Section Headers:
  [Nr] Name              Type            Address          Off    Size   ES Flg Lk Inf Al
  [ 0]                   NULL            0000000000000000 000000 000000 00      0   0  0
  [ 1] .dynsym           DYNSYM          0000000000000120 000120 000030 18   A  2   1  8
  [ 2] .dynstr           STRTAB          0000000000000150 000150 000026 00   A  0   0  1
  [ 3] .hash             HASH            0000000000000178 000178 000014 04   A  1   0  4
  [ 4] .dynamic          DYNAMIC         0000000000000190 000190 000070 10   A  2   0  8
  [ 5] payload           PROGBITS        0000000000004000 004000 000004 00   A  0   0 16384
  [ 6] .shstrtab         STRTAB          0000000000000000 004004 000032 00      0   0  1
```

The non-allocated section names and section headers follow the payload, outside `PT_LOAD`,
so `llvm-strip` can rebuild them without moving or corrupting the loadable bytes.

The program headers confirm that the `payload` section is part of a read-only `PT_LOAD` segment, i.e. the
dynamic linker maps it for us:

```shell
$ llvm-readelf --program-headers libassembly-store.so
Program Headers:
  Type           Offset   VirtAddr           PhysAddr           FileSiz  MemSiz   Flg Align
  PHDR           0x000040 0x0000000000000040 0x0000000000000040 0x0000e0 0x0000e0 R   0x8
  LOAD           0x000000 0x0000000000000000 0x0000000000000000 0x004004 0x004004 R   0x4000
  DYNAMIC        0x000190 0x0000000000000190 0x0000000000000190 0x000070 0x000070 R   0x8
  GNU_STACK      0x000000 0x0000000000000000 0x0000000000000000 0x000000 0x000000 RW  0x8

 Section to Segment mapping:
  Segment Sections...
   01     .dynsym .dynstr .hash .dynamic payload
   02     .dynamic
```

Finally, the dynamic symbol table exposes `_assembly_store`, whose value (`0x4000`) is the virtual address of
the `payload` section — this is exactly what `dlsym(handle, "_assembly_store")` returns at runtime:

```shell
$ llvm-readelf --dyn-symbols libassembly-store.so
Symbol table '.dynsym' contains 2 entries:
   Num:    Value          Size Type    Bind   Vis       Ndx Name
     0: 0000000000000000     0 NOTYPE  LOCAL  DEFAULT   UND
     1: 0000000000004000     4 OBJECT  GLOBAL DEFAULT     5 _assembly_store
```

(The offsets and sizes above come from a tiny sample payload; a real assembly store's `payload` section will
be much larger, but the flags, `PT_LOAD` membership and the `_assembly_store` symbol are what identify a valid
assembly store wrapper.)

Extraction works the same as for the discrete payload layout, since the section is still called `payload`:

```shell
$ llvm-objcopy --dump-section=payload=payload.bin libassembly-store.so
$ hexdump -c -n 4 payload.bin
0000000   X   A   B   A
0000004
```
