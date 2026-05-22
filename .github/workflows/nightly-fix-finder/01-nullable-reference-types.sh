#!/usr/bin/env bash
# Category: Files Missing #nullable enable

cat << 'GUIDANCE'
## Category: Files Missing #nullable enable

### What to look for
C# files in `src/` without `#nullable enable` that are good candidates for
opting in to nullable reference types. Prefer files in
`src/Xamarin.Android.Build.Tasks/` as they follow clear patterns.

### How to fix
Follow the repo's nullable conventions documented in the AI instructions:
- Add `#nullable enable` at the top of the file with no preceding blank lines
- **Never** use the `!` (null-forgiving) operator — refactor or check for null explicitly
- For MSBuild `[Required]` properties: non-nullable with a default (`= ""` or `= []`)
- For non-required MSBuild properties: nullable (`string?`, `ITaskItem[]?`)
- Replace `string.IsNullOrEmpty (x)` with the `x.IsNullOrEmpty ()` extension method
- Replace `string.IsNullOrWhiteSpace (x)` with `x.IsNullOrWhiteSpace ()`
- Use `ArgumentNullException.ThrowIfNull (x)` in .NET 10+ Android projects
- Use `throw new ArgumentNullException (nameof (x))` in `netstandard2.0` projects

### What NOT to flag
- Generated files (`*.generated.cs`, `*.Designer.cs`)
- Files that are already opted in (`#nullable enable` present anywhere)
- Files that are trivially small (less than ~20 lines) — low value
- Files dominated by `[DllImport]` / pinvoke — annotations are often meaningless there
GUIDANCE

echo ""
echo "## Scan Data"
echo "### C# files in src/ without #nullable enable (sample)"
grep -rL '#nullable enable' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null | shuf | head -20 || echo "None found"
echo ""
echo "### Total count"
grep -rL '#nullable enable' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null | wc -l || true
