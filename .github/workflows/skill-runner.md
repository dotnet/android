---
on:
  pull_request:
    paths:
    # Sentinel path that never matches. Keeping a pull_request trigger here is
    # required so gh-aw emits a pre_activation job (which shared/pat_pool.md's
    # pat_pool job depends on), but actually running on PRs would fail because
    # the copilot-pat-pool environment rejects PR refs via protection rules.
    - .github/workflows/skill-runner.__never_matches__
  schedule:
  - cron: weekly on monday around 03:00
  workflow_dispatch:
    inputs:
      skill:
        description: Skill to run (leave blank for random)
        options:
        - ""
        - "update-androidsdk-packages"
        required: false
        type: choice
  needs:
    - workflow_guard
permissions:
  contents: read
  issues: read
  pull-requests: read
concurrency:
  group: skill-runner
  cancel-in-progress: false
  queue: max
# ###############################################################
# Select a PAT from the pool and override COPILOT_GITHUB_TOKEN.
# Run agentic jobs in an isolated `copilot-pat-pool` environment.
#
# When org-level billing is available, this will be removed.
# See `shared/pat_pool.README.md` for more information.
# ###############################################################
#
# The PAT pool authenticates Copilot requests only. Repository writes use the
# workflow GITHUB_TOKEN, so generated commits and PRs are authored by
# github-actions[bot].
imports:
  - uses: shared/pat_pool.md
    with:
      environment: copilot-pat-pool

environment: copilot-pat-pool
# This workflow intentionally operates only from main. Manual dispatches from any
# other ref are rejected before activation, and PR-context checkout is disabled
# so generated workflow context cannot swap the workspace onto a branch checkout.
checkout:
  - fetch-depth: 0
    ref: main
jobs:
  workflow_guard:
    runs-on: ubuntu-slim
    permissions: {}
    steps:
      - name: Validate workflow dispatch context
        if: github.event_name == 'workflow_dispatch' && (github.ref != 'refs/heads/main' || fromJSON(github.event.inputs.aw_context || '{}').item_type == 'pull_request')
        env:
          WORKFLOW_REF: ${{ github.ref }}
        run: |
          echo "This workflow only accepts main-branch dispatches without PR context (ref: $WORKFLOW_REF)." >&2
          exit 1
  activation:
    if: github.event_name != 'workflow_dispatch' || github.ref == 'refs/heads/main'
  conclusion:
    permissions:
      issues: write
network:
  allowed:
  - defaults
  - github
  - dotnet
  - java
safe-outputs:
  github-token: ${{ secrets.GITHUB_TOKEN }}
  create-pull-request:
    allowed-base-branches:
    - main
    allowed-files:
    - Configuration.props
    - src/androidsdk/androidsdk.targets
    auto-close-issue: false
    draft: false
    fallback-as-issue: false
    labels:
    - automated
    - skill-runner
    max-patch-files: 5
    title-prefix: "[skill-runner] "
  create-issue:
    title-prefix: "[skill-runner] "
    labels:
    - automated
    - skill-runner
    close-older-issues: true
    expires: 30
  missing-data:
    create-issue: false
  missing-tool:
    create-issue: false
  noop:
    report-as-issue: true
  report-incomplete:
    create-issue: false
  report-failure-as-issue: true
steps:
  - name: Enforce unique skill update PR
    env:
      GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
    run: |
      existing_prs="$(gh api --paginate "repos/${GITHUB_REPOSITORY}/pulls?state=open&base=main&per_page=100" \
        --jq '.[] | select(.title == "[skill-runner] update-androidsdk-packages") | .number')"
      if [ -n "$existing_prs" ]; then
        printf '{"type":"noop","message":"An open [skill-runner] update-androidsdk-packages PR already exists against main: #%s. No duplicate PR will be created."}\n' \
          "$(printf '%s' "$existing_prs" | paste -sd, -)" >> "$GH_AW_SAFE_OUTPUTS"
      fi
  - name: Select skill and bootstrap prerequisites
    env:
      INPUT_SKILL: ${{ inputs.skill }}
    run: |
      mkdir -p /tmp/gh-aw/agent
      SKILL_ROOT=".github/skills"
      # The set of skills this workflow is allowed to run unattended. Keep this
      # in sync with the workflow_dispatch `skill` options list above. This is
      # an allowlist: an explicit dispatch input must still be a member of this
      # array, so a stray/unregistered skill directory can never be run just by
      # naming it in workflow_dispatch, even if it happens to exist on disk.
      ELIGIBLE_SKILLS=("update-androidsdk-packages")
      COUNT=${#ELIGIBLE_SKILLS[@]}
      if [ "$COUNT" -eq 0 ]; then
        echo "❌ ELIGIBLE_SKILLS is empty — nothing to run." >&2
        exit 1
      fi
  
      if [ -n "$INPUT_SKILL" ]; then
        SKILL_NAME=""
        for candidate in "${ELIGIBLE_SKILLS[@]}"; do
          if [ "$candidate" = "$INPUT_SKILL" ]; then
            SKILL_NAME="$candidate"
            break
          fi
        done
        if [ -z "$SKILL_NAME" ]; then
          echo "❌ Requested skill '$INPUT_SKILL' is not in ELIGIBLE_SKILLS: ${ELIGIBLE_SKILLS[*]}" >&2
          exit 1
        fi
      else
        # No explicit selection (scheduled run, or manual dispatch left blank):
        # pick uniformly at random from the eligible skills, same as
        # nightly-fix-finder does for its scan scripts. With only one skill
        # registered today this always picks it; once more are added, each
        # unselected run picks one at random.
        SKILL_NAME="${ELIGIBLE_SKILLS[$((RANDOM % COUNT))]}"
      fi
  
      SKILL_PATH="$SKILL_ROOT/$SKILL_NAME/SKILL.md"
      if [ ! -f "$SKILL_PATH" ]; then
        echo "❌ Requested skill not found: $SKILL_PATH" >&2
        exit 1
      fi
      echo "$SKILL_NAME" > /tmp/gh-aw/agent/selected-skill.txt
      echo "✅ Selected skill: $SKILL_NAME ($SKILL_PATH)"
  
      # Skill-specific prerequisite bootstrap. androidsdk.csproj requires the
      # BootstrapTasks assembly to evaluate at all, so build it up front whenever
      # that skill is selected. Add an `elif` here for future skills that need
      # their own bootstrap step, rather than special-casing it in the prompt.
      if [ "$SKILL_NAME" = "update-androidsdk-packages" ]; then
        dotnet build build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks.csproj -v:minimal
      fi
description: Weekly (or on-demand) runner that executes a selectable repository Copilot skill end to end, opens a PR for validated changes, and always reports outcome/errors on a tracking issue
model: gpt-5.6-sol
engine:
  id: copilot
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
strict: true
timeout-minutes: 120
tools:
  edit:
  bash: ["*"]
  github:
    github-token: ${{ secrets.GITHUB_TOKEN }}
    mode: gh-proxy
    min-integrity: none
    toolsets:
    - repos
    - issues
    - pull_requests
    - search
---

# Skill Runner

You are the Skill Runner Agent — a generic executor for repository Copilot skills that need to run
unattended on a schedule or on demand, validate their own work, open a PR when changes are warranted,
and always report the outcome (including no-ops and errors) on a tracking issue.

This workflow is intentionally skill-agnostic. Today it only runs `update-androidsdk-packages`, but
it is designed to grow: once more than one skill is registered, an unselected run (scheduled, or
manual dispatch with the dropdown left blank) picks one at random, the same way `nightly-fix-finder`
randomly picks a scan script. Explicitly picking a skill from the `workflow_dispatch` dropdown always
runs that one.

## Current Context

- **Repository**: ${{ github.repository }}
- **Selected skill name**: `/tmp/gh-aw/agent/selected-skill.txt` (written by the `Select skill and
  bootstrap prerequisites` step above — read this file first). It reflects whichever skill was
  explicitly chosen via `workflow_dispatch`, or a random pick among the eligible skills when the run
  was scheduled or dispatched without a selection.
- **Skill path**: `.github/skills/<selected-skill>/SKILL.md`.
- Any skill-specific prerequisite (for example building
  `build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks.csproj`,
  which `androidsdk.csproj` requires to evaluate at all) has already been run for you by that same
  step, keyed off the selected skill name.

## Mission

Each run:

1. Read `/tmp/gh-aw/agent/selected-skill.txt` to determine which skill to run this time.
2. Load and follow that skill's `SKILL.md` **in full, exactly as written** — it is the authoritative
   process for whatever task it describes, including any hard rules it defines. Do not improvise or
   substitute your own approach for what the skill documents.
3. If the skill's process results in a validated change: implement it, validate it fully per the
   skill's own instructions, commit, and open exactly one PR.
4. Regardless of whether anything changed, report the outcome on the tracking issue described below.
   This includes genuine no-op runs, and it includes surfacing anything the skill's own rules say must
   always be reported even when nothing else changed (see the skill table below for known examples).
5. If anything fails (network error, validation failure, ambiguous data, a rule in the skill you
   cannot satisfy confidently), stop, do not open a broken PR, and report the failure on the tracking
   issue instead.

## Known Skills

Consult this table for skill-specific context that supplements (never overrides) the skill's own
`SKILL.md`. Add a row here whenever a new skill is wired into this workflow's dropdown.

| Skill | SKILL.md | Notes for this workflow |
|---|---|---|
| `update-androidsdk-packages` | `.github/skills/update-androidsdk-packages/SKILL.md` | Refreshes stable Android SDK package pins in `Configuration.props` / `src/androidsdk/androidsdk.targets` for `androidsdk.csproj`. Hard rules: never touch the NDK; never add a new platform API level to `_PlatformPackage`, but **always** report in the tracking issue when a newer stable platform level exists upstream (e.g. platform 37.1), whether or not anything else changed. Validation must build `build-tools/Xamarin.Android.Tools.BootstrapTasks/Xamarin.Android.Tools.BootstrapTasks.csproj` before `src/androidsdk/androidsdk.csproj` — already done by the bootstrap step above. |

## Phase 1: Follow the Selected Skill

Read the selected skill's `SKILL.md` now and execute its documented workflow end to end. Use any
scripts it bundles (for example `dotnet run *.cs` helpers) exactly as documented there rather than
reimplementing equivalent logic ad hoc. Do not deviate from any hard rule the skill defines — treat
those as non-negotiable regardless of how the task otherwise seems to be going.

## Phase 2: Validate

Follow the skill's own validation steps in full before committing anything. If validation cannot pass
(build failure, ambiguous data, network failure, or any other blocker), do not commit or open a PR —
go straight to Phase 4 and report the failure instead.

## Phase 3: Commit and Open the PR (only if changes were made and validated)

1. Immediately before creating a PR, run a deterministic open-PR check keyed to this workflow and the
   selected skill. Search all open PRs against `main` using the exact workflow title prefix, with a
   paginated query that is not limited to the first page and not derived from a loose semantic match:

   `gh pr list --state open --base main --limit 100 --search '"[skill-runner]" "<skill-name>"' --json number,title,headRefName,body`

   Repeat/paginate until exhausted, then compare every returned title against the exact prefix
   `"[skill-runner] <skill-name>"`. If any open PR matches that same workflow/skill key, do not create
   another PR; report a no-op with the existing PR number and continue to Phase 4. Do not fall back to
   a looser heuristic or a manually judged "equivalent" match. The uniqueness check is mandatory and
   must be enforced before any `create_pull_request` call.
2. Commit with a concise message describing exactly what changed, per the skill's own guidance, ending
   in:

   `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`

   The workflow configures Git as `github-actions[bot]` — keep that author identity.
3. Call `create_pull_request` exactly once. Use a short branch name, explicitly target `main`, and use a PR body with this
   structure:

   ```markdown
   > AI-generated update. Produced by the `skill-runner` agentic workflow running the `<skill-name>` skill.

   ### Changes

   [What changed, following the selected skill's own summary format]

   ### Validation

   [Exact build/test commands run and their results]

   ### Notes

   [Anything the skill's rules require you to always report, e.g. a newer platform level found upstream]
   ```

## Phase 4: Report on the Tracking Issue (every run, unconditionally)

Regardless of whether a PR was opened, report the outcome via `create_issue`. Because
`close-older-issues: true` and a stable `title-prefix` are configured, this naturally supersedes the
previous run's report instead of piling up duplicate issues.

Always include, in every report:

- **Skill run**: which skill ran this time (from `/tmp/gh-aw/agent/selected-skill.txt`).
- **Run outcome**: one of "Changes found and PR opened (#N)", "No-op — nothing needed updating", or
  "Failed — [reason]".
- **Always-report notes**: anything the selected skill's own rules require surfacing every run
  regardless of outcome (see the Known Skills table above — for `update-androidsdk-packages` this is
  the platform-catalog check: state explicitly whether a newer stable platform level exists upstream
  beyond the highest one already in `_PlatformPackage`, or that none does). Never omit this, even on a
  pure no-op or failed run where you got far enough to check.
- **Errors**, if any occurred. Include enough detail (command, error text) for a human to act on it.

## Rules

1. **One PR per run at most** — never open more than one PR, and only when changes were made and
   validated successfully.
2. **Always report** — every run ends with a `create_issue` call summarizing the outcome, including
   pure no-ops and failures. This workflow's whole purpose is to keep a human informed even when
   nothing changed.
3. **Never skip a skill's always-report rules** — if the selected skill defines something that must be
   surfaced every run (like a newer platform level upstream), include it in the tracking issue every
   time, regardless of whether the run's trigger mentioned it.
4. **Never deviate from a skill's hard rules** — whatever the selected `SKILL.md` marks as a hard
   rule or exclusion is non-negotiable.
5. **Validate before opening a PR** — do not open a PR unless the selected skill's own validation
   steps pass.
6. **Respect repo conventions** — follow dotnet/android formatting, testing, and MSBuild rules
   regardless of which skill is running.
7. **Stay skill-agnostic** — do not hardcode assumptions about `update-androidsdk-packages` into your
   reasoning beyond what the Known Skills table documents; when a new skill is added, follow its
   `SKILL.md` on its own terms.

## Important

You **MUST** end by calling `create_issue` exactly once (the tracking report), and additionally
`create_pull_request` exactly once if — and only if — validated changes were made this run.

```json
{"create_issue": {"title": "Weekly scan", "body": "Skill run: update-androidsdk-packages\n\nNo-op — catalog already matches Google's current stable releases.\n\nPlatform catalog check: no newer stable platform level exists upstream beyond platform 37.0, the highest entry in _PlatformPackage."}}
```

## Adding a New Skill

1. Confirm the skill has a `.github/skills/<name>/SKILL.md` that is fully self-contained and safe to
   run unattended (it should validate its own work and not require a human in the loop mid-run).
2. Add the skill name to both the `workflow_dispatch` → `skill` → `options` list and the
   `ELIGIBLE_SKILLS` bash array in the `Select skill and bootstrap prerequisites` step at the top of
   this file, so it appears in the GitHub Actions UI dropdown and is eligible for random selection.
3. Add a row to the Known Skills table above summarizing any always-report rules, hard rules, or
   prerequisite bootstrap steps a human maintaining this workflow needs to know about.
4. If the skill needs a prerequisite build/bootstrap step (like `update-androidsdk-packages` needs
   `Xamarin.Android.Tools.BootstrapTasks.csproj` built first), add an `elif` branch to the `Select
   skill and bootstrap prerequisites` step's shell script.
5. If the skill touches files outside `Configuration.props`/`src/androidsdk/androidsdk.targets`,
   update `safe-outputs.create-pull-request.allowed-files` to include them.
6. Run `gh aw compile` to regenerate `skill-runner.lock.yml`.
