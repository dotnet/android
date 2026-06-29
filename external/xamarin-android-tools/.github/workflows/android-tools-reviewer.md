---
on:
  slash_command:
    name: review
    events: [pull_request_comment]
  roles: [admin, maintainer, write]

# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool

environment: copilot-pat-pool
permissions:
  contents: read
  pull-requests: read
engine:
  id: copilot
  model: claude-opus-4.8
  env:
    COPILOT_GITHUB_TOKEN: |
      ${{ case(
        needs.pat_pool.outputs.pat_number == '0', secrets.COPILOT_PAT_0,
        needs.pat_pool.outputs.pat_number == '1', secrets.COPILOT_PAT_1,
        needs.pat_pool.outputs.pat_number == '2', secrets.COPILOT_PAT_2,
        needs.pat_pool.outputs.pat_number == '3', secrets.COPILOT_PAT_3,
        needs.pat_pool.outputs.pat_number == '4', secrets.COPILOT_PAT_4,
        needs.pat_pool.outputs.pat_number == '5', secrets.COPILOT_PAT_5,
        needs.pat_pool.outputs.pat_number == '6', secrets.COPILOT_PAT_6,
        needs.pat_pool.outputs.pat_number == '7', secrets.COPILOT_PAT_7,
        needs.pat_pool.outputs.pat_number == '8', secrets.COPILOT_PAT_8,
        needs.pat_pool.outputs.pat_number == '9', secrets.COPILOT_PAT_9,
        'NO COPILOT PAT AVAILABLE')
      }}
max-daily-ai-credits: -1
max-ai-credits: -1
network:
  allowed:
    - defaults
    - dotnet
    - github
    - "aka.ms"
    - "microsoft.com"
tools:
  github:
    github-token: ${{ secrets.GITHUB_TOKEN }}
    toolsets: [pull_requests, repos]
    # Allow reading PR content from external/first-time contributors.
    # The /review command is gated to maintainers, so only trusted users can trigger it.
    min-integrity: none
safe-outputs:
  github-token: ${{ secrets.GITHUB_TOKEN }}
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
