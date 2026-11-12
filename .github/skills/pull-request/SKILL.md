---
name: pull-request
description: >-
  Writes dotnet/android commit messages and PR titles/descriptions, and opens
  PRs in that format. Use when opening, creating, or updating a PR, or writing
  a PR description, commit message, or squash message.
---

# Pull requests and commit messages

PRs are squash-merged, so the PR title and description become the commit message on `main`. Write it for a maintainer reading `git log` years later, without the PR page. Base rules are in `Documentation/workflow/commit-messages.md`; where it differs from the notes below, follow these notes (recent practice).

## Workflow

```
- [ ] No existing PR covers this (gh pr list --state all --search "<keywords>")
- [ ] Validated (tests skill); commands and results recorded
- [ ] Title and body follow the format below and the PR template
- [ ] Committed and pushed (git push -u origin HEAD) BEFORE creating the PR
- [ ] PR opened with the body from a file
```

Creating a PR from an unpushed branch fails with `422 ... field: head, code: invalid`; push first, then create.

- Follow `.github/PULL_REQUEST_TEMPLATE.md`: keep its HTML comment and don't add checklists.
- Never amend or rewrite pushed history; maintainers squash. For stacked PRs, use the `gh-stack` skill.
- Open with the app's PR tool, or `gh pr create --title "..." --body-file <file>`. Update the description when the change evolves.

## Format

**Title:** `[Component] Imperative summary`, under ~70 characters. `Component` is the main directory (`Mono.Android`, `Xamarin.Android.Build.Tasks`, `native`, …) or `build`, `ci`, `docs`, `tests`. Don't put bug numbers in the title, and don't add `(#NNNN)`; GitHub appends it. Dependency bumps: `Bump to org/repo/branch@sha`.

**Header lines** (optional, in this order):

- `Changes:` compare URL for bumps. List upstream commits if fewer than ~12; surface their bug fixes as `Context:`.
- `Fixes:` full issue URL. It closes the issue; use `Context:` otherwise.
- `Context:` related PRs (`#1234`, `owner/repo#1234`), commits in this repo (bare hash), announcements, or API diffs.

**Body:** why before what.

- **Fixes:** the symptom with verbatim errors or repro commands, then the cause, then the fix, including alternatives, trade-offs, compatibility, and limitations.
- **Removals:** evidence that nothing uses the code.
- **Performance:** before/after numbers with hardware, SDK, configuration, and method.
- **Validation:** end with the commands run and their results, or why something wasn't run.

Summarize linked content rather than relying on it. Use `###` sections named after topics for multi-part changes. Mechanical changes may be subject-only.

**Style:**

- Don't hard-wrap paragraphs.
- Markdown tables and code blocks are fine.
- Bullets are `  * item`.
- Refer to the `Name` target, `<Task/>`, `$(Property)`, and `@(Item)`.
- No "This PR…", file lists, or diff narration.

## Examples

Read the full message of a real commit with `git show --no-patch <sha>`:

- `a3b7c32f1b`: bug fix with `Context:` and validation results.
- `39cc235cd3`: removal with evidence and an intentional-limitation note.
- `99e88d8a378d`: multi-section change with a measurements table.
