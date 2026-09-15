# Managed-only activity startup

`_AndroidAllowJavaDebugging` already defaults to `False` in the MSBuild debugging
targets. This avoids launching the activity with `am start -D`, which requests
Java debugger startup. It does not by itself tell ActivityManager that a managed
debugger may deliberately pause the process during startup.

For a managed-only forced cold activity launch, the run entry points use
`am set-debug-app <package>` without `-w` or `--persistent`. Android selects
`DEBUG_ON`, rather than `DEBUG_WAIT`: it marks the process as debugging without
waiting for a Java debugger. This preserves ActivityManager's debugging state
while the managed debugger attaches. AOSP's `appNotResponding` skips a process
marked as debugging instead of treating its intentional startup pause as an ANR.

## Entry points and debug intent

- **`dotnet run`:** `src/Microsoft.Android.Run/Program.cs` protects activity launches
  only with `--attach-debugger`. `_AndroidComputeRunArguments` carries the existing
  `AndroidAttachDebugger=true` MSBuild property to this switch for activity launches.
  No new public MSBuild opt-in is required. `Debug` configuration, port mappings,
  and `WaitForExit=false` / `--no-wait` are not evidence of debug intent.
- **`-t:Run`:** `src/Xamarin.Android.Build.Debugging.Tasks/Tasks/RunActivity.cs`
  protects only `AttachDebugger && !AllowJavaDebugging`. The MSBuild debugging
  targets already default `_AndroidAllowJavaDebugging` to `False`. The task still
  uses `SetDebugPropertiesAsync` for managed setup, and its explicit Java and
  non-debug branches retain their existing launch behavior.

`ManagedActivityLaunch.cs` is an internal implementation owned by
`Microsoft.Android.Run` and source-linked, with its localized resources, into the
debugging task. It uses host transport, setup, fallback, and logging callbacks;
it has no dependency on `AndroidDevice`, `ExecutionConfiguration`, or the other
legacy tooling types. `Mono.AndroidTools` and `Xamarin.AndroidTools` retain their
pre-existing behavior and do not own this protection.

Instrumentation and `dotnet test` do not use the activity transaction, even if
the CLI receives the debug switch. Ordinary launch, wait/logcat, and Ctrl+C
behavior is unchanged. For an explicit debug activity launch, waiting for app
exit/logcat is still supported, but the initial `am start` never uses `-W`:
application startup may be waiting for the managed debugger to attach.
When exit/logcat waiting is requested for a debug launch, the host polls for its
PID for up to 30 seconds before starting logcat. This also covers unprotected
fallbacks where `am start` only schedules process creation; it does not wait for
application code. `--no-wait` does not perform this polling.
When activity metadata confirms a custom process, both PID readiness and
subsequent exit/logcat tracking use that exact process name, quoted for the
device shell. The helper returns an unconfirmed result when metadata is
unavailable; those fallbacks retain the existing package-name probe rather than
guessing a custom process or issuing another metadata query. Package identity
still controls debug-app and force-stop operations.

Debug PID reads retry normal `pidof` no-match responses (nonzero exit with empty
stdout and stderr), but report actual ADB diagnostics immediately. An offline or
unauthorized device is not reported as a PID timeout or successful application
exit. Ordinary non-debug PID handling is unchanged.

Activity launch validation reads both stdout and stderr. Successful non-waiting
`am start` status warnings on stderr are not failures, but nonzero exit codes
and error/exception records remain failures. Mutation and state-query output
checks remain strict.

The launch transaction:

- Serializes managed-only transactions for the same device serial within each
  launch host, including managed setup and cleanup. Admission has its own
  30-second timeout; its diagnostic distinguishes this from a startup timeout.
- Checks the package/component identity, Android users, and ActivityManager
  debug-app state before arming the one-shot setting.
- Awaits `am start` and then observes both the consumed global setting and the
  matching process's `mDebugging=true` record. A visible PID is not sufficient.
  It does not use `am start -W` or wait for application code to run.
- Cleans up with a separate five-second cancellation budget, including after
  launch failure, timeout, or cancellation. Cleanup errors are reported without
  replacing a primary launch error. In-flight mutations also get a separate
  five-second drain budget so cleanup cannot overtake their replies.

Startup, including setup and metadata queries, is bounded by the managed debugger
timeout (30 seconds by default). `RunActivity` keeps its completion/logging loop
alive during managed-only cancellation until the worker finishes cleanup, rather
than letting the base task return success while cleanup is still running.
`dotnet run` resolves and pins the device serial before the transaction; Ctrl+C
finishes cleanup before its existing force-stop operation.
Expiration of a private mutation, cleanup, or startup deadline is reported as a
timeout failure, not caller cancellation. The run program returns exit code 1
with a diagnostic for these failures; exit code 130 is reserved for actual Ctrl+C.

In AOSP, `attachApplicationLocked` sets the process debugging flag before restoring
the original global debug-app/wait settings, under the ActivityManager lock.
`mDebugTransient` remains true after consumption. Clearing the global setting
after observing this transition does not clear the process's debugging flag.

## Scope and limitations

Protection requires Android 12 or later, an explicit activity in the configured
package, a force-stopped, non-waiting activity command, and a single-user device
with the launch targeting that user. Both protected entry points construct
non-repeated, non-waiting activity commands. Older Android versions, warm launches,
and multi-user devices retain their existing launch behavior with a diagnostic
that they are not protected. In particular, `set-debug-app`
force-stops the package for **all users**; it must not silently replace a
user-scoped force-stop on a multi-user device.

The explicit component's package determines eligibility. Missing components retain
the host's unprotected launch with a diagnostic. Fallback launches check
cancellation both before and after executing the host's launch operation.

An unsupported initial ActivityManager process dump layout, including a vendor
postamble after the expected AOSP ending, also retains the unprotected launch
with a diagnostic and no debug-app mutation. This does not treat unknown output
as empty state: malformed or contradictory ownership records still fail.
Transport failures and cancellation are not layout fallbacks. After any marker
mutation, including during cleanup, unrecognized or truncated dumps remain errors.
No vendor-specific parsing or OEM-device validation is claimed.

Explicit Java-debugging, no-debug, broadcast, and instrumentation paths do not
use this transaction. Public Java-debugging options remain available.
Before arming, `pm resolve-activity` checks the selected user's installed activity
metadata. Android resolves application-level process inheritance and activity-level
overrides, including relative process names, into `ActivityInfo.processName`.
Custom processes and metadata that cannot be confirmed retain the unprotected
launch with an explicit diagnostic. The process name is never passed to
`set-debug-app`, because that command also force-stops a **package**.

Unrelated adb clients do not participate in the host lock. An observed different
debug target or persistent/original setting is not cleared. Android has no atomic
compare-and-clear API or ownership identifier: concurrent external changes,
including another launch of the same package, cannot be made race-free. Likewise,
a disconnected device or an unresponsive remote shell can prevent cleanup; a
host-side timeout cannot guarantee that a remote command will never execute late.
Do not run competing debug-app transactions on the same device.

## Public Android references

- [Android 16 ActivityManagerService: attach and setDebugApp](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/services/core/java/com/android/server/am/ActivityManagerService.java)
- [Android 16 ProcessRecord: process debugging state](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/services/core/java/com/android/server/am/ProcessRecord.java)
- [Android 16 ActivityThread: DEBUG_WAIT versus DEBUG_ON](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/core/java/android/app/ActivityThread.java)
- [Android 16 ProcessErrorStateRecord: debugged-process ANR exemption](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-16.0.0_r1/services/core/java/com/android/server/am/ProcessErrorStateRecord.java)
- [Android 12 ActivityManagerService](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-12.0.0_r1/services/core/java/com/android/server/am/ActivityManagerService.java)
- [Android 12 PackageManagerShellCommand: resolve-activity](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-12.0.0_r1/services/core/java/com/android/server/pm/PackageManagerShellCommand.java)
- [Android 12 ComponentInfo: effective process dump](https://android.googlesource.com/platform/frameworks/base/+/refs/tags/android-12.0.0_r1/core/java/android/content/pm/ComponentInfo.java)

## Regression coverage

`ManagedActivityLaunchTests` in `Xamarin.Android.Tools.AndroidSdk-Tests` exercises
the shared implementation compiled into the real run executable, using the
existing ADB transport against a private TCP server. Coverage includes process
metadata, ownership/layout validation, attach observation, startup and gate
timeouts, cancellation boundaries, mutation drain, and cleanup failures.

`ManagedActivityLaunchEntryPointTests` executes the real `RunActivity` task through
its existing per-build device cache, runs `Microsoft.Android.Run` with a fake ADB
process connected to the same private fixture, and evaluates the shipping
`_AndroidComputeRunArguments` target. It covers explicit debug gating, no-debug
ports/no-wait launches, instrumentation/test dispatch, shell quoting, typed task
errors, Java-debug launch routing, and cancellation/cleanup at the host boundary.
The fake-ADB process and Ctrl+C probes require bash and run on Unix; task, shared
transaction, and MSBuild argument tests are cross-platform.

The focused NUnit project builds both consumers: the run program's modern .NET
target and the task's `netstandard2.0` target. Its build graph also prepares the
version tasks required by a standalone debugging-task build; it does not require
a native Android SDK build or a device. These deterministic tests do not replace
device-side managed debugger startup, breakpoint, or OEM validation.
