#!/usr/bin/env bash
# Category: Unused Using Directives

cat << 'GUIDANCE'
## Category: Unused Using Directives

### What to look for
Files in `src/` with >10 `using` directives that likely contain unused ones.
A high count alone isn't a bug — the agent MUST open the file and confirm
specific directives are unused before filing.

### How to fix
Remove the unused `using` directives. Keep `global using`s and conditional
ones (`#if NETxx`) intact.

### What NOT to flag
- Files where every `using` is genuinely referenced
- Generated files (`*.generated.cs`, `*.Designer.cs`)
- Test files where `using` directives are often intentional setup
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Files with many using directives (potential cleanup, sample)"
for f in $(find src -name '*.cs' -type f \
        ! -path '*/obj/*' ! -path '*/bin/*' \
        ! -name '*.generated.cs' ! -name '*.Designer.cs' \
        2>/dev/null | shuf | head -30); do
    count=$(grep -c '^using ' "$f" 2>/dev/null || true)
    if [ "${count:-0}" -gt 10 ]; then
        echo "  $f: $count using directives"
    fi
done
