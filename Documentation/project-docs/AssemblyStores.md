<!-- markdown-toc start - Don't edit this section. Run M-x markdown-toc-refresh-toc -->
**Table of Contents**

- [Assembly Store format and purpose](#assembly-store-format-and-purpose)
    - [Rationale](#rationale)
- [Store locations](#store-locations)
- [Store format](#store-format)
    - [Common header](#common-header)
    - [Assembly descriptor table](#assembly-descriptor-table)
    - [Index store](#index-store)
        - [Hash table format](#hash-table-format)
- [Native Struct Documentation](#native-struct-documentation)
    - [AssemblyStoreHeader](#assemblystoreheader)
    - [AssemblyStoreIndexEntry](#assemblystoreindexentry)
    - [AssemblyStoreEntryDescriptor](#assemblystoreentrydescriptor)

<!-- markdown-toc end -->

# Assembly Store format and purpose

Assembly stores are binary files which contain the managed
assemblies, their debug data (optionally) and the associated config
file (optionally).  They are placed inside the Android APK/AAB
archives, replacing individual assemblies/pdb/config files.

Assembly stores are a form of assembly storage in the
archive, they can be used in all build configurations **except** when
Fast Deployment is in effect (in which case assemblies aren't placed
in the archives at all, they are instead synchronized from the host to
the device/emulator filesystem).

CoreCLR applications use this form of assembly storage whenever
assemblies are embedded in the application. Fast Deployment keeps
assemblies outside the archive and therefore does not create a store.

## Rationale

During native startup, the .NET for Android runtime must make the
managed assemblies (and their associated pdb and config files, if
applicable) available to CoreCLR. As far as Android is concerned,
managed assembly files are data rather than native libraries and are
not independently extracted from the archive.

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

The store is wrapped in a shared library whose payload lives in a
*loadable* ELF section pointed at by the exported `_assembly_store`
dynamic symbol. The runtime `dlopen()`s `libassembly-store.so` and
resolves the payload pointer with `dlsym("_assembly_store")`; there is
no ZIP scanning or manual section-header parsing. See
[ApkSharedLibraries.md](ApkSharedLibraries.md) for the payload layout.

# Store locations

There exists only one Assembly Store per architecture. Each application will contain 
architecture-specific assembly stores, with one store per architecture supported by 
and enabled for the application. On the execution time, the .NET for Android runtime 
will map one, and **only** one, of the architecture-specific stores based on the 
current device architecture.

Assembly Store files are placed in the architecture-specific `lib/` directory in the 
APK or AAB archives. The Assembly Store file in the APK or AAB archive is found 
inside an ELF shared library.

Each APK in the application (e.g. the future Feature APKs) **may**
contain assembly store files (some APKs may contain only
resources, other may contain only native libraries etc)

# Store format

Each target ABI/architecture has a single assembly store file, composed of the following parts:

- **[HEADER]** - Fixed size assembly store header
- **[INDEX]** - Variable size index for assembly name lookups  
- **[ASSEMBLY_DESCRIPTORS]** - Assembly descriptor entries
- **[ASSEMBLY_NAMES]** - Assembly name strings
- **[ASSEMBLY DATA]** - The actual assembly data

Each store is a structured binary file, using little-endian byte order
and aligned to a byte boundary.

## [HEADER]

The header is a fixed-size structure at the beginning of each assembly store file:

- **MAGIC** (`uint32_t`) - Magic value `0x41424158` ("XABA" in little-endian)
- **FORMAT_VERSION** (`uint32_t`) - Store format version number (includes ABI and 64-bit flags). The current format version is `3` (see [Hash table format](#hash-table-format))
- **ENTRY_COUNT** (`uint32_t`) - Number of assemblies in the store
- **INDEX_ENTRY_COUNT** (`uint32_t`) - Number of entries in the index (typically `ENTRY_COUNT * 2`)
- **INDEX_SIZE** (`uint32_t`) - Index size in bytes

## [INDEX]

Variable-size section containing hash-based lookup entries for assembly names. Contains `INDEX_ENTRY_COUNT` entries (typically `ENTRY_COUNT * 2` entries to handle assembly names both with and without file extensions):

- **NAME_HASH** (`uint32_t`) - CRC32 hash of the assembly name, regardless of platform bitness
- **DESCRIPTOR_INDEX** (`uint32_t`) - Index into the assembly descriptor array
- **IGNORE** (`uint8_t`) - If set to any value other than 0, the assembly should be ignored during loading

## [ASSEMBLY_DESCRIPTORS]

Variable-size section with `ENTRY_COUNT` entries, each describing one assembly:

- **MAPPING_INDEX** (`uint32_t`) - Index into runtime array where assembly data pointers are stored
- **DATA_OFFSET** (`uint32_t`) - Offset from store beginning to assembly data start
- **DATA_SIZE** (`uint32_t`) - Size of the stored assembly data
- **DEBUG_DATA_OFFSET** (`uint32_t`) - Offset to assembly PDB data start (0 if absent)
- **DEBUG_DATA_SIZE** (`uint32_t`) - Size of assembly PDB data (0 if absent)
- **CONFIG_DATA_OFFSET** (`uint32_t`) - Offset to assembly .config file content start (0 if absent)
- **CONFIG_DATA_SIZE** (`uint32_t`) - Size of assembly .config file content (0 if absent)

## [ASSEMBLY_NAMES]

Variable-size section with `ENTRY_COUNT` entries containing assembly name strings:

- **NAME_LENGTH** (`uint32_t`) - Length of assembly name in bytes
- **NAME** (variable length) - UTF-8 encoded assembly name bytes (without NUL terminator)

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
[`xamarin-app.hh`](../../src/native/clr/include/xamarin-app.hh) file.
Should there be any difference between this document and the
structures in the header file, the information from the header is the
one that should be trusted.

## Common header

All kinds of stores share the following header format:

    struct AssemblyStoreHeader
    {
        uint32_t magic;
        uint32_t version;
        uint32_t entry_count;
        uint32_t index_entry_count;
        uint32_t index_size; // index size in bytes
    };

Individual fields have the following meanings:

 - `magic`: has the value of 0x41424158 (`XABA`)
 - `version`: a value increased every time assembly store format changes.
 - `entry_count`: number of assemblies stored in this assembly
   store (also the number of entries in the assembly descriptor
   table, see below)
 - `index_entry_count`: number of entries in the index
 - `index_size`: index size in bytes
 
## Assembly descriptor table

Each store header is followed by a table of
`AssemblyStoreHeader.entry_count` entries, each entry
defined by the following structure:

    struct AssemblyStoreEntryDescriptor
    {
        uint32_t mapping_index;
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

  - `mapping_index`: index into a runtime array where assembly data pointers are stored
  - `data_offset`: offset of the assembly image data from the beginning of the store file
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

The Assembly Store contains an index section that follows the header and precedes the assembly descriptors.
The index contains entries for assembly name lookups, with each entry formatted according to the `AssemblyStoreIndexEntry` structure.

### Hash table format

Each entry contains the assembly name hash. In case of satellite assemblies, 
the assembly culture (e.g. `en/` or `fr/`) is treated as part of the assembly 
name, thus resulting in a unique hash. Each assembly contributes two index entries. One hashes the full
assembly name including the `.dll` extension, and the other hashes the
extensionless name. Each hash is calculated from its exact lookup key.

The hash is a 32-bit
[CRC32](https://en.wikipedia.org/wiki/Cyclic_redundancy_check)
value on both 32-bit and 64-bit platforms.

Because the CoreCLR hash is only 32 bits wide, hash collisions between
two different assembly names are possible (albeit extremely unlikely).
The index entries are sorted by hash, so all entries sharing a hash are
contiguous; at runtime the loader walks the entire run of entries with a
matching hash and compares the requested name against the actual assembly
name (recovered from the [ASSEMBLY_NAMES](#assembly_names) section) to
select the correct entry.

Each entry is represented by the following structure:

```cpp
struct AssemblyStoreIndexEntry
{
    xamarin::android::hash_t name_hash;
    uint32_t descriptor_index;
    uint8_t ignore;
};
```

Individual fields have the following meanings:

 - `name_hash`: the 32-bit CRC32 hash of the entry's lookup key (either
   the full assembly name or its extensionless form)
 - `descriptor_index`: index into assembly store [Assembly descriptor table](#assembly-descriptor-table)
   describing the assembly.
 - `ignore`: if set to anything other than 0, the assembly should be ignored when loading

# Native Struct Documentation

This section documents the native C++ structures used in the Assembly Store format, as defined in [`xamarin-app.hh`](../../src/native/clr/include/xamarin-app.hh).

## AssemblyStoreHeader

```cpp
struct [[gnu::packed]] AssemblyStoreHeader final
{
    uint32_t magic;
    uint32_t version;
    uint32_t entry_count;
    uint32_t index_entry_count;
    uint32_t index_size; // index size in bytes
};
```

This structure defines the header of each Assembly Store file. The `[[gnu::packed]]` attribute ensures that the structure is stored without padding, which is crucial for binary file format compatibility.

## AssemblyStoreIndexEntry

```cpp
struct [[gnu::packed]] AssemblyStoreIndexEntry final
{
    xamarin::android::hash_t name_hash;
    uint32_t descriptor_index;
    uint8_t ignore; // Assembly should be ignored when loading, its data isn't actually there
};
```

This structure represents an entry in the Assembly Store index.
`xamarin::android::hash_t` is defined as `uint32_t`, so `name_hash`
holds the 32-bit CRC32 hash of the assembly name on all platforms.

## AssemblyStoreEntryDescriptor

```cpp
struct [[gnu::packed]] AssemblyStoreEntryDescriptor final
{
    uint32_t mapping_index;

    uint32_t data_offset;
    uint32_t data_size;

    uint32_t debug_data_offset;
    uint32_t debug_data_size;

    uint32_t config_data_offset;
    uint32_t config_data_size;
};
```

This structure describes an individual assembly within the Assembly Store, including offsets and sizes for the assembly data, debug data (PDB files), and configuration data (.config files).
