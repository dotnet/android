#!/usr/bin/env bash
# Category: Error Handling

cat << 'GUIDANCE'
## Category: Error Handling

### What to look for
Bare `catch (Exception)` blocks (or `catch { }`) that swallow errors without
logging or rethrowing. These hide real failures and make build/runtime issues
nearly impossible to diagnose.

### How to fix
- If the exception is genuinely expected, catch the **specific** type
  (`IOException`, `FileNotFoundException`, etc.) and add a comment explaining why
- Otherwise add logging via `LogCodedError` / `LogCodedWarning` (in MSBuild
  tasks, prefer the `AsyncTask` thread-safe helpers — never `Log.LogMessage`
  directly from a background thread)
- For truly fatal failures, rethrow with `throw;` (not `throw ex;` — preserves
  the stack trace)

### What NOT to flag
- `catch (Exception) { /* logged */ Log.* / LogCodedError ... }` — already handled
- `catch` blocks inside test code that intentionally swallow expected exceptions
- Generated files (`*.generated.cs`, `*.Designer.cs`)
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Bare catch blocks in shipped code that may swallow exceptions (sample)"
grep -rnP 'catch\s*\(Exception\b' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' \
    --exclude='*Test.cs' --exclude='*Tests.cs' \
    src/ 2>/dev/null | shuf | head -20 || echo "None found"
