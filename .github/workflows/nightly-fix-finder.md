---
description: Nightly scan for random code improvement opportunities, files issues assigned to Copilot
on:
  schedule:
    - cron: "daily around 02:00"
  pull_request:
    paths:
      - ".github/workflows/nightly-fix-finder.md"
      - ".github/workflows/nightly-fix-finder.lock.yml"
  workflow_dispatch:
    inputs:
      category:
        description: "Category to scan (leave blank for random)"
        required: false
        type: choice
        options:
          - ""
          - "0 - TODO/FIXME/HACK Comments"
          - "1 - Nullable Reference Types"
          - "2 - Obsolete API Usage"
          - "3 - Performance Anti-patterns"
          - "4 - Missing XML Documentation"
          - "5 - General Mistakes"
          - "6 - Unused Using Directives"
          - "7 - Error Handling"
          - "8 - String Literals in Error Messages"
permissions:
  contents: read
  issues: read
engine:
  id: copilot
  model: claude-opus-4.6
network:
  allowed:
    - defaults
    - github
    - dotnet
tools:
  github:
    toolsets: [repos, issues]
    min-integrity: none
  bash:
    - "find src -name '*.cs' -type f"
    - "grep:*"
    - "wc:*"
    - "head:*"
    - "tail:*"
    - "sort:*"
    - "cat:*"
    - "awk:*"
    - "sed:*"
    - "shuf:*"
    - "date:*"
    - "xargs:*"
safe-outputs:
  report-failure-as-issue: false
  create-issue:
    title-prefix: "[fix-finder] "
    labels: [automated, code-quality]
    expires: 7d
    close-older-issues: false
  assign-to-agent:
    model: "claude-opus-4.6"
    target: "*"
    github-token: ${{ secrets.ANDROID_TEAM_PAT }}
  noop:
timeout-minutes: 30
strict: true
steps:
  - name: Collect codebase metrics
    env:
      INPUT_CATEGORY: ${{ inputs.category }}
    run: |
      mkdir -p /tmp/gh-aw/agent
      if [ -n "$INPUT_CATEGORY" ]; then
        CATEGORY_INDEX="${INPUT_CATEGORY%%\ *}"
      else
        CATEGORY_INDEX=$(( RANDOM % 9 ))
      fi
      {
        echo "## Selected Category: $CATEGORY_INDEX"
        echo ""

        case $CATEGORY_INDEX in
          0)
            echo "## Category 0: TODO/FIXME/HACK Comments"
            echo "### Sample TODO/FIXME/HACK comments in src/"
            grep -rn "TODO\|FIXME\|HACK\|XXX" --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | shuf | head -20 || echo "None found"
            echo "### Total count"
            grep -rn "TODO\|FIXME\|HACK\|XXX" --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | wc -l || true
            ;;
          1)
            echo "## Category 1: Files Missing Nullable Enable"
            echo "### C# files in src/ without #nullable enable (sample)"
            grep -rL '#nullable enable' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | shuf | head -20 || echo "None found"
            echo "### Total count"
            grep -rL '#nullable enable' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | wc -l || true
            ;;
          2)
            echo "## Category 2: Obsolete API Usage"
            echo "### Files using [Obsolete] or #pragma warning disable CS0618 (sample)"
            grep -rn "\[Obsolete\]\|CS0618\|CS0612" --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | shuf | head -20 || echo "None found"
            ;;
          3)
            echo "## Category 3: Performance Anti-patterns"
            echo "### String concatenation in loops (+=)"
            grep -rn '+=' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | grep -i 'string\|str\|result\|output\|sb\|builder\|message\|msg\|text\|line\|path\|name\|value' | grep -v '//' | grep -v 'test' | shuf | head -10 || echo "None found"
            echo ""
            echo "### Sync-over-async (Task.Result, .Wait(), .GetAwaiter().GetResult())"
            grep -rn 'Task\.Result\|\.Wait()\|\.GetAwaiter()\.GetResult()' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | grep -v '//\|test\|Test' | shuf | head -10 || echo "None found"
            echo ""
            echo "### Unnecessary LINQ allocations (.ToList(), .ToArray() that may not be needed)"
            grep -rn '\.ToList()\|\.ToArray()' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | grep -v '//\|test\|Test' | shuf | head -10 || echo "None found"
            echo ""
            echo "### Repeated string.Format or interpolation in loops"
            grep -rn 'string\.Format\|string\.Concat\|String\.Join' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | grep -v '//\|test\|Test' | shuf | head -10 || echo "None found"
            ;;
          4)
            echo "## Category 4: Missing XML Documentation (src/Mono.Android/ only)"
            echo "### Public declarations in Mono.Android (shipped product) without XML docs"
            echo "### NOTE: Excludes Android.Runtime (plumbing), Java.Interop (bridge), and generated code"
            grep -rn "public " --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/Mono.Android/ 2>/dev/null | grep -v "Designer.cs" | grep -v "AssemblyInfo.cs" | grep -v "Android.Runtime" | grep -v "Java.Interop" | grep -v "/obj/" | shuf | head -20 || echo "None found"
            ;;
          5)
            echo "## Category 5: General Mistakes"
            echo "### Random C# source files in src/ for general review (sample)"
            find src -name '*.cs' -type f ! -path '*/obj/*' ! -path '*/bin/*' ! -name 'Designer.cs' ! -name 'AssemblyInfo.cs' 2>/dev/null | shuf | head -5
            ;;
          6)
            echo "## Category 6: Unused Using Directives"
            echo "### Files with many using directives (potential cleanup, sample)"
            for f in $(find src -name '*.cs' -type f ! -path '*/obj/*' ! -path '*/bin/*' ! -name 'Designer.cs' 2>/dev/null | shuf | head -30); do
              count=$(grep -c "^using " "$f" 2>/dev/null || true)
              if [ "${count:-0}" -gt 10 ]; then
                echo "  $f: $count using directives"
              fi
            done
            ;;
          7)
            echo "## Category 7: Error Handling"
            echo "### Bare catch blocks that may swallow exceptions (sample)"
            grep -rnP "catch\s*\(Exception\b" --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | shuf | head -20 || echo "None found"
            ;;
          8)
            echo "## Category 8: String Literals in Error Messages"
            echo "### Hardcoded error strings that could be in Resources.resx (sample)"
            grep -rn 'Log\.\(Error\|Warning\)\|LogError\|LogWarning\|LogCodedError\|LogCodedWarning' --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/ 2>/dev/null | grep '"' | grep -v "Properties.Resources" | shuf | head -20 || echo "None found"
            ;;
        esac
      } > /tmp/gh-aw/agent/scan-results.md
      echo "✅ Category $CATEGORY_INDEX scan complete → /tmp/gh-aw/agent/scan-results.md"
---

# Nightly Fix Finder

You are the Nightly Fix Finder Agent — an expert system that scans the dotnet/android repository each night for random code improvement opportunities and files actionable issues for Copilot to fix.

## Mission

Each night, select one scan category, analyze the pre-collected data, find one specific actionable improvement, create a well-scoped issue, and assign Copilot to fix it.

## Current Context

- **Repository**: ${{ github.repository }}
- **Run Date**: Each run picks a random category (0-9) using $RANDOM
- **Pre-computed scan results**: `/tmp/gh-aw/agent/scan-results.md`

## Phase 1: Load Scan Results

### 1.1 Read Pre-computed Data

Read `/tmp/gh-aw/agent/scan-results.md` which contains pre-collected metrics for **one randomly selected category**. The file header tells you which category was selected.

### 1.2 Identify the Category

The scan results start with `## Selected Category: N` where N is 0-9. The file ONLY contains data for that one category — you MUST work with whatever category was selected:

| Index | Category | Description |
|-------|----------|-------------|
| 0 | TODO/FIXME/HACK Comments | Find stale TODO/FIXME/HACK comments that should be resolved |
| 1 | Nullable Reference Types | Find C# files missing `#nullable enable` that should be opted in |
| 2 | Obsolete API Usage | Find uses of `[Obsolete]` members that should be updated |
| 3 | Performance Anti-patterns | Find performance issues: sync-over-async, string concat in loops, unnecessary allocations |
| 4 | Missing XML Documentation | Find public APIs without XML doc comments |
| 5 | General Mistakes | Read random source files and find real bugs, logic errors, or code smells |
| 6 | Unused Using Directives | Find files with excessive using directives that could be cleaned up |
| 7 | Error Handling | Find bare `catch (Exception)` blocks that swallow errors |
| 8 | String Literals | Find hardcoded error strings that should be in `Properties.Resources` |

If the selected category has no actionable findings in the scan results, call `noop` — do NOT switch to a different category.

## Phase 2: Deep Analysis

Using the pre-collected sample data for the selected category, pick **one specific, well-scoped improvement**. Then do a deeper investigation:

1. **Read the actual source file(s)** involved to understand the full context
2. **Verify the issue is real** — not a false positive
3. **Determine the fix** — what specifically needs to change
4. **Scope it appropriately** — one issue should be completable in a single PR

### Category-Specific Guidance

#### TODO/FIXME/HACK Comments (Category 0)
- Pick a TODO that is clearly stale or has a concrete action
- Check if the TODO references an old bug number or feature that's already done
- The issue should ask to either implement the TODO or remove it if no longer relevant

#### Nullable Reference Types (Category 1)
- Pick a file that's a good candidate for `#nullable enable`
- Prefer files in `src/Xamarin.Android.Build.Tasks/` as they follow clear patterns
- Follow the repo's nullable conventions: no `!` operator, use `IsNullOrEmpty()` extension methods
- The issue should reference the repo's nullable guidelines

#### Obsolete API Usage (Category 2)
- Find calls to deprecated APIs and suggest the modern replacement
- Check if the obsolete message suggests an alternative

#### Performance Anti-patterns (Category 3)
- Look for real performance issues, not micro-optimizations
- **Sync-over-async**: `Task.Result`, `.Wait()`, `.GetAwaiter().GetResult()` in non-test code can cause deadlocks
- **String concatenation in loops**: `+=` on strings inside loops should use `StringBuilder`
- **Unnecessary allocations**: `.ToList()` or `.ToArray()` where the collection is only enumerated once
- **Repeated formatting**: `string.Format` or interpolation inside tight loops
- Verify the code is actually in a hot path or loop before filing — don't flag one-time startup code

#### Missing XML Documentation (Category 4)
- **ONLY** look at public APIs in `src/Mono.Android/` — this is the shipped product (Mono.Android.dll)
- **EXCLUDE** `Android.Runtime` namespace (plumbing/bridge types like `InputStreamInvoker`, `JNIEnv`)
- **EXCLUDE** `Java.Interop` namespace (low-level interop, not user-facing)
- Focus on types developers actually use: `Android.App`, `Android.Widget`, `Android.Views`, etc.
- Do NOT file issues for XML docs in build tasks, tools, or test code — those are internal
- The issue should provide example doc comments for the specific APIs

#### General Mistakes (Category 5)
- Read the randomly selected source files thoroughly
- Look for real bugs: logic errors, off-by-one, null dereferences, race conditions, resource leaks
- Look for code smells: dead code, unreachable branches, copy-paste errors, incorrect exception handling
- Do NOT file issues about formatting, whitespace, or style — those are not actionable
- The issue should describe the actual bug or problem with concrete evidence

#### Unused Using Directives (Category 6)
- Pick files with >10 using directives that likely have unused ones
- The issue should ask to clean up unnecessary usings

#### Error Handling (Category 7)
- Find `catch (Exception)` blocks that don't log or rethrow
- The issue should suggest proper error handling patterns

#### String Literals (Category 8)
- Find hardcoded strings in `LogError`/`LogWarning` calls
- The issue should ask to move them to `Properties.Resources` with proper `XA####` error codes
- **Important**: When new `XA####` error codes are created, the issue MUST also instruct to:
  1. Create a markdown doc file at `Documentation/docs-mobile/messages/xa####.md` (following the existing format: frontmatter with title/description/date/f1_keywords, then sections for Example messages, Issue explanation, and Solution)
  2. Add the new code to the table of contents in `Documentation/docs-mobile/messages/index.md`

## Phase 3: Create Issue

Create exactly **one** well-scoped issue using `create_issue`. The issue must be specific enough that Copilot can implement the fix without ambiguity.

### Issue Template

Use this structure:

```markdown
### Problem

[1-2 sentences describing what's wrong and why it matters]

### Location

- **File(s)**: `path/to/file.cs`
- **Line(s)**: [specific lines if applicable]

### Current Code

[Show the relevant code snippet]

### Suggested Fix

[Describe exactly what should change, with example code if possible]

### Guidelines

- [Any repo-specific conventions to follow]
- [Reference to relevant documentation]

### Acceptance Criteria

- [ ] [Specific, verifiable criteria]
- [ ] All tests pass
- [ ] No new warnings introduced
```

## Phase 4: Assign to Copilot

After creating the issue, use `assign_to_agent` to assign Copilot to work on it. You **MUST** pass the `issue_number` parameter — use the `temporary_id` from the `create_issue` call (**without** the `#` prefix). The safe-output is configured with `model: "claude-opus-4.6"` so Copilot will use Claude Opus 4.6 to implement the fix.

Example call sequence:
1. `create_issue` with `temporary_id: "aw_fix123"`, `title`, `body`
2. `assign_to_agent` with `issue_number: "aw_fix123"`

## Rules

1. **One issue per run** — Create exactly one issue, not multiple
2. **Be specific** — The issue must be implementable from the description alone
3. **Verify before filing** — Read the actual source to confirm the issue is real
4. **Skip non-actionable findings** — If nothing good is found in any category, call `noop`
5. **Respect repo conventions** — Follow dotnet/android formatting and coding style
6. **Don't duplicate** — Search for existing issues with similar titles before creating

## Important

You **MUST** end by calling exactly one set of safe output tools:

- **`create_issue` + `assign_to_agent`**: When a valid improvement is found
- **`noop`**: When no actionable improvement was found after checking all categories

```json
{"noop": {"message": "No actionable improvements found today after scanning all 10 categories."}}
```
