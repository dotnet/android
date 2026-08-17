#!/usr/bin/env bash
# Category: String Literals in Error Messages

cat << 'GUIDANCE'
## Category: String Literals in Error Messages

### What to look for
Hardcoded error/warning strings passed to `Log.Error`, `Log.Warning`,
`LogError`, `LogWarning`, `LogCodedError`, or `LogCodedWarning` calls that
should live in `Properties.Resources` for localization.

### How to fix
Replace the literal with a suitable existing `Properties.Resources.XA####`
message. If no existing resource accurately describes the error, call `noop`:
adding a new resource requires updating the generated `Resources.Designer.cs`,
which this workflow must not modify.

### What NOT to flag
- Strings already coming from `Properties.Resources`
- Debug-only log messages (`LogDebugMessage`) — not customer-facing
- Test code (`tests/`, `*Test*.cs`)
- Strings that are format templates already in resources but reconstructed inline
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Hardcoded error strings in shipped code that could be in Resources.resx (sample)"
grep -rn 'Log\.\(Error\|Warning\)\|LogError\|LogWarning\|LogCodedError\|LogCodedWarning' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    --exclude='*Test.cs' --exclude='*Tests.cs' \
    src/ 2>/dev/null \
    | grep '"' \
    | grep -v 'Properties\.Resources' \
    | shuf | head -20 || echo "None found"
