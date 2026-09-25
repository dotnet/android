# Flaky-CI issue matching

Use this reference after classifying a failure as `known-flaky-test`, `transient-infrastructure`, or `timeout-or-crash`. Search automatically, but do not comment on or create an issue until the user explicitly confirms.

## Canonical fingerprints

Build a stable fingerprint before searching:

- **Test:** fully-qualified test name, assembly, normalized first assertion/error, OS/flavor.
- **Infrastructure:** provider/service token, HTTP/error code, failed task, stage/OS.
- **Crash/timeout:** signal or timeout cap, lane/test family, last-started test when known.

Remove volatile values: build/activity IDs, GUIDs, timestamps, agent numbers, absolute paths, line numbers, and duplicate copies of the same root message from multiple projects/configurations.

Examples:

```text
test|system-nettests-ssltest-httpsshouldwork|http-504
azure-artifacts|atcpu|http-500|prepare-solution
hosted-agent|disconnect|msbuild-emulator-11
azure-job-timeout|240m|macos-build
```

## Search strategy

Run separate exact searches and merge/deduplicate the results. Broad `OR` searches are less reliable and can be rejected by GitHub search syntax.

For a test:

```bash
gh issue list --repo dotnet/android --state all --label flaky-tests \
  --search "\"$FULL_TEST_NAME\" sort:updated-desc" \
  --json number,title,state,labels,createdAt,updatedAt,body,url

gh issue list --repo dotnet/android --state all \
  --search "\"$SHORT_TEST_NAME\" \"$ERROR_TOKEN\" sort:updated-desc" \
  --json number,title,state,labels,createdAt,updatedAt,body,url
```

For infrastructure/crash:

```bash
gh issue list --repo dotnet/android --state all --label flaky-ci \
  --search "\"$PROVIDER_TOKEN\" \"$TASK_OR_STAGE\" sort:updated-desc" \
  --json number,title,state,labels,createdAt,updatedAt,body,url

gh issue list --repo dotnet/android --state all \
  --search "\"$ERROR_TOKEN\" \"$PLATFORM\" sort:updated-desc" \
  --json number,title,state,labels,createdAt,updatedAt,body,url
```

Search order:

1. Exact fully-qualified test or provider-specific token (`ATCPU`, exact signal/error).
2. Normalized error code/message plus task/stage.
3. Class/method or broader failure family.
4. Inventory issues such as #12704 only as history/frequency fallback.

## Candidate scoring

Score each candidate from title and body:

| Match | Score |
|---|---:|
| Exact test/provider fingerprint | +45 |
| Exact normalized root error/code | +25 |
| Same task/stage/platform | +10 |
| `flaky-tests` or `flaky-ci` label | +10 |
| Open, or updated in the last 180 days | +10 |

- **75+**: recommend updating the most recently updated exact tracker.
- **55–74**: show the candidates; require a human choice.
- **Below 55**: recommend opening a new issue.

Prefer an open exact tracker over a newer broad inventory. For closed issues:

- If it was recently closed as fixed and the exact signature recurred after the fix, recommend a recurrence comment/reopen (when permitted).
- If it is old, superseded, or materially different, create a new issue and link the old one.

## Updating an existing issue

“Update” means add a comment; do not rewrite the issue body. Include:

- PR, build, source SHA, stage/job/task, OS/configuration, and direct links.
- Normalized fingerprint and concise root error.
- Classification, confidence, and the evidence supporting it.
- Same-build sibling/retry outcomes.
- Targeted retry result once known.
- A short log excerpt; never paste large logs or credentials.

Template:

~~~~markdown
Observed again in PR #<pr>, [build <id>](<url>) (`<sha>`).

- Stage/job: `<stage>` / `<job>`
- Fingerprint: `<fingerprint>`
- Classification: `<category>` (`<confidence>`)
- Evidence: <why this is the same failure>
- Retry: <not attempted / stage retry pending / passed / failed again>

```text
<short normalized log excerpt>
```
~~~~

## Opening a new issue

Use:

- `[Flaky test] <test>: <root signature>` with `needs-triage,flaky-tests`
- `[CI] <stage/task>: <root signature>` with `needs-triage,flaky-ci`

Follow the repository's “Other” issue-form headings:

~~~~markdown
### Android framework version

N/A for hosted infrastructure, otherwise the affected target framework.

### Affected platform version

Public `dotnet-android` PR pipeline, build <id>, <stage/job>, <OS/configuration>.

### Description

What failed, the normalized fingerprint, classification/confidence, and why it is not currently attributed to the PR.

### Steps to Reproduce

List the observed occurrence(s). Do not claim deterministic local reproduction when none exists.

### Did you find any workaround?

Targeted retry of failed jobs in stage `<stageRefName>`; include the result if known.

### Relevant log output

```text
<short root-cause excerpt>
```

### Related searches

List near matches and why they are not the same tracker.
~~~~
