---
name: flaky-tests
description: Investigates, fixes, disables, or counts flaky dotnet/android tests. Use when a test fails intermittently, when asked to stabilize or disable a flaky test, or when measuring CI flakiness. For classifying failures in one PR's CI run, use ci-status.
---

# Flaky tests

## Before investigating

- Search existing issues and open PRs for the test (see the `ci-status` skill's [issue search](../ci-status/references/flaky-issues.md)), and check `git log origin/main -- <test file>` for an existing fix.
- Confirm the test still fails in a current build. Don't start from an old inventory entry.
- Save logs and artifacts before rerunning or cleaning up; a rerun can destroy the evidence.
- Retry only the failed job, not the whole pipeline, for an isolated infrastructure failure.

## Classifying and counting

- A failure that passes on retry is still a flaky failure. Count per attempt (Azure DevOps job attempts), and record which builds were retried.
- Keep test bugs, product bugs, and infrastructure failures separate. An unrecognized failure is not flaky until evidence shows it.
- Don't conclude from incomplete data; report missing logs or attempts as unknown.

## Fixing

- First state what regression the test protects. The fix must keep that check: don't loosen assertions, inflate timeouts without a measured reason, or change the code path under test.
- Fix the test, not the product, unless the product has the bug. Keep unrelated fixes out of the PR.
- Replace the flaky pattern with the deterministic alternative from the [flake-prevention checklist](../code-review/references/testing-rules.md#test-reliability-flake-prevention).

## Disabling

Use the existing convention, one issue per test:

```csharp
[Ignore ("Flaky: <short reason>. See: https://github.com/dotnet/android/issues/<n>")]
```

Ask the user before creating or commenting on issues.
