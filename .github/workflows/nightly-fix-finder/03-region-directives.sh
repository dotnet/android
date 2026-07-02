#!/usr/bin/env bash
# Category: #region / #endregion Directives

cat << 'GUIDANCE'
## Category: #region / #endregion Directives

### What to look for
`#region` and `#endregion` directives in shipped C# code under `src/`.
The repo style is explicit: **Do NOT use `#region` or `#endregion`.**

### How to fix
Delete the `#region NAME` and matching `#endregion` lines. Do not delete
the code between them, and do not reflow the surrounding lines — keep the
diff to just the two directive lines per region.

If the region name carried useful information (e.g. `#region IDisposable`),
preserve it as a single `// IDisposable` comment on the line that used to
hold the `#region` directive.

### What NOT to flag
- Generated files (`*.generated.cs`, `*.Designer.cs`, `*.g.cs`)
- Files under `external/` (submodules — not owned by this repo)
- `#region` appearing inside a string literal or comment (rare but verify
  by opening the file before filing)
GUIDANCE

echo ""
echo "## Scan Data"
echo "### \`#region\` directives in shipped code (sample)"
grep -rnP '^\s*#region\b' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    src/ 2>/dev/null \
  | shuf | head -20 \
  || echo "None found"

echo ""
echo "### Per-file region counts (top offenders)"
grep -rlP '^\s*#region\b' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    src/ 2>/dev/null \
  | while read -r f; do
        c=$(grep -cP '^\s*#region\b' "$f" 2>/dev/null || echo 0)
        echo "  $c  $f"
    done \
  | sort -rn | head -10
