---
on:
  slash_command:
    name: review
    events: [pull_request_comment]
  roles: [admin, maintainer, write]
permissions:
  contents: read
  pull-requests: read
engine:
  id: copilot
  model: claude-opus-4.6
network:
  allowed:
    - defaults
    - dotnet
    - github
    - "aka.ms"
    - "microsoft.com"
tools:
  github:
    toolsets: [pull_requests, repos]
    # Allow reading PR content from external/first-time contributors.
    # The /review command is gated to maintainers, so only trusted users can trigger it.
    min-integrity: none
safe-outputs:
  create-pull-request-review-comment:
    max: 50
  submit-pull-request-review:
    max: 1
    allowed-events: [COMMENT, REQUEST_CHANGES]
---

# Android Tools PR Reviewer

A maintainer commented `/review` on this pull request. Perform a thorough code review following the dotnet/android-tools review guidelines.

## Instructions

1. Read the review methodology from `.github/skills/android-tools-reviewer/SKILL.md` — this defines the review workflow (including which rule files to load), mindset, severity levels, and comment format.
2. Follow the skill's workflow to analyze the pull request:
   - Gather context: read the diff and changed files
   - For each changed file, read the **full source file** to understand surrounding context
   - Form an independent assessment before reading the PR description
   - Read the PR title and description — treat claims as things to verify
   - Check CI status
   - Analyze the diff against the review rules
3. Post your findings as inline review comments and a review summary.

## Constraints

- **Always post at least one inline review comment**, even for clean PRs — add a brief 💡 observation on a key implementation line. The review framework requires at least one inline comment to submit the review.
- Only comment on added/modified lines visible in the diff.
- One issue per inline comment.
- If the same issue appears many times, flag it once listing all affected files.
- Don't flag what CI catches (compiler errors, linter issues).
- Avoid false positives — verify concerns given the full file context.
- **Never submit an APPROVE event.** Use COMMENT for clean PRs and REQUEST_CHANGES when issues are found.
- Prioritize: bugs > safety > performance > missing tests > duplication > consistency > documentation.
