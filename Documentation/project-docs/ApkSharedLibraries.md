# Shared libraries in .NET for Android applications

Applications contain a number of shared libraries which are placed in the
per-rid directories inside APK/AAB archives (`lib/ABI/lib*.so`).  The libraries
have different purposes and come from different sources:

  1. .NET PAL (Platform Abstraction Layer), used by various Base Class Library
     assemblies.
  2. .NET runtime (`libmonosgen-2.0.so` containing the Mono VM)
  3. AOT images (`libaot*.so`, containing pre-JITed **data** which is loaded by
     MonoVM at runtime and processed to turn into executable code)
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

## Data payload stub library

ELF binaries consist of a number of sections, which contain code, data (read-only and read-write), debug symbols etc.
However, the ELF specification doesn't dictate names of any of those sections and, thus, developers are free to lay out
ELF binaries any way they see fit, as long as the binary conforms to the ELF specification and the operating system
requirements.  This gave us the idea of placing our data files (assemblies, assembly stores, debug data, config files etc)
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

### Layout of the payload library

In order to examine content of our "payload" ELF shared library, one can run the `llvm-readelf` utility which is
shipped with the Android NDK (and also part of native developer tools on macOS and Linux distributions which have
the LLVM Clang toolchain installed), or the `readelf` utility which is part of GNU binutils.

File used in the samples below is the `.NET for Android` assembly store, wrapped in an ELF image for the Arm64
(`AArch64`) architecture.

The first command verifies that the file is a valid ELF image and shows the header information, including the
target platform/abi/machine:

```shell
$ llvm-readelf --file-header libassemblies.arm64-v8a.blob.so
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
$ llvm-readelf --section-headers libassemblies.arm64-v8a.blob.so
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
$ llvm-objcopy --dump-section=payload=payload.bin libassemblies.arm64-v8a.blob.so
$ ls -gG payload.bin
-rw-rw-r-- 1 833095 Sep 12 11:32 payload.bin
```

To verify the size is correct, we can convert the section size indicated in the section headers
output from hexadecimal to decimal:

```shell
$ printf "%d\n" 0x0cb647
833095
```

In this case, the payload file is an assembly store, which should have its first 4 bytes read
`XABA`, we can verify this with the following command:

```shell
$ hexdump -c -n 4 payload.bin
0000000   X   A   B   A
0000004
```
