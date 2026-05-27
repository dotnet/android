#!/usr/bin/env bash
# Category: Missing XML Documentation (src/Mono.Android/ only)

cat << 'GUIDANCE'
## Category: Missing XML Documentation (src/Mono.Android/ only)

### What to look for
Public declarations in `src/Mono.Android/` (the shipped product `Mono.Android.dll`)
without `///` XML doc comments. Focus on types developers actually consume.

### How to fix
Add `///` XML documentation:
- `<summary>` describing what the API does
- `<param>` for each parameter
- `<returns>` if non-void
- `<exception>` for documented exceptions
- Link to the corresponding Android documentation when relevant

### What NOT to flag
- `Android.Runtime` namespace — plumbing/bridge types (`InputStreamInvoker`, `JNIEnv`)
- `Java.Interop` namespace — low-level interop, not user-facing
- Build tasks, tools, test code — all internal
- Generated files (`*.generated.cs`, `*.Designer.cs`, `AssemblyInfo.cs`)
- Members that already have a doc on the base/interface (inheritdoc applies)
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Public declarations in src/Mono.Android/ (sample, filtered)"
grep -rn 'public ' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='AssemblyInfo.cs' \
    src/Mono.Android/ 2>/dev/null \
    | grep -v 'Android\.Runtime' \
    | grep -v 'Java\.Interop' \
    | grep -v '/obj/' \
    | shuf | head -20 || echo "None found"
