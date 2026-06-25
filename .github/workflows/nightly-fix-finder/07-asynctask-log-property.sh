#!/usr/bin/env bash
# Category: AsyncTask Log Property Misuse

cat << 'GUIDANCE'
## Category: AsyncTask Log Property Misuse

### What to look for
Direct uses of the `Log` property (`Log.LogMessage`, `Log.LogError`,
`Log.LogWarning`, `LogErrorFromException`, etc.) inside MSBuild task
classes that derive from `AsyncTask`. The `Log` property is marked
`[Obsolete]` on `AsyncTask` because calling it from a background thread
can hang Visual Studio.

### How to fix
Swap the call to the matching thread-safe helper on `AsyncTask`:

| Direct (forbidden)              | Thread-safe replacement              |
|---------------------------------|--------------------------------------|
| `Log.LogMessage (...)`          | `LogMessage (...)`                   |
| `Log.LogError (...)`            | `LogCodedError (...)` (with `XA####`)|
| `Log.LogWarning (...)`          | `LogCodedWarning (...)` (with `XA####`)|
| `Log.LogErrorFromException (e)` | `LogCodedError ("XAxxxx", e.Message)`|
| `Log.LogDebugMessage (...)`     | `LogDebugMessage (...)`              |

Error messages must come from `Properties.Resources` (e.g.
`Properties.Resources.XA0143`) for localization — see repo rules.

### What NOT to flag
- Classes that derive from `Task` or `AndroidTask` directly (not
  `AsyncTask`) — `Log` is fine on those.
- Generated files (`*.generated.cs`, `*.Designer.cs`, `*.g.cs`).
- Test code under `Tests/` / `tests/`.
- The `AsyncTask` base class itself, and the `AsyncTaskExtensions` file.
- The agent MUST open the file and verify the enclosing class actually
  derives (directly or transitively) from `AsyncTask` before filing.
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Files deriving from AsyncTask"
ASYNC_FILES=$(grep -rlP ':\s*AsyncTask\b' \
    --include='*.cs' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    src/ 2>/dev/null || true)

if [ -z "$ASYNC_FILES" ]; then
    echo "  (none found — scan cannot proceed)"
    exit 0
fi

echo "$ASYNC_FILES" | sed 's/^/  /'

echo ""
echo "### Direct \`Log.Log*\` calls inside AsyncTask-derived files (sample)"
echo "$ASYNC_FILES" \
  | xargs grep -nHP '\bLog\.(LogMessage|LogError|LogWarning|LogErrorFromException|LogDebugMessage|LogCodedError|LogCodedWarning)\b' 2>/dev/null \
  | shuf | head -20 \
  || echo "None found"
