# Shared libraries in .NET for Android applications

Applications package native libraries in the per-ABI `lib/<abi>/` directories
inside APKs, or `base/lib/<abi>/` inside AABs. Android and Google Play use these
directories to select libraries for the device's ABI. This includes .NET runtime
and BCL libraries, the .NET for Android host, application native dependencies,
and the data-only assembly store.

## Bootstrap and build tools

CoreCLR applications use the prebuilt native host (`libmonodroid.so` in the APK).
Generated `AppBootstrapConfig.java` supplies environment variables, runtime and
system properties, native-library preload settings, and whether an assembly
store is present. JNI remappings are packaged in a versioned binary asset.
There is no app-specific `libxamarin-app.so`, generated LLVM IR, native
compilation, or native linking in an ordinary CoreCLR build.

NativeAOT still compiles managed code with ILC and links the result with the
prebuilt Android NativeAOT host archive. It uses the official Android NDK's
`ld.lld`, `llvm-objcopy`, CRT objects, compiler runtime, and system-library
stubs. The Android workload does not redistribute those tools or NDK libraries.

The optional `AndroidStripNativeLibraries=true` packaging step uses the NDK's
`llvm-strip`. It creates intermediate copies without modifying the original
libraries, and fails explicitly if the NDK is absent or stripping fails.
The default is `false`, so ordinary CoreCLR Debug/Release and APK/AAB builds
need neither an NDK nor a bundled native toolchain.

## Why assembly stores are shared libraries

The trimmer and ReadyToRun compiler can produce different managed assemblies
for each ABI. Packaging those assemblies as ordinary assets would deliver all
ABIs' copies to every device. Instead, an [assembly store](AssemblyStores.md)
is packaged as `lib/<abi>/libassembly-store.so`, preserving Google Play's
per-ABI splitting and Android's native-library installation behavior.

These files are real ELF shared libraries, not blobs with renamed extensions.
CoreCLR uses stores when assemblies are embedded in the application. Fast
Deployment does not wrap its separately deployed assemblies; the obsolete
discrete-assembly ELF stub pipeline is no longer needed.

## Assembly store payload library

The prebuilt host cannot have a fixed `DT_NEEDED` dependency on the store
because Fast Deployment builds do not ship one. Instead, the store payload is
in an allocatable ELF section (`SHF_ALLOC`, covered by `PT_LOAD`) and is
referenced by the exported `_assembly_store` symbol. At startup the host calls
`dlopen("libassembly-store.so", ...)` and `dlsym(handle, "_assembly_store")`.
The Android linker maps the payload; the host does not scan the APK or parse
ELF section headers.

Only `_assembly_store` is exported. The runtime trusts the build-generated
store, checking its format and internal metadata without an end-pointer-based
payload-length check. The symbol's standard ELF size remains available to
inspection tools.

[`DlopenAssemblyStoreGenerator`](../../src/Microsoft.Android.Build.Tasks/Utilities/DlopenAssemblyStoreGenerator.cs)
uses the managed
[`AssemblyStoreElfWriter`](../../src/Microsoft.Android.Build.Tasks/Utilities/AssemblyStoreElfWriter.cs)
to serialize a data-only `ET_DYN` image directly:

1. ELF and program headers, including one read-only `PT_LOAD`.
2. A dynamic symbol/string table, SysV hash table, SONAME and dynamic entries.
3. The unchanged store bytes, streamed into the `payload` section.
4. Non-allocated section names and section headers after the payload, outside
   `PT_LOAD`, so inspection and stripping tools can rewrite them.

No assembly source, object file, compiler, assembler or linker is needed.
The image has no relocations, native-library dependencies, executable segments
or writable load segments. Because the payload is allocated and referenced by
a dynamic symbol, it survives `strip`/`llvm-strip`.

The writer emits little-endian ELF32 for ARM/x86 and ELF64 for ARM64/x64.
The load segment and payload use 16 KiB alignment for 64-bit ABIs and 4 KiB
alignment for 32-bit ABIs. The 64-bit layout is compatible with both 4 KiB
and 16 KiB pages. APK entry alignment and uncompressed `.so` packaging are
separate responsibilities of the Android packaging tools.

## Inspecting an assembly store

The following NDK commands are useful for inspection but are not used to
build the store:

```shell
llvm-readelf --file-header --section-headers libassembly-store.so
llvm-readelf --program-headers libassembly-store.so
llvm-readelf --dyn-symbols libassembly-store.so
```

For a 4096-byte ARM64 sample, the `payload` section has virtual address and
file offset `0x4000`, size `0x1000`, and the `A` flag:

```text
[Nr] Name       Type       Address          Off    Size   ES Flg Lk Inf Al
[ 5] payload    PROGBITS   0000000000004000 004000 001000 00   A  0   0 16384
```

The read-only load segment covers the header, dynamic metadata and payload:

```text
Type  Offset   VirtAddr           PhysAddr           FileSiz  MemSiz   Flg Align
LOAD  0x000000 0x0000000000000000 0x0000000000000000 0x005000 0x005000 R   0x4000
```

The dynamic symbol table has the required null entry and one payload symbol:

```text
Num:    Value          Size Type    Bind   Vis       Ndx Name
  0: 0000000000000000     0 NOTYPE  LOCAL  DEFAULT   UND
  1: 0000000000004000  4096 OBJECT  GLOBAL DEFAULT     5 _assembly_store
```

`dlsym()` adds the library's load bias to the symbol value and returns the
payload's process address. Real stores have larger payloads, but the section's
allocated flag, read-only load segment and exported symbol follow this layout.

To extract the unchanged raw store, whose header starts with `XABA`:

```shell
llvm-objcopy --dump-section=payload=payload.bin libassembly-store.so
hexdump -c -n 4 payload.bin
```
