# android-tools

**android-tools** is a repo to easily share code between the
[xamarin-android][android] repo and the .NET for Android commercial tooling,
such as IDE extensions, without requiring that the IDE extensions
submodule the entire **android** repo, which is gigantic.

[android]: https://github.com/xamarin/xamarin-android

# Build Status

| Platform              | Status |
|-----------------------|--------|
| **macOS**             | [![macOS Build Status][azure-macOS-icon]][azure-macOS-status] |
| **Windows**           | [![Windows Build Status][azure-Windows-icon]][azure-Windows-status] |


[azure-macOS-icon]: https://dev.azure.com/xamarin/public/_apis/build/status/xamarin-android-tools
[azure-macOS-status]: https://dev.azure.com/xamarin/public/_build/latest?definitionId=3
[azure-Windows-icon]: https://dev.azure.com/xamarin/public/_apis/build/status/xamarin-android-tools
[azure-Windows-status]: https://dev.azure.com/xamarin/public/_build/latest?definitionId=3


# Build Requirements

**-android-tools** requires .NET 6 or later.

# Build Configuration

The default `make all` target accepts the following optional
**make**(1) variables:

  * `$(CONFIGURATION)`: The configuration to build.
    Possible values include `Debug` and `Release`.
    The default value is `Debug`.
  * `$(V)`: Controls build verbosity. When set to a non-zero value,
    The build is built with `/v:diag` logging.

# Build

To build **android-tools**:

	dotnet build Xamarin.Android.Tools.sln

Alternatively run `make`:

	make

# Tests

To run the unit tests:

	dotnet test tests/Xamarin.Android.Tools.AndroidSdk-Tests/Xamarin.Android.Tools.AndroidSdk-Tests.csproj -l "console;verbosity=detailed"

# Build Output Directory Structure

There are two configurations, `Debug` and `Release`, controlled by the
`$(Configuration)` MSBuild property or the `$(CONFIGURATION)` make variable.

The `bin\$(Configuration)` directory, e.g. `bin\Debug`, contains
*redistributable* artifacts. The `bin\Test$(Configuration)` directory,
e.g. `bin\TestDebug`, contains unit tests and related files.

* `bin\$(Configuration)`: redistributable build artifacts.
* `bin\Test$(Configuration)`: Unit tests and related files.

# Distribution

Package versioning follows [Semantic Versioning 2.0.0](https://semver.org/).
The major version in the `nuget.version` file should be updated when a breaking change is introduced.
The minor version should be updated when new functionality is added.
The patch version will be automatically determined by the number of commits since the last version change.

Xamarin.Android.Tools.AndroidSdk nupkg files are produced for every build which occurrs on [Azure Devops](https://dev.azure.com/xamarin/Xamarin/_build?definitionId=2&_a=summary).
To download one of these packages, navigate to the build you are interested in and click on the `Artifacts` button.

Alternatively, "unofficial" releases are currently hosted on the [Xamarin.Android](https://dev.azure.com/xamarin/public/_packaging?_a=feed&feed=Xamarin.Android) feed.
Add the feed to your project's `NuGet.config` to reference these packages:

```xml
<configuration>
  <packageSources>
    <add key="Xamarin.Android" value="https://pkgs.dev.azure.com/xamarin/public/_packaging/Xamarin.Android/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

An Azure Pipelines [Release ](https://dev.azure.com/xamarin/public/_release?view=mine&_a=releases&definitionId=12) can be manually triggered to push a new version to this feed.

# Mailing Lists

To discuss this project, and participate in the design, we use the
[android-devel@lists.xamarin.com](http://lists.xamarin.com/mailman/listinfo/android-devel) mailing list.

# Coding Guidelines

We use [Mono's Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

# Reporting Bugs

We use [GitHub](https://github.com/dotnet/android-tools/issues) to track issues.
