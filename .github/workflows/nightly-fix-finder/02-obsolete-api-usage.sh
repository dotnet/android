#!/usr/bin/env bash
# Category: Obsolete API Usage

cat << 'GUIDANCE'
## Category: Obsolete API Usage

### What to look for
Calls to APIs marked with `[Obsolete]` (or `#pragma warning disable CS0618 / CS0612`
suppressions hiding obsolete-usage warnings). Check whether the `[Obsolete]` message
suggests a replacement and migrate the caller.

### How to fix
- If the `[Obsolete]` message names a replacement API, update the call site
- If the obsolete suppression is no longer needed (the API was removed or migrated),
  delete the `#pragma` lines too
- Preserve behavior — only swap to the documented replacement

### What NOT to flag
- The `[Obsolete]` declaration itself (that is intentional)
- Callers that exist solely to test the obsolete API (look for `Test`/`tests/`)
- Generated files (`*.generated.cs`, `*.Designer.cs`)
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Files using [Obsolete] or #pragma warning disable CS0618/CS0612 (sample, shipped code only)"
grep -rn '\[Obsolete\]\|CS0618\|CS0612' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    --exclude='*Test.cs' --exclude='*Tests.cs' \
    src/ 2>/dev/null | shuf | head -20 || echo "None found"
