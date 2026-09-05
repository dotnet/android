#!/usr/bin/env bash
# Category: Completely Dead Code

cat << 'GUIDANCE'
## Category: Completely Dead Code

### What to look for
Code under `src/` that can be proven to have no callers and no execution path:
- Private members or nested types whose identifier has no references
- Branches that are unconditionally unreachable
- Disabled `#if false` blocks with no documented reason to retain them

The proof must account for every target framework and build configuration in
the owning project. "Probably unused" is not sufficient.

### How to fix
Delete the smallest complete dead member or block, then remove imports or helper
code that became unused as a direct result. Add or update a focused test only
when removal changes a code path that can be exercised.

Before deleting a private member, search the full repository for its identifier
and inspect attributes, interfaces, partial declarations, build targets, and
generated-code inputs that could reference it indirectly.

### What NOT to flag
- Public, protected, or internal API
- JNI callbacks, `[Register]` members, Java peer members, or native entry points
- MSBuild task types or properties, serialization members, reflection targets,
  dependency-injection entry points, or members referenced by name
- Platform-, runtime-, ABI-, or TFM-specific code that is live in another build
- Compatibility code retained for older Android or .NET versions
- Generated files, submodules under `external/`, or test code
- Code that is merely redundant, inefficient, or currently untested
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Explicitly unreachable or disabled constructs"
OBVIOUS=$(grep -rnP '^\s*(#if\s+false\b|if\s*\(\s*false\s*\)|while\s*\(\s*false\s*\))' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    src/ 2>/dev/null \
  | shuf -n 20)
if [ -n "$OBVIOUS" ]; then
    echo "$OBVIOUS"
else
    echo "None found"
fi

echo ""
echo "### Private declarations whose identifier appears only once in repository C# source"
SINGLETON_TOKENS=$(mktemp)
PRIVATE_DECLARATIONS=$(mktemp)
trap 'rm -f "$SINGLETON_TOKENS" "$PRIVATE_DECLARATIONS"' EXIT

grep -rhoP '\b[A-Za-z_][A-Za-z0-9_]*\b' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    src/ 2>/dev/null \
  | sort | uniq -c \
  | awk '$1 == 1 { print $2 }' > "$SINGLETON_TOKENS"

grep -rnPo '^\s*private\s+(?:(?:static|readonly|const|async|unsafe|partial|volatile|new|sealed)\s+)*(?:[A-Za-z_][A-Za-z0-9_.<>,?\[\]]*\s+)+\K[A-Za-z_][A-Za-z0-9_]*(?=\s*(?:\(|\{|=>|=|;))' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    src/ 2>/dev/null > "$PRIVATE_DECLARATIONS"

CANDIDATES=$(awk -F: '
    NR == FNR {
        singleton [$1] = 1
        next
    }
    length ($NF) >= 4 && singleton [$NF] {
        print
    }
' "$SINGLETON_TOKENS" "$PRIVATE_DECLARATIONS" | shuf -n 20)

if [ -n "$CANDIDATES" ]; then
    echo "$CANDIDATES"
else
    echo "None found"
fi
