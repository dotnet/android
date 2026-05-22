#!/usr/bin/env bash
# Category: String Literals in Error Messages

cat << 'GUIDANCE'
## Category: String Literals in Error Messages

### What to look for
Hardcoded error/warning strings passed to `Log.Error`, `Log.Warning`,
`LogError`, `LogWarning`, `LogCodedError`, or `LogCodedWarning` calls that
should live in `Properties.Resources` for localization.

### How to fix
1. Add the new message to the English `src/.../Properties/Resources.resx`
   (never modify non-English `*.resx` or `*.lcl` files — those are auto-generated)
2. Reference it via `Properties.Resources.XA####`
3. If you create a NEW `XA####` error code, you MUST also:
   - Create `Documentation/docs-mobile/messages/xa####.md` following the
     existing format (frontmatter + Example messages + Issue explanation + Solution)
   - Add the new code to the table of contents in
     `Documentation/docs-mobile/messages/index.md`

### What NOT to flag
- Strings already coming from `Properties.Resources`
- Debug-only log messages (`LogDebugMessage`) — not customer-facing
- Test code (`tests/`, `*Test*.cs`)
- Strings that are format templates already in resources but reconstructed inline
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Hardcoded error strings that could be in Resources.resx (sample)"
grep -rn 'Log\.\(Error\|Warning\)\|LogError\|LogWarning\|LogCodedError\|LogCodedWarning' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null \
    | grep '"' \
    | grep -v 'Properties\.Resources' \
    | shuf | head -20 || echo "None found"
