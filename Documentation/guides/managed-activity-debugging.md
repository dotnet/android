# Managed-only activity startup

Avoiding `am start -D` prevents Java-debugger waiting, but does not protect an
intentional managed-debugger pause from Android's startup ANR detection.
Eligible launches therefore use transient `am set-debug-app <package>`, without
`-w` or `--persistent`. This selects Android's `DEBUG_ON` rather than `DEBUG_WAIT`.

## Entry points and debug intent

| Entry point | Protection gate |
| --- | --- |
| `Microsoft.Android.Run/Program.cs` (`dotnet run`) | `--attach-debugger`, forwarded from existing `AndroidAttachDebugger=true` for activity launches |
| `RunActivity.cs` (`-t:Run`) | `AttachDebugger && !AllowJavaDebugging` |

Debug configuration, port forwarding, and `--no-wait` alone do not select this
behavior. Instrumentation does not use it. The task's explicit Java-debugging and
non-debug branches retain their existing behavior. The MSBuild debugging targets
already default `_AndroidAllowJavaDebugging` to `False`.

The internal `ManagedActivityLaunch` helper and its resources belong to
`Microsoft.Android.Run` and are source-linked into the task. Host callbacks
provide transport, debugger setup, fallback, and logging; the helper does not
depend on the retiring `Mono.AndroidTools` or `Xamarin.AndroidTools` libraries.
It protects startup, not the debugger connection itself: callers still supply
the runtime's debugger configuration.

## Transaction and safety boundaries

- Serialize the device's debug-app transaction within each launch host. Validate
  package/component identity, users, process metadata, and existing debug-app
  ownership before mutation.
- Arm the transient setting, launch without `-D` or `-W`, and observe both global
  state consumption and the matching process's `mDebugging=true`. A visible PID
  alone is too early; waiting for application code could deadlock debugger attach.
- Drain in-flight mutations before cleanup can overtake them. Mutation drain and
  cleanup each have independent five-second budgets; startup uses the debugger
  timeout and lock admission has a separate 30-second limit.
- Preserve the primary error while reporting cleanup failures. Task cancellation
  keeps the completion/logging pump alive until cleanup finishes. CLI timeouts
  return failure, not the exit code reserved for actual Ctrl+C.

Protection is limited to Android 12+, forced cold activity launches, a single-user
device, and a confirmed default package process. `set-debug-app` force-stops the
package for **all users**, so it must not broaden a user-scoped launch. Warm,
multi-user, custom-process, or otherwise ineligible launches retain an explicitly
diagnosed unprotected path. Unknown initial dump layouts can fall back before
mutation; malformed ownership, transport failures, and post-mutation state errors
do not become success-shaped fallbacks.

CLI exit/logcat tracking uses a confirmed custom process when available, otherwise
the existing package probe. Debug launches poll for initial PID readiness rather
than using `am start -W`; ordinary launches are unchanged. ADB errors are distinct
from normal PID absence, and successful `am start` warnings on stderr are not
treated as failures.

The host lock cannot coordinate unrelated adb clients. There is no atomic
compare-and-clear API, so external ownership changes, disconnection, and late
remote execution remain limitations. An observed foreign or persistent debug-app
assignment is never cleared. Do not run competing transactions on the same device.

## Regression coverage and limits

Tests cover the shared transaction's ownership, eligibility, consumed-state,
cancellation, timeout, and cleanup invariants, plus the real launch entry points'
debug-intent gates, command/exit behavior, task diagnostics, and MSBuild arguments.
Fixtures must not touch a real adb server or device. Host-side tests do not replace
live managed-debugger startup, sustained breakpoint pause, or OEM-layout validation.

| Coverage | Owning test project |
| --- | --- |
| Shared helper and portable CLI process bridge | [Microsoft.Android.Run-Tests](../../tests/Microsoft.Android.Run-Tests/Microsoft.Android.Run-Tests.csproj) |
| `RunActivity` and shipping MSBuild argument routing | [Microsoft.Android.Build.Tasks.Tests](../../src/Microsoft.Android.Build.Tasks/Tests/Microsoft.Android.Build.Tasks.Tests/Microsoft.Android.Build.Tasks.Tests.csproj) |

The task-test project requires its default .NET 11 target; do not retarget it to
.NET 10. The real SIGINT assertion is Unix-only; the other CLI cases use a portable
fake-ADB executable rather than a Bash-only bridge.

## Android references

- [ActivityManagerService: transient debug-app state and application attach](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/services/core/java/com/android/server/am/ActivityManagerService.java)
- [ActivityThread: DEBUG_WAIT versus DEBUG_ON](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/core/java/android/app/ActivityThread.java)
- [ProcessErrorStateRecord: debugged-process ANR exemption](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/services/core/java/com/android/server/am/ProcessErrorStateRecord.java)
