# CI gate

`ci-gate.yml` reports a `dotnet-android-gate` commit status for every
`dotnet-android` Azure DevOps build on a PR. Unlike `dotnet-android`, it does
not turn red when the only failures come from the infrastructure. In that case
it reports **inconclusive**, retries the failed jobs and waits for the retry.

| `dotnet-android` build | Failed jobs | `dotnet-android-gate` |
|---|---|---|
| running | — | pending |
| succeeded | — | success |
| failed | any test, build, timeout or unrecognized failure | failure |
| failed | only known infrastructure failures | pending ("Inconclusive"); the failed stages are retried, up to 3 attempts per stage |

An inconclusive status never counts as success: the PR still needs a real
passing build.

## How failures are classified

`ci_gate.py` reads the build timeline and the logs of the failed tasks. A
failed job counts as infrastructure only when either:

- the agent was lost, which is reported in the job's own issues; or
- its errors match a known infrastructure signature and contain no test or
  build failure. The signatures cover DNS, connection resets and TLS errors,
  Azure DevOps 503/429 responses, PowerShell start-up crashes on the agent,
  and Gradle 5xx download errors.

Test and build evidence always wins. Anything unrecognized is treated as a
real failure. The signatures come from 30 days of `dotnet-android` failures
(see #13429).

To evaluate a build without posting a status or retrying:

```sh
python3 .github/workflows/ci-gate/ci_gate.py --build-id <buildId> --dry-run
```

## Setup (repository admins)

1. **Automatic retries:** create a Microsoft Entra app with a federated
   credential for `repo:dotnet/android` workflow runs. Grant it **Queue builds**
   on the `dotnet-android` pipeline (definition 333) in
   `dnceng-public/public`. Then set the repository variables
   `CI_GATE_AZURE_CLIENT_ID` and `CI_GATE_AZURE_TENANT_ID`. Without these, the
   gate still reports inconclusive but asks for a manual
   **Retry failed jobs**.
2. **Required check:** in the `main` ruleset, replace the required
   `dotnet-android` check with the `dotnet-android-gate` status.
