---
name: extract-android-assemblies
description: Locally extract managed DLL files from .NET for Android APK, AAB, assembly-store, or compressed DLL inputs, including legacy/current store layouts and LZ4/Zstd compression. Use when investigating a customer Android package, getting assemblies for ILSpy or decompilation, unpacking assembly stores, or recovering ABI-specific managed files.
---

# Extract Android Assemblies

Extract customer assemblies locally with the bundled C# file-based app. Never upload the input package or extracted code.

## Workflow

1. Resolve the input file and choose a new output directory. Keep temporary customer artifacts outside the repository.
2. From the repository root, run:

   ```text
   dotnet run --file .github/skills/extract-android-assemblies/scripts/extract-android-assemblies.cs -- --output <output-directory> <input.apk-or-aab>
   ```

3. Pass multiple input files after the output option when needed.
4. Do not reuse a non-empty output directory: the app refuses to overwrite files.
5. Report:
   - detected inputs
   - extracted DLL count grouped by ABI
   - whether LZ4 or Zstd decompression occurred
   - the local output directory
   - unsupported or corrupt store details

The app supports:

- legacy `assemblies/assemblies.blob` store sets and manifests
- historical `libassemblies.{abi}.blob.so` stores
- current `libassembly-store.so` stores
- individual legacy and RID-specific packaged assemblies
- raw, ELF `payload`, and `_assembly_store` symbol wrappers
- uncompressed, `XALZ`/LZ4, and `XAZS`/Zstd assembly data
