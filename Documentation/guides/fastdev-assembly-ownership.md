# CoreCLR Fast Deployment assembly ownership

`FastDevAssemblies::open_assembly` owns the bytes it returns through
`Host::clr_external_assembly_probe`. The pointer is borrowed by CoreCLR, not
transferred to it. Do not free it on callback return, runtime initialization,
assembly-load completion, or collectible `AssemblyLoadContext` unload.

## Runtime contract

The runtime source pinned by `eng/Version.Details.xml` is
[`dotnet/dotnet` commit `846769e14f918f16ee85e7055c72fcc2f6b141bb`](https://github.com/dotnet/dotnet/tree/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime).
These are the relevant implementation contracts:

- [`host_runtime_contract.h`](https://github.com/dotnet/dotnet/blob/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime/src/native/corehost/host_runtime_contract.h)
  exposes an external probe returning a pointer and length, with no release callback.
- [`AssemblyProbeExtension::Probe`](https://github.com/dotnet/dotnet/blob/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime/src/coreclr/vm/assemblyprobeextension.cpp)
  passes those values into `ProbeExtensionResult::External`; it does not copy the bytes.
- [`FlatImageLayout::FlatImageLayout`](https://github.com/dotnet/dotnet/blob/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime/src/coreclr/vm/peimagelayout.cpp#L748-L757)
  explicitly states "We do not manage the data" and initializes its decoders directly
  with the external pointer. This is different from the byte-array constructor,
  which copies into a runtime-owned mapping.
- [`PEImage::OpenImage`](https://github.com/dotnet/dotnet/blob/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime/src/coreclr/vm/peimage.inl#L324-L358)
  caches images by path and probe type. A cache hit ignores the new probe data.
  [`AssemblyBinderCommon`](https://github.com/dotnet/dotnet/blob/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime/src/coreclr/binder/assemblybindercommon.cpp#L853-L879)
  calls the probe before opening the image. The image cache therefore does not
  prevent repeated host allocations. Ordinary already-bound assembly loads may
  bypass probing altogether; not every managed load is a new callback.
- [`AssemblyNative_LoadFromPath`](https://github.com/dotnet/dotnet/blob/846769e14f918f16ee85e7055c72fcc2f6b141bb/src/runtime/src/coreclr/vm/assemblynative.cpp#L191-L211)
  also probes before opening the image. Explicit path loads in separate
  collectible contexts can therefore repeat the callback for the same file.

There is no host notification identifying when an external buffer becomes unused.
The Android host consequently retains successful buffers for process lifetime,
as it does mapped assembly-store data. Retention of one backing image is required,
not evidence of a leak. Allocating a fresh buffer for each callback is unnecessary.

## Retention and repeated probes

A mutex-protected owned list records each exact, case-sensitive probe name,
including any culture directory. The first successful read is an immutable
snapshot. Subsequent callbacks return the same address and length without reading
the file or allocating again. Lookup, reading, and publication share the directory
mutex, so concurrent first probes cannot publish duplicate buffers. Missing files
and failed reads are not cached, allowing a later successful retry.

Expected retained payload bytes are the sum of the file sizes at the first
successful probe of each distinct name. Each name additionally retains
`sizeof (assembly_data) + name.length() + 1` bytes of bookkeeping, excluding
allocator overhead. There is no negative cache or per-callback storage growth.
The directory handle is also retained. With a usable TPA list, only CoreLib
normally needs an external FastDev buffer; other assemblies load from disk.
Probe-only deployments (including the ReadyToRun fallback) can retain all
successfully probed assembly images.

Entries are not evicted, replaced on file changes, or freed by static destructors:
CoreCLR can still reference the old image, and there is no safe release ordering.
Fast Deployment assembly updates require an app-process restart, which resets
both the host cache and CoreCLR's binding/image state. Do not change that behavior
to reload a file in place. The
[`FastDeploy` task](../../src/Xamarin.Android.Build.Debugging.Tasks/Tasks/FastDeploy.cs)
terminates the app before deploying changed FastDev files. Revisit this policy if
the runtime gains a release callback, or if the host begins supporting multiple
override directories in one process.

## Regression coverage

Run the host tests, which compile the production loader and class header with
AddressSanitizer and UndefinedBehaviorSanitizer:

```sh
dotnet test src/Microsoft.Android.Build.Tasks/Tests/Microsoft.Android.Build.Tasks.Tests/Microsoft.Android.Build.Tasks.Tests.csproj -v minimal --filter FullyQualifiedName~FastDevAssemblyOwnershipTests
```

The tests exercise 10,000 repeated probes, concurrent first probes, immutable
snapshots after file replacement/deletion, retry after a missing file, distinct
culture-directory keys, and TPA deferral with CoreLib retention. Platform
discovery, filesystem queries, and logging are substituted; allocation, reading,
locking, cache publication, and TPA behavior are the production implementation.

A separate real-CoreCLR reproducer loaded a 4,096-byte payload into 100 successive
collectible contexts, invoked a method, unloaded each context, and forced garbage
collection. On the pinned runtime it produced 100 successful external callbacks.
Before the cache, those callbacks returned 100 different buffers and retained
409,600 payload bytes. With the cache, they returned one buffer and retained
4,096 payload bytes. This measures the selected payload only, not total process
memory or other assembly images. It demonstrates why collectible unload is not
a release notification for the host and why per-name reuse matters on the
probe-only path.
