# Error Patterns (dotnet/android)

Load this file only during failure categorization or when investigating a specific error.

## Categories

### 🔴 Real Failures — Investigate

These indicate genuine code problems that need fixing.

| Pattern | Example |
|---------|---------|
| MSBuild errors | `XA####`, `APT####` |
| C# compiler | `error CS####` |
| NuGet resolution | `NU1100`–`NU1699` |
| Test assertions | `Failed :`, `Assert.`, `Expected:` |
| Segfaults / crashes | `SIGSEGV`, `SIGABRT`, `Fatal error` |

### 🟡 Flaky Failures — Retry

These are known intermittent issues.

| Pattern | Example |
|---------|---------|
| Device connectivity | `device not found`, `adb: device offline` |
| Emulator timeouts | `System.TimeoutException`, `emulator did not boot` |
| Single-platform failure | Test fails on one OS but passes on others |

### 🔵 Infrastructure — Retry

These are CI environment issues, not code problems.

| Pattern | Example |
|---------|---------|
| Disk space | `No space left on device`, `NOSPC` |
| Network | `Unable to load the service index`, `Connection refused` |
| NuGet feed | `NU1301` (feed connectivity) |
| Agent issues | `The agent did not connect`, `##[error] The job was canceled` |
| Timeout (job-level) | Job canceled after 55+ minutes |

## Decision Tree

1. Does the error contain `XA`, `CS`, `NU1[1-6]`, or `Assert`? → 🔴 Real
2. Does the error mention `device`, `emulator`, `adb`, or `TimeoutException`? → 🟡 Flaky
3. Does the error mention `disk`, `network`, `feed`, `agent`, or `##[error] canceled`? → 🔵 Infra
4. Does the same test pass on other platforms in the same build? → 🟡 Flaky
5. Otherwise → 🔴 Real (default to investigating)
