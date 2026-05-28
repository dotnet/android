#!/usr/bin/env bash
# Category: General Mistakes

cat << 'GUIDANCE'
## Category: General Mistakes

### What to look for
Read the randomly selected source files thoroughly and look for **real bugs**:
- Logic errors, off-by-one errors
- Null dereferences not protected by the surrounding code
- Race conditions on shared state
- Resource leaks (missing `using` / `Dispose`)
- Dead code, unreachable branches
- Copy-paste errors (same code repeated with subtle differences that look unintended)
- Incorrect exception handling (catching too broadly, swallowing without logging)

### How to fix
File a specific issue describing the actual bug with concrete evidence (line
numbers, the suspected wrong behavior, and the expected correct behavior).

### What NOT to flag
- Formatting, whitespace, or style — not actionable for a fix issue
- "Could be cleaner" subjective preferences with no functional impact
- Generated files (`*.generated.cs`, `*.Designer.cs`, `AssemblyInfo.cs`)
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Random C# source files in shipped code under src/ for general review (sample)"
find src -name '*.cs' -type f \
    ! -path '*/obj/*' ! -path '*/bin/*' \
    ! -path '*/Tests/*' ! -path '*/Test/*' ! -path '*/tests/*' \
    ! -name '*.generated.cs' ! -name '*.Designer.cs' ! -name 'AssemblyInfo.cs' \
    ! -name '*Test.cs' ! -name '*Tests.cs' \
    2>/dev/null | shuf | head -5
