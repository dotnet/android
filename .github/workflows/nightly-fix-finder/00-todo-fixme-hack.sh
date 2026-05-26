#!/usr/bin/env bash
# Category: TODO/FIXME/HACK Comments

cat << 'GUIDANCE'
## Category: TODO/FIXME/HACK Comments

### What to look for
Stale TODO/FIXME/HACK/XXX comments in shipped code under `src/` with a
concrete, actionable resolution — either implement what's described or remove
the comment if it's no longer relevant. **Test code is out of scope** (the
scan excludes `Tests/`, `*Test.cs`, `*Tests.cs`).

### Good candidates
- TODOs referencing a bug number or feature that has since been completed
- TODOs with a clear description of what needs doing (not just "fix this someday")
- FIXMEs that indicate a known problem without a workaround

### How to fix
Either implement the TODO or remove the comment if it's no longer relevant.
Include a brief explanation in the issue body of why it was resolved or removed.

### What NOT to flag
- TODOs inside generated files (`*.generated.cs`, `*.Designer.cs`) — never touch those
- TODOs that are clearly still valid and require multi-PR effort
- Vague TODO comments with no actionable content
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Sample TODO/FIXME/HACK comments in shipped code under src/"
grep -rn 'TODO\|FIXME\|HACK\|XXX' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    --exclude='*Test.cs' --exclude='*Tests.cs' \
    src/ 2>/dev/null | shuf | head -20 || echo "None found"
echo ""
echo "### Total count"
grep -rn 'TODO\|FIXME\|HACK\|XXX' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    --exclude='*Test.cs' --exclude='*Tests.cs' \
    src/ 2>/dev/null | wc -l || true
