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

## Contributing

Follow the repository's [contribution guidelines](../../CONTRIBUTING.md).
Report issues in the [dotnet/android issue tracker](https://github.com/dotnet/android/issues).
