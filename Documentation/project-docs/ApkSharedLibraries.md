# Shared libraries in .NET for Android applications

Applications place ABI-specific shared libraries in the `lib/{ABI}/` directories
of APK archives, or `base/lib/{ABI}/` in AAB archives. These include the .NET
runtime, BCL platform libraries, ReadyToRun images, the .NET for Android native
host, and data payload libraries.

The CoreCLR host (`libmonodroid.so` in the APK) is precompiled and shared by all
applications. Application configuration and embedded managed assemblies are
packaged as data-only ELF libraries written directly by managed code. No
assembler, native linker, or objcopy executable is needed to create them.
NativeAOT compilation and optional native-library stripping use the official
Android NDK; ordinary CoreCLR packaging does not require the NDK.

## Assembly store payload library

Embedded CoreCLR assemblies, debug data, and assembly configuration are collected
into one [assembly store](AssemblyStores.md) per ABI. The raw XABA v3 store is
wrapped in `libassembly-store.so` by
[`WrapAssemblyStoresAsSharedLibraries`](../../src/Microsoft.Android.Build.Tasks/Tasks/WrapAssemblyStoresAsSharedLibraries.cs).
Its
[`AssemblyStoreElfWriter`](../../src/Microsoft.Android.Build.Tasks/Utilities/AssemblyStoreElfWriter.cs)
writes ELF headers and dynamic metadata and stream-copies the store bytes unchanged.

The payload lives in a read-only, **loadable** ELF section (`SHF_ALLOC`, covered
by a `PT_LOAD` segment), referenced by the exported `_assembly_store` symbol.
The runtime calls `dlopen("libassembly-store.so", ...)` and
`dlsym(handle, "_assembly_store")`; the dynamic linker maps the payload directly
from the APK. There is no ZIP scanning or runtime ELF section-header parsing.

Debug/FastDev builds which deploy individual managed assemblies do not ship an
assembly store. The prebuilt host therefore has no baked-in `DT_NEEDED` dependency
on the store library. Runtime assembly descriptors, decompression buffers, and
handles remain runtime-owned.

### ELF layout

The wrapper is a little-endian `ET_DYN` image with a single read-only `PT_LOAD`,
a read-only `PT_DYNAMIC`, `PT_PHDR`, and a non-executable `PT_GNU_STACK`.
The payload and load segment are aligned to 16 KiB on 64-bit ABIs and 4 KiB on
32-bit ABIs. ZIP-entry alignment is a separate packaging concern.
For `armeabi-v7a`, ELF flags `0x05000200` specify EABI5 and the base (softfp)
calling convention, not an ARMv5 instruction-set requirement.

The payload section is named `payload`, has a nonzero virtual address, and is
referenced by the default-visible global object symbol `_assembly_store`.
Its symbol size describes the payload for inspection; the runtime does not read
it. The native API trusts the build-generated XABA store and retains its existing
format, descriptor, and decompression checks. There is no `_assembly_store_end`,
external payload length, or replacement runtime parser.

Non-allocated section names and section headers follow the payload outside
`PT_LOAD`, so `strip`/`llvm-strip` can rebuild them without moving or corrupting
the mapped bytes.

### Inspection

The official Android NDK tools can inspect and extract the payload:

```shell
llvm-readelf --file-header --program-headers --section-headers --dyn-syms libassembly-store.so
llvm-objcopy --dump-section=payload=payload.bin libassembly-store.so
cmp payload.bin assembly-store.so
```

The extracted bytes are the original store, beginning with `XABA`.
The ELF inspection tests use the NDK's `llvm-readobj`, `llvm-nm`, `llvm-strip`,
and `llvm-objcopy`. They resolve the executing host's NDK from
`TEST_ANDROID_NDK_PATH`, then `ANDROID_NDK_LATEST_HOME`, then
`android-toolchain/ndk` under the user profile. A build-time
`$(AndroidNdkDirectory)` is only a fallback when it exists on that host.
Missing inspection tools fail explicitly; stream/input-validation tests require
no native tools.

## Bootstrap and remapping payload library

`libbinary_blobs.so` uses the same managed ELF writer to expose named data payloads.
The required CoreCLR `coreclr_bootstrap` symbol contains the XCFG application
configuration. The optional `remapping_data` symbol contains JNI remapping data
and is absent when there are no semantic mappings.

CoreCLR reads application configuration, environment variables, system properties,
runtime properties, and JNI preloads from XCFG into runtime-owned state. It does
not link to application configuration globals or a dummy `libxamarin-app` library.
The native build targets Android only; the deprecated desktop Designer targets
are no-ops and do not require a configuration stub. NativeAOT still initializes
its environment and system properties through its Java bootstrap.

The shared native loader owns `dlopen`/`dlsym`, BLBB envelope decoding, optional
Zstandard decompression, and process-lifetime storage. Managed remapping lookup
receives the existing single-pointer descriptor. The ELF layer does not change
the BLBBv1 envelope, XCFG body, or remapping body.
