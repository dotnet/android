# Failure Classification (dotnet/android)

Load this file whenever a `dotnet-android` build is red. Classify each independent root failure from evidence; do not classify from an error-code prefix alone.

## Categories and confidence

| Category | Meaning | Default action |
|---|---|---|
| `likely-pr-regression` | The PR probably introduced or exposed the failure. | Investigate/fix; do not recommend retry as the primary action. |
| `known-flaky-test` | The test has direct retry/cross-build evidence of nondeterminism or an exact flaky-test tracker. | Targeted retry; update the matching tracker. |
| `transient-infrastructure` | The hosted agent, Azure service, feed, network, checkout, disk, emulator transport, or other external resource failed. | Targeted retry; update/open a `flaky-ci` tracker if recurring. |
| `timeout-or-crash` | Azure terminated the job, a runner died/incompletely published results, or native crash/hang evidence exists. | Inspect logcat when needed, then targeted retry if no PR-causal evidence exists. |
| `unknown` | Evidence is missing, weak, generic, or contradictory. | Diagnose before retrying. |

Use numeric confidence in reports and machine-readable output:

- **High: `0.80`–`1.00`** — state the classification directly.
- **Medium: `0.60`–`0.79`** — say “likely” and name the missing corroboration.
- **Low: below `0.60`** — emit `unknown`, even if one weak pattern points elsewhere.

## Evidence precedence

Evaluate evidence in this order. Higher rows override lower rows.

1. **Provider/root-cause text** from failed task/job logs: Azure service response, hosted-agent disconnect, disk exhaustion, native signal, exact timeout message.
2. **Retry/history evidence**: failed then passed in the same build, unchanged-SHA rerun success, or recurrence across unrelated PRs.
3. **Cross-configuration evidence**: the exact test fails or passes in sibling OS/flavor runs.
4. **PR overlap**: the changed component, test, target, or generator directly matches the failure.
5. **Generic surface error**: `NU####`, `XA####`, `CS####`, `Assert`, `TimeoutException`, cancellation, or task exit code.

Never let row 5 override stronger evidence. For example:

- `NU1301` + Azure Artifacts HTTP 500 + `ATCPU` rate limiting is **infrastructure**, not a package regression.
- `NU1101` for a stable package/version newly referenced by the PR can be a **PR regression**.
- `XA####`, `Assert`, or `TimeoutException` can be product, test, device, or infrastructure failures; inspect the source context.

## High-confidence rules

### Likely PR regression

Use `likely-pr-regression` at high confidence when at least one applies:

- A compiler/build error names a changed source/project/target and the root message is deterministic.
- The exact test fails in two or more independent configurations, never passes in a sibling/retry, and the PR changes the implicated component.
- A previously passing lane now fails with a deterministic assertion whose test/product path directly overlaps the PR diff.

Branch/file-name overlap alone is only a lead. Without deterministic failure evidence it is not enough for high confidence.

### Known flaky test

Use `known-flaky-test` when at least one applies:

- The exact fully-qualified test plus normalized root error matches a current/recent `flaky-tests` issue.
- The exact test changes from `Failed` to `Passed` on retry without a code change.
- The same signature recurs across unrelated PRs or appears as a non-gating failure in otherwise-green builds.

A one-platform failure with sibling passes is medium evidence (`0.60`–`0.79`), not proof by itself. Search for a tracker before calling it known.

### Transient infrastructure

Strong infrastructure signatures include:

- `ATCPU`, Azure Artifacts/DevOps HTTP `429`/`5xx`, no-route-to-host, DNS/service-index outages, or feed publication lag.
- `We stopped hearing from agent`, remote-provider cancellation/deprovisioning, or process-startup failure before repo code runs.
- Hosted-agent disk exhaustion, checkout/submodule transport failure, or task download/upload failure.
- `adb: device offline`, broken pipe, emulator service unavailability, or other transport failure without a product assertion.

Generic network failures inside a test that intentionally exercises networking are not automatically infrastructure; use the test semantics and tracker history.

### Timeout or crash

Strong timeout/crash signatures include:

- Timeline `issues[]` says the job “ran longer than the maximum time of N minutes”.
- A test run reports incomplete tests or “Zero tests ran”.
- Logcat contains `SIGSEGV`, `SIGABRT`, tombstone, `JNI DETECTED ERROR`, fatal runtime output, or a test start with no matching result.

A generic canceled job without timeout/crash evidence is `unknown`.

## Conflicts and retry safety

- If a stage contains both a retryable root and a likely regression/unknown root, mark the stage **mixed** and exclude it from the automatic retry plan. Azure retries failed jobs at stage granularity.
- Do not recommend retry merely because a failure is red. Require category confidence of at least `0.60` and no conflicting regression/unknown root in that stage.
- A successful retry is evidence of flakiness, but it does not erase the original occurrence; retain it for issue tracking.
- If the selected stage already advanced to another attempt, is running, or failed again after a targeted retry, do not recommend another automatic retry.

## Build 1611818 examples

| Evidence | Classification | Confidence |
|---|---|---:|
| `NU1301`, Azure Artifacts HTTP 500, request blocked for `ATCPU` in `IPAddress` namespace | `transient-infrastructure` | `0.99` |
| `We stopped hearing from agent Azure Pipelines 77` | `transient-infrastructure` | `0.99` |
| A bare `PowerShell exited with code '1'` with no task log | `unknown` until the underlying task log is read | `<0.60` |
