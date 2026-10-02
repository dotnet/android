# Shared Android tooling

This project contains Android SDK/JDK discovery, process, and runner utilities
used by [.NET for Android](https://github.com/dotnet/android). Shared MSBuild
task infrastructure is in
[`src/Microsoft.Android.Build.BaseTasks`](../Microsoft.Android.Build.BaseTasks).

`Mono.AndroidTools.dll` and `Xamarin.AndroidTools.dll` are no longer built or
included in the workload SDK pack. This is a breaking change for tools that
reference those assemblies directly. Use this library's `AndroidSdkInfo`,
`AdbRunner`, and `ProcessUtils` APIs instead; installers can use
`AndroidSdkInfo.DiscoverInstallationPaths` before an SDK or JDK is installed.

## Build

From the repository root:

```shell
dotnet build src/Microsoft.Android.Build.BaseTasks/Microsoft.Android.Build.BaseTasks.csproj
dotnet build src/Xamarin.Android.Tools.AndroidSdk/Xamarin.Android.Tools.AndroidSdk.csproj
```

## Tests

```shell
dotnet test tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj
dotnet test tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj -p:AndroidToolsDisableMultiTargeting=false -p:DotNetTargetFrameworkVersion=10.0
```

## Process execution

`ProcessUtils.StartProcess` drains stdout and stderr concurrently, preserving
encoding/BOM detection, CR/LF characters, empty lines, and trailing partial text.
It returns the root process's exit code, including nonzero codes. Supplied
`TextWriter` instances remain caller-owned; using the same writer for both streams
serializes writes. Explicit redirection requires a corresponding writer.

The runner owns the process and its redirected readers. `onStarted` receives a
borrowed process and may write to redirected stdin; close stdin in the callback
when the child requires EOF. Output drains start before the callback, so a child
producing output while consuming input does not deadlock on full pipes. Do not
dispose the borrowed process or read its output concurrently with the runner.

Execution has no implicit deadline: supply a cancellation token, optionally
from `CancellationTokenSource.CancelAfter`, to bound a running command. Cancellation
or a writer/callback failure stops reading and terminates **only the owned root**.
Descendants are not tree-killed: an ADB server or persistent emulator may belong
to other callers. Root exit is distinct from pipe EOF; after root exit, redirected
output has 30 seconds to close, otherwise the task faults with `TimeoutException`
rather than returning incomplete output as success. Shutdown waits at most five
seconds for the root, callback, and output consumers, and reports cleanup failures
explicitly.

Owned readers/pipes are closed on completion, cancellation, or failure. On older
stream implementations that cannot interrupt an in-flight read, the runner stops
forwarding data and observes any eventual read failure. A descendant that retains
these pipes may encounter a closed pipe if it subsequently writes; callers needing
a persistent descendant should not give it the command's captured output handles.
Synchronous callbacks and writer methods must return promptly: managed code cannot
preempt an arbitrary blocked consumer.

The internal `Exec` helper keeps its `DataReceivedEventHandler` contract, including
line splitting, EOF notifications, and optional stderr delivery. It serializes
callbacks across the two streams and propagates callback failures instead of
letting them escape an event thread. It uses the same post-exit drain bound.
`ExecuteToolAsync` requires a result parser and propagates parser exceptions through
its returned task; nonzero exits still prefer stderr, falling back to stdout. A
consumer failure takes precedence over concurrent execution cancellation; multiple
consumer or cleanup failures are reported together as `AggregateException`.

## Contributing

Follow the repository's [contribution guidelines](../../CONTRIBUTING.md).
Report issues in the [dotnet/android issue tracker](https://github.com/dotnet/android/issues).
