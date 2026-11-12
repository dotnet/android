---
name: pull-request
description: >-
  Writes dotnet/android commit messages and pull request titles/descriptions,
  and opens PRs in that format. Use when asked to open, create, or update a PR,
  write or improve a PR description or commit message, or prepare the squash
  commit message. Covers the title convention, Context/Fixes/Changes lines,
  narrative evidence-driven bodies, validation reporting, and dependency bumps.
---

# Pull requests and commit messages

PRs are squash-merged, and the PR title and description become the commit message on `main`. Write the description as the final commit message: something a maintainer can read in `git log` years later, without the PR page.

The repository guide is [`Documentation/workflow/commit-messages.md`](../../../Documentation/workflow/commit-messages.md). This skill summarizes it and records where recent practice differs.

## Before opening

  * Check for an existing PR on the branch or topic (`gh pr list --head <branch>`, `gh pr list --state all --search "<keywords>"`). Update it instead of opening a duplicate.
  * Follow `.github/PULL_REQUEST_TEMPLATE.md`. Keep its HTML comment (it is invisible), and don't add checklists that restate it.
  * Validate first (see the `tests` skill). The description must state what was run and the results.
  * Never `git commit --amend` or rewrite pushed history; add commits. Maintainers squash.
  * For dependent/stacked PRs, use the `gh-stack` skill.

## Title

```
[Component] Imperative summary of the change
```

  * Under ~70 characters, imperative mood, no trailing period.
  * `Component` is the directory holding the main change (`Mono.Android`, `Xamarin.Android.Build.Tasks`, `native`, `generator`, `Microsoft.Android.Sdk`, …) or a broad category: `build`, `ci`, `docs`, `tests`. Combine when needed: `[build, runtime]`.
  * Describe the nature of the fix, not the bug number.
  * Dependency bumps: `Bump to org/repo/branch@sha` or `Bump to <Dependency> <Version>`.
  * GitHub appends ` (#1234)` on squash; do not add it yourself.

## Body

### Header lines

Optional, at the top, in this order, one per line:

```
Changes: https://github.com/org/repo/compare/old...new
Fixes: https://github.com/dotnet/android/issues/1234
Context: #1200
Context: 0123456789ab
```

  * `Fixes:` uses a full URL and closes the issue. Use `Context:` instead when the issue should stay open.
  * `Context:` points to related PRs (`#1234` in this repo, `owner/repo#1234` elsewhere), commits in this repo (bare hash, not a URL), issues, announcements, or API diffs that a reader needs to reconstruct the background.
  * `Changes:` is the compare URL for dependency bumps.

### Narrative

Explain **why** before **what**. For bug fixes, tell the story:

 1. **Symptom:** what broke, who hit it, and how it surfaced. Quote the actual error message, stack trace, or failing command verbatim, so the commit is searchable when the bug recurs.
 2. **Cause:** what investigation established, and why it happened.
 3. **Fix:** what changed, why this approach was chosen over alternatives, and its constraints, trade-offs, compatibility notes, and known limitations.

Do not rely on linked URLs for any of this; summarize them, since links rot.

For changes with several parts, use `###` sections named after the actual topic (for example, `### Preserve the designer contract`, not `### Changes`). Mechanical, obvious changes may be subject-only.

### Evidence and validation

  * Include exact repro commands and outputs where they help.
  * For performance changes, include before/after measurements with the hardware, OS, SDK, configuration, and method, and state what the numbers do and don't cover.
  * State what was tested, with exact commands and results. If something was not run, say why.

### Style

  * Tense: present for this change; past for prior commits and investigation; future only for brief TODOs.
  * **Do not hard-wrap paragraphs.** Write each paragraph as one line and let renderers wrap it. (The repository guide's 70–72 column rule predates squash-merged PR descriptions; recent history does not wrap.)
  * Markdown is fine: inline code, tables for measurements, and code blocks for logs and commands.
  * Bullets are indented two spaces (`  * item`); numbered items are right-aligned (` 1. item`).
  * Code references: the `TargetName` target, `<TaskName/>`, `$(Property)`, `@(Item)`, a fully qualified `Type.Name` on first use and the short name afterwards, and `Method()`.
  * Avoid filler ("This PR…", "In summary"), lists of changed files, and restating the diff line by line.

## Patterns

Bug fix:

```
[Component] Fix <specific failure mode>

Fixes: https://github.com/dotnet/android/issues/NNNN
Context: #NNNN

<What broke, how it surfaced, and the verbatim error>

<Why it happened>

<What changed and why this fix is appropriate; limitations>

<Validation: exact commands and results>
```

Dependency bump:

```
Bump to org/repo/branch@sha

Changes: https://github.com/org/repo/compare/old...new

  * org/repo@abcdef01: [Area] Upstream change (#NN)
  * org/repo@abcdef02: [Area] Upstream change (#NN)

<Why this bump matters here>
```

List upstream commits only when there are fewer than about a dozen; always surface upstream bug fixes as `Context:` lines.

Platform or API update:

```
[Mono.Android] Bind API-NN <release>

Context: <announcement URL>
Context: <API diff URL>

<Release summary; compatibility, stability, or migration implications; what this repo does in response>
```

Removal or simplification:

```
[Component] Remove <unused thing>

<What it was, why it is no longer needed (evidence that nothing uses it), and what replaces it, if anything>
```

## Opening the PR

  * Push the branch, then create the PR with the app's PR tool when available, or `gh pr create --title "<title>" --body-file <file>` (write the body to a file to preserve formatting).
  * When the PR changes significantly, update the description so it still matches the final squash message.
