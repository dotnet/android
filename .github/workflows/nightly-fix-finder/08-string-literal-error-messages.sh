#!/usr/bin/env bash
# Category: String Literals in Error Messages

cat << 'GUIDANCE'
## Category: String Literals in Error Messages

### What to look for
Hardcoded error/warning strings passed to `Log.Error`, `Log.Warning`,
`LogError`, `LogWarning`, `LogCodedError`, or `LogCodedWarning` calls that
should live in `Properties.Resources` for localization.

### How to fix
Prefer a suitable existing `Properties.Resources` message when it accurately
preserves the diagnostic's meaning. If none exists, implement the complete
localized diagnostic:
- Add the message to the owning project's main English `Resources.resx` only;
  never edit non-English resource files
- Update the checked-in `Resources.Designer.cs` accessor when the project keeps
  that generated file in source control
- For a new MSBuild warning or error, allocate an unused `XA####` code, use
  `LogCodedWarning` or `LogCodedError`, and add the required message
  documentation under `Documentation/docs-mobile/messages/`, including its
  index and TOC entries
- Preserve formatting arguments and exception details

The resource, accessor, source call site, and required XA documentation are one
cohesive fix. Do not reject an otherwise small candidate merely because the
complete localization change spans those files.

### What NOT to flag
- Strings already coming from any `Resources.*` accessor, including fully
  qualified resource classes
- Debug-only log messages (`LogDebugMessage`) — not customer-facing
- Test code (`tests/`, `*Test*.cs`)
- Strings that are format templates already in resources but reconstructed inline
- Dynamic tool output or exception text that is intentionally passed through
- Messages where no stable, specific wording or error-code meaning can be established
- Projects without an existing `Properties/Resources.resx` localization system
GUIDANCE

echo ""
echo "## Scan Data"
echo "### Hardcoded error strings in resource-backed shipped projects (sample)"
RESOURCE_ROOTS=$(find src -path '*/Properties/Resources.resx' -type f 2>/dev/null \
  | sed 's#/Properties/Resources\.resx$##')

if [ -z "$RESOURCE_ROOTS" ]; then
    echo "None found"
    exit 0
fi

MATCHES=$(
  while IFS= read -r root; do
      grep -rnP '(?<![A-Za-z0-9_])(?:Log\.(?:Error|Warning)|LogError|LogWarning)\s*\(\s*\$?"' \
          --include='*.cs' \
          --exclude-dir=obj --exclude-dir=bin \
          --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
          --exclude='*.generated.cs' --exclude='*.Designer.cs' \
          --exclude='*Test.cs' --exclude='*Tests.cs' \
          "$root" 2>/dev/null
      grep -rnP 'Log(?:CodedError|CodedWarning)\s*\(\s*"[^"]+"\s*,\s*\$?"' \
          --include='*.cs' \
          --exclude-dir=obj --exclude-dir=bin \
          --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
          --exclude='*.generated.cs' --exclude='*.Designer.cs' \
          --exclude='*Test.cs' --exclude='*Tests.cs' \
          "$root" 2>/dev/null
  done <<< "$RESOURCE_ROOTS" \
    | grep -vE '(^|[^A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_.]*\.)?Resources\.' \
    | grep -vE 'LogError[[:space:]]*\([[:space:]]*\$?"XA(\{|[0-9])' \
    | grep -vE 'Log\.Log(Error|Warning)[[:space:]]*\([[:space:]]*""[[:space:]]*,[[:space:]]*"XA[0-9]+"' \
    | grep -vE '\$"\{[^}]+\}"' \
    | grep -vP '\$"(?:\{[^}]+\}[ .,:;-]*)+"' \
    | grep -vE '\$?"\{(message|error|text|output|ex|rex)(\.|})' \
    | grep -vE '"\{[0-9]+\}([ :.-]*\{[0-9]+\})+"' \
    | grep -v '"{0}"' \
    | shuf -n 20
)

if [ -n "$MATCHES" ]; then
    echo "$MATCHES"
else
    echo "None found"
fi
