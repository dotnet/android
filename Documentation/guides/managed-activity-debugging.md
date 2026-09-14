# Managed-only activity startup

`_AndroidAllowJavaDebugging` already defaults to `False` in the MSBuild debugging
targets. This avoids launching the activity with `am start -D`, which requests
Java debugger startup. It does not by itself tell ActivityManager that a managed
debugger may deliberately pause the process during startup.

For a managed-only forced cold activity launch, `StartWithDebuggingAsync` uses
`am set-debug-app <package>` without `-w` or `--persistent`. Android selects
`DEBUG_ON`, rather than `DEBUG_WAIT`: it marks the process as debugging without
waiting for a Java debugger. This preserves ActivityManager's debugging state
while the managed debugger attaches. AOSP's `appNotResponding` skips a process
marked as debugging instead of treating its intentional startup pause as an ANR.

The launch transaction:

- Serializes activity debug launches to the same device serial within the host
  process, including cleanup.
- Checks the package/component identity, Android users, and ActivityManager
  debug-app state before arming the one-shot setting.
- Awaits `am start` and then observes both the consumed global setting and the
  matching process's `mDebugging=true` record. A visible PID is not sufficient.
  It does not use `am start -W` or wait for application code to run.
- Cleans up with a separate five-second cancellation budget, including after
  launch failure, timeout, or cancellation. Cleanup errors are reported without
  replacing a primary launch error.

In AOSP, `attachApplicationLocked` sets the process debugging flag before restoring
the original global debug-app/wait settings, under the ActivityManager lock.
`mDebugTransient` remains true after consumption. Clearing the global setting
after observing this transition does not clear the process's debugging flag.

## Scope and limitations

Protection requires Android 12 or later, an explicit activity in the configured
package, `ForceStop=true`, no wait/repeat option, and a single-user device with
the launch targeting that user. Older Android versions, warm launches, repeated
or waiting launches, and multi-user devices retain their existing launch behavior
with a diagnostic that they are not protected. In particular, `set-debug-app`
force-stops the package for **all users**; it must not silently replace a
user-scoped force-stop on a multi-user device.

The explicit component's package determines eligibility; the command's optional,
non-emitted `PackageName` bookkeeping is not required. Implicit intents retain
their existing unprotected launch with a diagnostic. Fallback launches check
cancellation both before and after executing the legacy intent command.

An unsupported initial ActivityManager process dump layout, including a vendor
postamble after the expected AOSP ending, also retains the unprotected launch
with a diagnostic and no debug-app mutation. This does not treat unknown output
as empty state: recognized layouts with malformed ownership records still fail.
Transport failures and cancellation are not layout fallbacks. After any marker
mutation, including during cleanup, unrecognized or truncated dumps remain errors.
No vendor-specific parsing or OEM-device validation is claimed.

Explicit Java-debugging, no-debug, null-command, broadcast, and instrumentation
paths do not use this transaction. Public Java-debugging options remain available.
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

`ManagedActivityLaunchTests` in `Xamarin.Android.Tools.AndroidSdk-Tests` exercises
the real shared launch implementation and ADB transport against a private TCP
server. These deterministic tests do not replace device-side managed debugger
startup and breakpoint validation.
