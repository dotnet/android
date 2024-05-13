<!-- markdown-toc start - Don't edit this section. Run M-x markdown-toc-refresh-toc -->
**Table of Contents**

- [Assembly Store format and purpose](#assembly-store-format-and-purpose)
    - [Rationale](#rationale)
- [Store kinds and locations](#store-kinds-and-locations)
- [Store format](#store-format)
    - [Common header](#common-header)
    - [Assembly descriptor table](#assembly-descriptor-table)
    - [Index store](#index-store)
        - [Hash table format](#hash-table-format)

<!-- markdown-toc end -->

# Assembly Store format and purpose

Assembly stores are binary files which contain the managed
assemblies, their debug data (optionally) and the associated config
file (optionally).  They are placed inside the Android APK/AAB
archives, replacing individual assemblies/pdb/config files.

Assembly stores are an optional form of assembly storage in the
archive, they can be used in all build configurations **except** when
Fast Deployment is in effect (in which case assemblies aren't placed
in the archives at all, they are instead synchronized from the host to
the device/emulator filesystem)

## Rationale

During native startup, the .NET for Android runtime looks inside the
application APK file for the managed assemblies (and their associated
pdb and config files, if applicable) in order to map them (using the
`mmap(2)` call) into memory so that they can be given to the Mono
runtime when it requests a given assembly is loaded.  The reason for
the memory mapping is that, as far as Android is concerned, managed
assembly files are just data/resources and, thus, aren't extracted to
the filesystem.  As a result, Mono wouldn't be able to find the
assemblies by scanning the filesystem - the host application
(.NET for Android) must give it a hand in finding them.

Applications can contain hundreds of assemblies (for instance a Hello
World MAUI application currently contains over 120 assemblies) and
each of them would have to be mmapped at startup, together with its
pdb and config files, if found.  This not only costs time (each `mmap`
invocation is a system call) but it also makes the assembly discovery
an O(n) algorithm, which takes more time as more assemblies are added
to the APK/AAB archive.

An assembly store, however, needs to be mapped only once and any
further operations are merely pointer arithmetic, making the process
not only faster but also reducing the algorithm complexity to O(1).

# Store kinds and locations

Each application will contain at least a single assembly store, with
assemblies that are architecture-agnostics and any number of
architecture-specific stores.  dotnet ships with a handful of
assemblies that **are** architecture-specific - those assemblies are
placed in an architecture specific store, one per architecture
supported by and enabled for the application.  On the execution time,
the .NET for Android runtime will always map the architecture-agnostic
store and one, and **only** one, of the architecture-specific stores.

Stores are placed in the same location in the APK/AAB archive where the
individual assemblies traditionally live, the `assemblies/` (for APK)
and `base/root/assemblies/` (for AAB) folders.

The architecture agnostic store is always named `assemblies.blob` while
the architecture-specific one is called `assemblies.[ARCH].blob`.

Each APK in the application (e.g. the future Feature APKs) **may**
contain the above two assembly store files (some APKs may contain only
resources, other may contain only native libraries etc)

Currently, .NET for Android applications will produce only one set of
stores but when .NET for Android adds support for Android Features, each
feature APK will contain its own set of stores.  All of the APKs will
follow the location, format and naming conventions described above.

# Store format

Each store is a structured binary file, using little-endian byte order
and aligned to a byte boundary.  Each store consists of a header, an
assembly descriptor table and, optionally (see below), two tables with
assembly name hashes.  All the stores are assigned a unique ID, with
the store having ID equal to `0` being the [Index store](#index-store)

Assemblies are stored as adjacent byte streams:

 - **Image data**
   Required to be present for all assemblies, contains the actual
   assembly PE image.
 - **Debug data**
   Optional. Contains the assembly's PDB or MDB debug data.
 - **Config data**
   Optional. Contains the assembly's .config file. Config data
   **must** be terminated with a `NUL` character (`0`), this is to
   make runtime code slightly more efficient.

All the structures described here are defined in the
[`xamarin-app.hh`](../../src/monodroid/jni/xamarin-app.hh) file.
Should there be any difference between this document and the
structures in the header file, the information from the header is the
one that should be trusted.

## Common header

All kinds of stores share the following header format:

    struct AssemblyStoreHeader
    {
        uint32_t magic;
        uint32_t version;
        uint32_t local_entry_count;
        uint32_t global_entry_count;
        uint32_t store_id;
    ;

Individual fields have the following meanings:

 - `magic`: has the value of 0x41424158 (`XABA`)
 - `version`: a value increased every time assembly store format changes.
 - `local_entry_count`: number of assemblies stored in this assembly
   store (also the number of entries in the assembly descriptor
   table, see below)
 - `global_entry_count`: number of entries in the index store's (see
   below) hash tables and, thus, the number of assemblies stored in
   **all** of the assembly stores across **all** of the application's
   APK files, all the other assembly stores have `0` in this field
   since they do **not** have the hash tables.
 - `store_id`: a unique ID of this store.
 
## Assembly descriptor table

Each store header is followed by a table of
`AssemblyStoreHeader.local_entry_count` entries, each entry
defined by the following structure:

    struct AssemblyStoreAssemblyDescriptor
    {
        uint32_t data_offset;
        uint32_t data_size;
        uint32_t debug_data_offset;
        uint32_t debug_data_size;
        uint32_t config_data_offset;
        uint32_t config_data_size;
    };

Only the `data_offset` and `data_size` fields must have a non-zero
value, other fields describe optional data and can be set to `0`. 

Individual fields have the following meanings:

  - `data_offset`: offset of the assembly image data from the
    beginning of the store file
  - `data_size`: number of bytes of the image data
  - `debug_data_offset`: offset of the assembly's debug data from the
    beginning of the store file. A value of `0` indicates there's no
    debug data for this assembly.
  - `debug_data_size`: number of bytes of debug data. Can be `0` only
    if `debug_data_offset` is `0`
  - `config_data_offset`: offset of the assembly's config file data
    from the  beginning of the store file. A value of `0` indicates
    there's no config file data for this assembly.
  - `config_data_size`: number of bytes of config file data. Can be
    `0` only if `config_data_offset` is `0`

## Index store

Each application will contain exactly one store with a global index -
two tables with assembly name hashes.  All the other stores **do not**
contain these tables.  Two hash tables are necessary because hashes
for 32-bit and 64-bit devices are different.

The hash tables follow the [Assembly descriptor
table](#assembly-descriptor-table) and precede the individual assembly
streams.

Placing the hash tables in a single index store, while "wasting" a
certain amount of memory (since 32-bit devices won't use the 64-bit
table and vice versa), makes for simpler and faster runtime
implementation and the amount of memory wasted isn't big (1000
two tables which are 8kb long each, this being the amount of memory
wasted)

### Hash table format

Both tables share the same format, despite the hashes themselves being
of different sizes.  This is done to make handling of the tables
easier on the runtime.

Each entry contains, among other fields, the assembly name hash.  In
case of satellite assemblies, the assembly culture (e.g. `en/` or
`fr/`) is treated as part of the assembly name, thus resulting in a
unique hash. The  hash value is obtained using the
[xxHash](https://cyan4973.github.io/xxHash/) algorithm and is
calculated **without** including the `.dll` extension.  This is done
for runtime efficiency as the vast majority of Mono requests to load
an assembly does not include the `.dll` suffix, thus saving us time of
appending it in order to generate the hash for index lookup. 

Each entry is represented by the following structure:

    struct AssemblyStoreHashEntry
    {
        union {
            uint64_t hash64;
            uint32_t hash32;
        };
        uint32_t mapping_index;
        uint32_t local_store_index;
        uint32_t store_id;
    };

Individual fields have the following meanings:

 - `hash64`/`hash32`: the 32-bit or 64-bit hash of the assembly's name
   **without** the `.dll` suffix
 - `mapping_index`: index into a compile-time generated array of
   assembly data pointers.  This is a global index, unique across
   **all** the APK files comprising the application.
 - `local_store_index`: index into assembly store [Assembly descriptor table](#assembly-descriptor-table)
   describing the assembly.
 - `store_id`: ID of the assembly store containing the assembly
