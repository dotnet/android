#!/usr/bin/env bash
# Category: Performance Anti-patterns

cat << 'GUIDANCE'
## Category: Performance Anti-patterns

### What to look for
Real performance issues — not micro-optimizations:
- **Sync-over-async**: `Task.Result`, `.Wait()`, `.GetAwaiter().GetResult()` in
  non-test code can deadlock in MSBuild / VS contexts
- **String concatenation in loops**: `+=` on strings inside loops should use
  `StringBuilder`
- **Unnecessary allocations**: `.ToList()` / `.ToArray()` where the collection
  is only enumerated once
- **Repeated formatting**: `string.Format` or interpolation inside tight loops

### How to fix
- Convert sync-over-async to genuine `await` (this often requires plumbing
  `async` up the call chain — file the issue with realistic scope)
- Replace string `+=` in loops with a single `StringBuilder`
- Drop the unnecessary `.ToList()`/`.ToArray()` (or move it outside the loop)

### What NOT to flag
- One-time startup code — performance only matters in hot paths or loops
- Test code (`tests/`, `*Test*.cs`, `*Tests*.cs`)
- `Task.Result` inside an `AsyncTask` that already has `Reacquire()` plumbing
- LINQ allocations on small fixed-size collections
- Generated files
GUIDANCE

echo ""
echo "## Scan Data"
echo "### String concatenation in loops (+=)"
grep -rn '+=' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null \
    | grep -i 'string\|str\|result\|output\|sb\|builder\|message\|msg\|text\|line\|path\|name\|value' \
    | grep -v '//' | grep -v -i 'test' \
    | shuf | head -10 || echo "None found"
echo ""
echo "### Sync-over-async (Task.Result, .Wait(), .GetAwaiter().GetResult())"
grep -rn 'Task\.Result\|\.Wait()\|\.GetAwaiter()\.GetResult()' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null \
    | grep -v '//' | grep -v -i 'test' \
    | shuf | head -10 || echo "None found"
echo ""
echo "### Unnecessary LINQ allocations (.ToList(), .ToArray() that may not be needed)"
grep -rn '\.ToList()\|\.ToArray()' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null \
    | grep -v '//' | grep -v -i 'test' \
    | shuf | head -10 || echo "None found"
echo ""
echo "### Repeated string.Format / string.Concat / String.Join (potential in loops)"
grep -rn 'string\.Format\|string\.Concat\|String\.Join' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    src/ 2>/dev/null \
    | grep -v '//' | grep -v -i 'test' \
    | shuf | head -10 || echo "None found"
