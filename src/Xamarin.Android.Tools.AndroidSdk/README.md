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

The SDK tooling requires .NET 11, including its build/MSBuild host and consumers.
There is no older-runtime process implementation.

From the repository root, using a .NET 11 SDK with the native process APIs:

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
`Process.Run[Async]` without capture, `Process.RunAndCaptureText[Async]` for complete
text, and `Process.ReadAllLines[Async]` for streaming. This library provides no
generic process facade or older-runtime implementation.

ADB keeps environment overrides, structured arguments, diagnostics, and its
per-device protocols at `AdbRunner`. Root exit is distinct from output EOF: the
ADB owner bounds post-exit capture to 30 seconds and terminates only its owned
client on cancellation, never a shared server or persistent emulator.
`EmulatorRunner.LaunchEmulator` still returns a caller-owned persistent process;
the caller retains responsibility for terminating it.

JDK discovery uses bounded native capture. SDK-manager and AVD creation own
interactive stdin, start native capture before feeding input, and observe their
input/exit tasks. Native line enumeration serializes instrumentation parsing and
LLVM symbol filtering without an event pump or text-writer adapter.

Native text capture preserves CR/LF and trailing text but decodes with the selected
encoding, rather than guessing a different encoding from a BOM. Applications
that actually require BOM detection should use native byte capture plus BCL
`StreamReader`; do not recreate a decoding or compatibility framework.

The affected SDK, build/debugging/installer task assemblies, instrumentation
runner, and discovery tools require a **.NET 11 or later MSBuild/CLI host**.
They are not supported in .NET Framework MSBuild or an older `dotnet` host.
The workload retains its existing flat `tools/` and isolated `tools/net/` layout;
both contain native-runtime-compatible dependencies. Unrelated BaseTasks,
Installer.Common, Java.Interop libraries, and repository publishing tools keep
their own target frameworks. The selected Java.Interop bootstrap/test consumers
use truthful .NET 11 paths, not .NET 10 aliases.

These owners explicitly select the .NET 11 runtime and targeting pack
`11.0.0-rtm.26479.103`. The repository's pinned .NET 12 preview SDK otherwise
maps even `net11.0` to its own .NET 12 runtime and reference pack. A target
framework label alone is not a runtime floor: shipped tooling must also load
on the declared .NET 11 host. The settings and restore feed are owner-scoped;
global SDK pins and unrelated frameworks are unchanged.

SDK fixture CI provisions the repository-pinned SDK with the existing
`eng/install-dotnet` scripts and explicitly selects that executable for VSTest.
A wildcard .NET 11 preview installation may be older than the required runtime.
Benchmark and BaseTasks jobs keep their own host selection.

## Contributing

Follow the repository's [contribution guidelines](../../CONTRIBUTING.md).
Report issues in the [dotnet/android issue tracker](https://github.com/dotnet/android/issues).
