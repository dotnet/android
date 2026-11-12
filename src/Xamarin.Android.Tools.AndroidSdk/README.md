# Shared Android tooling

This project contains Android SDK/JDK discovery, process, and runner utilities
used by [.NET for Android](https://github.com/dotnet/android). Shared MSBuild
task infrastructure is in
[`src/Microsoft.Android.Build.BaseTasks`](../Microsoft.Android.Build.BaseTasks).

`Mono.AndroidTools.dll` and `Xamarin.AndroidTools.dll` are no longer built or
included in the workload SDK pack. This is a breaking change for tools that
reference those assemblies directly. Use this library's `AndroidSdkInfo` and
`AdbRunner` APIs instead; installers can use
`AndroidSdkInfo.DiscoverInstallationPaths` before an SDK or JDK is installed.

## Build

The SDK tooling uses the repository's shared stable target framework,
`$(DotNetStableTargetFramework)` (currently .NET 11). Its build/MSBuild host and
consumers must support that framework. There is no older-runtime process implementation.

From the repository root, using the repository-pinned SDK:

```shell
dotnet build src/Microsoft.Android.Build.BaseTasks/Microsoft.Android.Build.BaseTasks.csproj
dotnet build src/Xamarin.Android.Tools.AndroidSdk/Xamarin.Android.Tools.AndroidSdk.csproj
```

## Tests

```shell
dotnet test tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj
dotnet test tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj
```

## Process execution and hosting

Process execution belongs to the tool that owns the command. Use .NET 11's
`Process.Run[Async]` without capture, instance `Process.ReadAllText[Async]` for
complete text, and `Process.ReadAllLines[Async]` for streaming. This library provides no
generic process facade or older-runtime implementation.

Start native capture before accessing the redirected readers, then explicitly own
and dispose both readers after capture has joined. The pinned runtime's static
complete-capture helpers leave those readers to finalization: repeated successful
captures otherwise retain two pipe descriptors per command until GC. Disposing
the `Process` alone does not close readers in the native capture's synchronous mode.

ADB keeps environment overrides, structured arguments, diagnostics, and its
per-device protocols at `AdbRunner`. Root exit is distinct from output EOF:
`AdbRunner`, FastDeploy and the instrumentation CLI's `AdbHelper` bound
post-exit capture to 30 seconds and terminate only their owned
client on cancellation, never a shared server or persistent emulator.
Cleanup still observes the owned client's exit when output capture fails, with
a five-second termination bound; successful cleanup preserves the original
capture exception.
Finite `EmulatorRunner.ListAvdNamesAsync` discovery has a 30-second total budget
for execution and complete output capture, including default-token callers.
Expiration throws `TimeoutException`; caller cancellation retains the caller's
token. Cleanup stops only the discovery command, observes its exit for up to five
seconds, and does not terminate descendants merely to close inherited pipes.
`EmulatorRunner.LaunchEmulator` still returns a caller-owned persistent process;
the caller retains responsibility for terminating it.

JDK discovery uses bounded native capture. SDK-manager and AVD creation own
interactive stdin, start native capture before feeding input, and observe their
input/exit tasks. SDK-manager closing stdin ends answer feeding, not execution
or output capture; the command's exit status still determines success.
License discovery parses completed output even when declining returns a nonzero
exit code, but propagates startup, capture and drain-timeout failures instead of
reporting an empty license list. Accepting specific license IDs propagates the
same discovery failures.
AVD list/delete/device-profile discovery and SDK-manager package queries have a
30-second execution budget. AVD creation, SDK/JDK installation, license input and
tar extraction retain caller-controlled active durations. Every such command
separately bounds post-root output draining to 30 seconds; cancellation stops and
joins the owned root and readers, not shared ADB servers or installer services.
Unexpected input or capture failures cannot bypass the other owned task joins.
These budgets govern owned processes and reads, not arbitrary caller-supplied
logging callbacks; callbacks must return cooperatively.
Native line enumeration serializes instrumentation parsing and LLVM symbol
filtering without an event pump or text-writer adapter.

Instrumentation publication failures stop the invocation and propagate rather
than being treated as malformed status blocks. Owned client/reader cleanup runs
before joining the publication consumer. MTP's `IMessageBus.PublishAsync` has no
cancellation argument: an in-progress arbitrary sink must cooperate to finish;
the five-second exit-cleanup budget is not a hard publication deadline. Already
observed complete status blocks remain available for partial-run diagnostics.

FastDeploy distinguishes authored remote shell scripts from remote argument
lists, quoting dynamic operands before ADB's device-side re-parsing. It rejects
failed force-stop and install results before claiming success. Its initial
device query participates in task cancellation. Deferred app-detection queries
have a 30-second execution budget and a disposable build owner; waiter/build-end
cancellation stops and joins the original query and preserves the upload flag
when discovery is incomplete.

Native text capture preserves CR/LF and trailing text but decodes with the selected
encoding, rather than guessing a different encoding from a BOM. Applications
that actually require BOM detection should use native byte capture plus BCL
`StreamReader`; do not recreate a decoding or compatibility framework.

The affected SDK, build/debugging/installer task assemblies, instrumentation
runner, and discovery tools follow the shared stable framework. Modern build
tasks retain the shared product framework, `$(DotNetTargetFramework)`.
Use an MSBuild/CLI host supporting the selected framework.
They are not supported in .NET Framework MSBuild or an older `dotnet` host.
The workload retains its existing flat `tools/` and isolated `tools/net/` layout;
both contain native-runtime-compatible dependencies. Unrelated BaseTasks,
Installer.Common and repository publishing tools keep their own target
frameworks. Java.Interop libraries, tooling, and tests now use the repository's
standard `src/`, `tests/`, and `build-tools/` layout and shared framework
properties; the retired standalone bootstrap tasks and host overrides are not
required.

`System.Collections.Immutable` and `System.Reflection.Metadata` are supplied by
the host framework, not copied into the workload's tooling directories. The
installer manifest omits those obsolete support-package files. Dependencies
still produced by unrelated owners, such as `System.Buffers` for BaseTasks and
Installer.Common, remain packaged.

## Contributing

Follow the repository's [contribution guidelines](../../CONTRIBUTING.md).
Report issues in the [dotnet/android issue tracker](https://github.com/dotnet/android/issues).
