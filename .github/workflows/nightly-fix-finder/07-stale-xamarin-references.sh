#!/usr/bin/env bash
# Category: Stale Xamarin and Mono References

cat << 'GUIDANCE'
## Category: Stale Xamarin and Mono References

### What to look for
Comments in source code and current documentation that still describe the
product as Xamarin or Xamarin.Android, or link to the archived
`xamarin/xamarin-android` or `mono/mono` repositories, when the text clearly
predates the .NET 6 transition.

Good candidates state current behavior, ownership, setup, or contribution
instructions using obsolete names or locations. Verify that the replacement is
accurate before editing:
- Current product name: `.NET for Android`
- Current repository: `dotnet/android`
- Runtime source or issues formerly in `mono/mono`: locate the exact equivalent
  in `dotnet/runtime`; do not mechanically rewrite the repository name

### How to fix
Update only the stale prose or link. Preserve the original technical meaning,
verify replacement URLs exist, and keep the diff focused. If no accurate modern
equivalent can be established, call `noop`.

### What NOT to flag
- Historical release notes, previous-release documentation, migration guides,
  or text intentionally describing Xamarin-era behavior
- Namespaces, assembly names, package IDs, MSBuild properties, paths, API names,
  compatibility switches, test fixtures, or literal values containing Xamarin
- Copyright, attribution, license, third-party notice, or provenance text
- References to the Mono runtime that remain technically current
- Old repository links retained to identify the original issue, commit, or
  source provenance when no equivalent moved to the current repository
- Generated files or submodules under `external/`
GUIDANCE

echo ""
echo "## Scan Data"
if [ ! -d Documentation ] || [ ! -d src ] || [ ! -f README.md ]; then
    echo "Required scan paths are missing." >&2
    exit 1
fi

echo "### Links to the pre-.NET 6 mono/mono repository"
MONO_LINKS=$(grep -rnEi 'github\.com/mono/mono' \
    --include='*.md' --include='*.cs' --include='*.cpp' --include='*.cc' \
    --include='*.h' --include='*.hh' --include='*.java' \
    --exclude='previous-releases.md' --exclude='AssemblyInfo.cs' \
    --exclude-dir=release-notes \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    Documentation/ src/ README.md 2>/dev/null \
  | grep -v '^Documentation/guides/profiling.md:121:' \
  | shuf -n 20)
if [ -n "$MONO_LINKS" ]; then
    echo "$MONO_LINKS"
else
    echo "None found"
fi

echo ""
echo "### Stale Xamarin documentation and repository links"
DOC_MATCHES=$(grep -rnEi \
    'https?://(docs\.microsoft\.com/xamarin/android|developer\.xamarin\.com/(guides/)?android|github\.com/xamarin/xamarin-android/wiki)' \
    --include='*.md' \
    --exclude='previous-releases.md' \
    --exclude-dir=release-notes \
    --exclude-dir=obj --exclude-dir=bin \
    Documentation/ README.md 2>/dev/null \
  | shuf -n 20)
if [ -n "$DOC_MATCHES" ]; then
    echo "$DOC_MATCHES"
else
    echo "None found"
fi

echo ""
echo "### Current prose that still describes the product as Xamarin"
PROSE_MATCHES=$(grep -rnEi \
    'Xamarin(\.Android| Android)? (project|application|app|build system|product|tooling|runtime|SDK|provides|supports|uses|requires|inserts|will|can|does|is|was|source tree)' \
    --include='*.md' \
    --exclude='previous-releases.md' \
    --exclude-dir=release-notes \
    Documentation/ 2>/dev/null \
  | grep -vEi 'Xamarin\.Android\.(slnx|Build|Tools|Runtime)|Xamarin\.(AndroidX|Forms|Kotlin|Google)|legacy|classic|Added in|Removed in|support ended|upgrade|migration' \
  | shuf -n 10)

SOURCE_PROSE=$(grep -rnEi \
    'Xamarin(\.Android| Android)? (project|application|app|build system|product|tooling|runtime|SDK|provides|supports|uses|requires|inserts|will|can|does|is|was|source tree)' \
    --include='*.cs' --include='*.cpp' --include='*.cc' \
    --include='*.h' --include='*.hh' --include='*.java' \
    --exclude-dir=obj --exclude-dir=bin \
    --exclude-dir=Tests --exclude-dir=Test --exclude-dir=tests \
    --exclude='*.generated.cs' --exclude='*.Designer.cs' --exclude='*.g.cs' \
    --exclude='AssemblyInfo.cs' \
    src/ 2>/dev/null \
  | grep -E ':[0-9]+:.*(//|/\*|\*)' \
  | grep -vEi 'Xamarin\.Android\.(slnx|Build|Tools|Runtime)|Xamarin\.(AndroidX|Forms|Kotlin|Google)|legacy|classic|Copyright|@xamarin\.com|<see cref|xmlns:xamarin|global::Xamarin|xamarin::|/Users/runner/|com\.xamarin\.' \
  | shuf -n 10)

if [ -n "$PROSE_MATCHES" ] || [ -n "$SOURCE_PROSE" ]; then
    if [ -n "$PROSE_MATCHES" ]; then
        echo "$PROSE_MATCHES"
    fi
    if [ -n "$SOURCE_PROSE" ]; then
        echo "$SOURCE_PROSE"
    fi
else
    echo "None found"
fi
