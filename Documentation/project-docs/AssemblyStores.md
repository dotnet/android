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

The Assembly Store contains an index section that follows the header and precedes the assembly descriptors. The index contains entries for assembly name lookups, with each entry formatted according to the `AssemblyStoreIndexEntry` structure.

### Hash table format

Each entry contains the assembly name hash. In case of satellite assemblies, 
the assembly culture (e.g. `en/` or `fr/`) is treated as part of the assembly 
name, thus resulting in a unique hash. The hash value is obtained using the
[xxHash](https://cyan4973.github.io/xxHash/) algorithm and is
calculated **without** including the `.dll` extension. This is done
for runtime efficiency as the vast majority of Mono requests to load
an assembly does not include the `.dll` suffix, thus saving us time of
appending it in order to generate the hash for index lookup. 

Each entry is represented by the following structure:

    struct AssemblyStoreIndexEntry
    {
        xamarin::android::hash_t name_hash;
        uint32_t descriptor_index;
        uint8_t ignore;
    };

Individual fields have the following meanings:

 - `name_hash`: the platform-specific hash of the assembly's name
   **without** the `.dll` suffix (32-bit hash on 32-bit platforms, 
   64-bit hash on 64-bit platforms)
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

This structure represents an entry in the Assembly Store index. The `name_hash` field is either a 32-bit or 64-bit hash depending on the target platform architecture (`xamarin::android::hash_t` is `uint32_t` on 32-bit platforms and `XXH64_hash_t` on 64-bit platforms).

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
