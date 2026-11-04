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
runner, and discovery tools follow the shared stable framework. Modern build
tasks retain the shared product framework, `$(DotNetTargetFramework)`.
Use an MSBuild/CLI host supporting the selected framework.
They are not supported in .NET Framework MSBuild or an older `dotnet` host.
The workload retains its existing flat `tools/` and isolated `tools/net/` layout;
both contain native-runtime-compatible dependencies. Unrelated BaseTasks,
Installer.Common, Java.Interop libraries, and repository publishing tools keep
their own target frameworks. Only Java.Interop bootstrap and bytecode-test
consumers of this library require the native SDK's framework; the remaining
standalone Java.Interop tools keep their independent target framework.

`System.Collections.Immutable` and `System.Reflection.Metadata` are supplied by
the host framework, not copied into the workload's tooling directories. The
installer manifest omits those obsolete support-package files. Dependencies
still produced by unrelated owners, such as `System.Buffers` for BaseTasks and
Installer.Common, remain packaged.

## Contributing

Follow the repository's [contribution guidelines](../../CONTRIBUTING.md).
Report issues in the [dotnet/android issue tracker](https://github.com/dotnet/android/issues).
