---
name: android-reviewer
description: >-
  Review dotnet/android PRs against established rules. Trigger on "review this PR",
  a GitHub PR URL, or code review requests. Checks MSBuild, nullable, async, security,
  error handling, formatting, performance, native code.
---

# Android PR Reviewer

Review PRs against guidelines distilled from past reviews by senior maintainers of dotnet/android.

## Review Mindset

Be polite but skeptical. Prioritize bugs, performance regressions, safety issues, and pattern violations over style nitpicks. **3 important comments > 15 nitpicks.**

Flag severity clearly in every comment:
- ❌ **error** — Must fix before merge. Bugs, security issues, missing error codes, broken incremental builds.
- ⚠️ **warning** — Should fix. Performance issues, missing validation, inconsistency with patterns.
- 💡 **suggestion** — Consider changing. Style, readability, optional improvements.

**Every review should produce at least one inline comment.** Even clean PRs have opportunities for improvement — code consolidation, missing edge-case tests, perf micro-optimizations, or documentation gaps. Use 💡 suggestions for these. A review with zero comments appears superficial and misses the chance to share knowledge. Only omit inline comments if the PR is truly trivial (e.g., a 1-line typo fix or dependency bump).

## Workflow

### 1. Identify the PR

If triggered from an agentic workflow (slash command on a PR), use the PR from the event context. Otherwise, extract `owner`, `repo`, `pr_number` from a URL or reference provided by the user.
Formats: `https://github.com/{owner}/{repo}/pull/{number}`, `{owner}/{repo}#{number}`, or bare number (defaults to `dotnet/android`).

### 2. Gather context (before reading PR description)

```
gh pr diff {number} --repo {owner}/{repo}
gh pr view {number} --repo {owner}/{repo} --json files
```

For each changed file, read the **full source file** (not just the diff) to understand surrounding invariants, call patterns, and data flow. If the change modifies a public/internal API or utility, search for callers. Check whether sibling types need the same fix.

**Form an independent assessment** of what the change does and what problems it has *before* reading the PR description.

### 3. Incorporate PR narrative and reconcile

```
gh pr view {number} --repo {owner}/{repo} --json title,body
```

Now read the PR description and linked issues. Treat them as claims to verify, not facts to accept. Where your independent reading disagrees with the PR description, investigate further. If the PR claims a performance improvement, require evidence (benchmarks, profiling data). If it claims a bug fix, verify the bug exists and the fix addresses root cause — not symptoms.

### 4. Check CI status

```
gh pr checks {number} --repo {owner}/{repo}
```

Review the CI results. **Never post ✅ LGTM if any required CI check is failing or if the code doesn't build.** If CI is failing:
- Investigate the failure using the **azdo-build-investigator** skill (for Azure DevOps pipeline failures) or GitHub Actions job logs.
- If the failure is caused by the PR's code changes, flag it as ❌ error.
- If the failure is a known infrastructure issue or pre-existing flake unrelated to the PR, note it in the summary but still use ⚠️ Needs Changes — the PR isn't mergeable until CI is green.
- If **all public CI checks pass** but only the internal `Xamarin.Android-PR` check is failing, still use ⚠️ Needs Changes with a note that the internal pipeline may need a re-run. Do not give ✅ LGTM.
- If the PR description acknowledges the failure and documents a dependency (e.g., "blocked on X"), note it in the summary.

### 5. Load review rules

Based on the file types identified in step 2, read the appropriate rule files from this skill's `references/` directory.

**Always load:**
- `references/repo-conventions.md` — Formatting, style, and patterns specific to this repository.
- `references/ai-pitfalls.md` — Common AI-generated code mistakes.

**Conditionally load based on changed file types:**
- `references/csharp-rules.md` — When any `.cs` files changed. Covers nullable, async, error handling, performance, and code organization.
- `references/msbuild-rules.md` — When `.targets`, `.props`, `.projitems`, or `.csproj` files changed, or when MSBuild task C# files changed (e.g., files under `src/Xamarin.Android.Build.Tasks/`).
- `references/native-rules.md` — When `.c`, `.cpp`, `.h`, or `.hpp` files changed. Covers memory management, C++ best practices, symbol visibility, and platform-specific code.
- `references/interop-rules.md` — When both C# and native files changed, or when the diff contains P/Invoke or JNI interop code (e.g., `DllImport`, `[Register]` attribute changes, `JNIEnv` calls, `[MarshalAs]`, `[StructLayout]`, or marshal-related changes in files under `src/Mono.Android/` or `src/native/`).
- `references/testing-rules.md` — When test files changed (e.g., files under `tests/`, `**/Tests/`, or test project directories).
- `references/security-rules.md` — When any code files changed (C#, C/C++, or MSBuild).

### 6. Analyze the diff

For each changed file, check against the review rules. Record issues as:

```json
{ "path": "src/Example.cs", "line": 42, "side": "RIGHT", "body": "..." }
```

**What to look for (in priority order):**
1. **Bugs & correctness** — race conditions, null dereferences, off-by-one, logic errors
2. **Safety** — thread safety, resource leaks, security vulnerabilities
3. **Performance** — O(n²) patterns, unnecessary allocations, missing caches
4. **Missing tests** — untested error paths, edge cases, missing regression tests for bug fixes
5. **Code duplication** — near-identical methods that should be consolidated
6. **Consistency** — dedup patterns mixed within the same PR, API return types inconsistent with repo conventions
7. **Documentation** — misleading comments, undocumented behavioral decisions

Constraints:
- Only comment on added/modified lines in the diff — the API rejects out-of-range lines.
- `line` = line number in the NEW file (right side). Double-check against the diff.
- One issue per comment.
- **Don't pile on.** If the same issue appears many times, flag it once with a note listing all affected files.
- **Don't flag what CI catches.** Skip compiler errors, formatting the linter will catch, etc.
- **Avoid false positives.** Verify the concern actually applies given the full context. If unsure, phrase it as a question rather than a firm claim.

### 7. Post the review

Post your findings directly:

- **Inline comments** on specific lines of the diff with the severity, category, and explanation.
- **Review summary** with the overall verdict (✅ LGTM, ⚠️ Needs Changes, or ❌ Reject), issue counts by severity, and positive callouts.

If no issues found **and CI is green**, submit with at most one or two 💡 suggestions and a positive summary. Truly trivial PRs (dependency bumps, 1-line typo fixes) may have no inline comments.

**Copilot-authored PRs:** If the PR author is `Copilot` (the GitHub Copilot coding agent) and the verdict is ⚠️ Needs Changes or ❌ Reject, prefix the review summary with `@copilot ` so the comment automatically triggers Copilot to address the feedback. Do NOT add the prefix for ✅ LGTM verdicts.

## Comment format

```
🤖 {severity} **{Category}** — {What's wrong and what to do instead.}

_{Rule: Brief name (Postmortem `#N`)}_
```

Where `{severity}` is ❌, ⚠️, or 💡. Always wrap `#N` in backticks so GitHub doesn't auto-link to issues.

**Categories:** MSBuild tasks · MSBuild targets · Nullable · Async pattern · Error handling · Resource management · Security · Formatting · Performance · Code organization · Patterns · Native memory · Native C++ · Symbol visibility · Platform-specific · Testing · YAGNI · API design · Documentation
