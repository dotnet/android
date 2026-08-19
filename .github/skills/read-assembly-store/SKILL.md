---
name: read-assembly-store
description: Inspect and list managed assemblies in .NET for Android APK, AAB, legacy assembly blob, manifest, or libassembly-store.so files. Use for customer package investigation, assembly-store format identification, ABI-specific assembly listings, offsets, sizes, hashes, or determining what managed code an Android package contains.
---

# Read Assembly Store

Inspect the package locally with the bundled C# file-based app. Never upload customer packages or extracted code.

## Workflow

1. Resolve the input file and optional ABI filter.
2. From the repository root, run:

   ```bash
   dotnet run --file .github/skills/read-assembly-store/scripts/read-assembly-store.cs -- <input>
   ```

3. To filter architectures, add `--arch=Arm64`, `--arch=Arm`, `--arch=X86_64`, or `--arch=X86`. Separate multiple values with commas.
4. Report:
   - target architectures
   - assembly count
   - assembly names and notable ignored entries
   - the local input path

The app supports legacy v1 store sets and manifests, v2/v3 RID-specific stores, raw blobs, ELF `payload` wrappers, and `_assembly_store` symbol wrappers.
