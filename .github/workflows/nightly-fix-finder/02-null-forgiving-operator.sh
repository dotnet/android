#!/usr/bin/env bash
# Category: Null-Forgiving Operator

cat << 'GUIDANCE'
## Category: Null-Forgiving Operator

### What to look for
Uses of the `!` null-forgiving operator in shipped (`src/`) C# code. The
repo rule is explicit: **NEVER** use `!` in C# code — always refactor to
avoid it, e.g. by adding an explicit null check that throws, by changing a
helper method's return type to be non-null, or (for `[SetUp]`-initialized
test fields) by declaring the field as nullable.

### How to fix
- Method/property results: add `if (x is null) throw new ArgumentNullException (nameof (x));`
  before the dereference (use `ArgumentNullException.ThrowIfNull (x)` only
  when the owning project targets `net6.0+` — see Phase 2.5).
- After `Assert.IsNotNull (foo)`: extract a local — `var f = foo; Assert.IsNotNull (f); f.Bar ...` —
  instead of `foo!.Bar`.
- Fields that the framework guarantees non-null after init (e.g. `[SetUp]`):
  declare the field as nullable (`MockBuildEngine? engine;`) instead of `= null!;`.
- For helpers like `Dictionary.TryGetValue` where the out value is non-null
  on `true`: pattern-match (`if (dict.TryGetValue (k, out var v) && v is not null)`).

### What NOT to flag
- Generated files (`*.generated.cs`, `*.Designer.cs`, `*.g.cs`)
- Logical-not operators (`!foo`, `!IsEnabled`, `if (!x)`) — only the
  *postfix* null-forgiving form (`foo!.`, `foo![`, `foo!;`, `foo!,`, `foo!)`)
- The `!=` operator
- Strings/comments containing `!` (the scan filters trailing-`!` positions
  so this is rare, but still verify before filing)
- Test code is in scope (the repo rule explicitly covers test code too),
  but prefer the "declare as nullable" refactor noted above.
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Postfix null-forgiving operator (\`!.\`, \`![\`, \`!;\`, \`!,\`, \`!)\`) in shipped code (sample)"
# Match identifier-or-closing-bracket followed by '!' followed by . [ ; , or )
# This intentionally excludes !=, !foo (prefix), and the start-of-line case.
grep -rnP '[A-Za-z0-9_\)\]]\!(\.|\[|;|,|\))' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    src/ 2>/dev/null \
  | grep -vP '!=' \
  | shuf | head -20 \
  || echo "None found"
