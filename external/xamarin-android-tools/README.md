# Xamarin.Android Tools

This directory contains shared Android SDK/JDK discovery, process, and MSBuild
task infrastructure used by [.NET for Android](https://github.com/dotnet/android).

## Build

From the repository root:

```shell
dotnet build external/xamarin-android-tools/Xamarin.Android.Tools.sln
```

The solution can also be built from this directory with `make`. Set
`CONFIGURATION=Release` for a release build or `V=1` for diagnostic MSBuild
logging.

## Tests

From the repository root:

```shell
dotnet test external/xamarin-android-tools/tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj
dotnet test external/xamarin-android-tools/tests/Microsoft.Android.Build.BaseTasks-Tests/Microsoft.Android.Build.BaseTasks-Tests.csproj
```

Build outputs are written beneath `external/xamarin-android-tools/bin/`.

## Contributing

Follow the repository's [contribution guidelines](../../CONTRIBUTING.md).
Report issues in the [dotnet/android issue tracker](https://github.com/dotnet/android/issues).
