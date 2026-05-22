#!/usr/bin/env bash
# Category: Files Missing #nullable enable

cat << 'GUIDANCE'
## Category: Files Missing #nullable enable

### What to look for
C# files in `src/` without `#nullable enable` that are good candidates for
opting in to nullable reference types. Prefer files in
`src/Xamarin.Android.Build.Tasks/` as they follow clear patterns.

### ⚠️ CRITICAL — Check the owning csproj TargetFramework FIRST

Before emitting ANY null-check code, look at the candidate file's owning
`*.csproj` and read its `<TargetFramework>` / `<TargetFrameworks>` value.
The scan data below pairs each candidate file with its owning project's TFM
to make this trivial.

The pattern you emit MUST match the TFM:

| Owning TFM contains... | Use this pattern |
|---|---|
| `netstandard2.0`, `netstandard2.1`, `net4xx`, or anything below `net6.0` | `if (x == null)`<br>`    throw new ArgumentNullException (nameof (x));` |
| `net6.0` or later (including `net10.0`, `net11.0`-android, etc.) | `ArgumentNullException.ThrowIfNull (x);` |

**`ArgumentNullException.ThrowIfNull` was added in .NET 6** and does NOT exist
in `netstandard2.0`. Emitting it into a `netstandard2.0` project produces code
that fails to compile (see the regression in PR #11455). If the project
multi-targets and any TFM is below `net6.0`, use the explicit two-line form.

### How to fix
Follow the repo's nullable conventions documented in the AI instructions:
- Add `#nullable enable` at the top of the file with no preceding blank lines
- **Never** use the `!` (null-forgiving) operator — refactor or check for null explicitly
- For MSBuild `[Required]` properties: non-nullable with a default (`= ""` or `= []`)
- For non-required MSBuild properties: nullable (`string?`, `ITaskItem[]?`)
- Replace `string.IsNullOrEmpty (x)` with the `x.IsNullOrEmpty ()` extension method
- Replace `string.IsNullOrWhiteSpace (x)` with `x.IsNullOrWhiteSpace ()`
- Choose the null-check API per the TFM table above — do NOT pick uniformly

### What NOT to flag
- Generated files (`*.generated.cs`, `*.Designer.cs`)
- Files that are already opted in (`#nullable enable` present anywhere)
- Files that are trivially small (less than ~20 lines) — low value
- Files dominated by `[DllImport]` / pinvoke — annotations are often meaningless there
GUIDANCE

echo ""
echo "## Scan Data"

CANDIDATES=$(grep -rL '#nullable enable' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null | shuf | head -20)

echo "### C# files in src/ without #nullable enable (sample, with owning csproj TFM)"
if [ -z "$CANDIDATES" ]; then
    echo "None found"
else
    while IFS= read -r f; do
        # Walk up parent directories looking for the nearest *.csproj
        dir=$(dirname "$f")
        csproj=""
        while [ "$dir" != "." ] && [ "$dir" != "/" ]; do
            found=$(find "$dir" -maxdepth 1 -name '*.csproj' -type f 2>/dev/null | head -n 1)
            if [ -n "$found" ]; then
                csproj="$found"
                break
            fi
            dir=$(dirname "$dir")
        done
        if [ -n "$csproj" ]; then
            # Extract <TargetFramework> or <TargetFrameworks> (first occurrence)
            tfm=$(grep -oE '<TargetFrameworks?>[^<]+</TargetFrameworks?>' "$csproj" 2>/dev/null \
                | head -n 1 \
                | sed -E 's|</?TargetFrameworks?>||g')
            if [ -z "$tfm" ]; then tfm="(unknown — no <TargetFramework[s]> in $csproj)"; fi
            echo "  $f"
            echo "      csproj: $csproj"
            echo "      TFM:    $tfm"
        else
            echo "  $f"
            echo "      csproj: (none found walking up — agent must investigate)"
        fi
    done <<< "$CANDIDATES"
fi
echo ""
echo "### Total count of files missing #nullable enable"
grep -rL '#nullable enable' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null | wc -l || true
