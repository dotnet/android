# Building .NET for Android on Windows

Building .NET for Android on Windows requires .NET and the `dotnet` command
be available within the Command-Line environment.
(The **Developer Command Prompt** that Visual Studio installs is sufficient.)

.NET 9 SDK or later is required at the time of writing.

## Building .NET for Android

 1. Install the [build dependencies](dependencies.md).

 2. Clone the xamarin-android repo:

        git clone https://github.com/dotnet/android.git

 3. Navigate to the `android` directory

 4. (Optional) [Configure the build](../configuration.md).

 5. In a [Developer Command Prompt][developer-prompt], prepare the project:

        dotnet msbuild Xamarin.Android.sln -t:Prepare

    This will ensure that the build dependencies are installed, perform
    `git submodule update`, download NuGet dependencies, and other
    "preparatory" and pre-build tasks that need to be performed.

 6. Build the project:

        dotnet-local.cmd build Xamarin.Android.sln

 7. (For Microsoft team members only - Optional) In a [Developer Command
    Prompt][developer-prompt], build external proprietary git
    dependencies:

        dotnet-local.cmd build Xamarin.Android.sln -t:BuildExternal

    This will clone and build external proprietary components such as
    the `android-platform-support` repository in Azure DevOps.

 8. Configure local `android` workload:

        dotnet-local.cmd build build-tools/create-packs/Microsoft.Android.Sdk.proj -t:ConfigureLocalWorkload

After the solution has built successfully, you can use `dotnet-local.cmd` to create and build Android projects.

Once an initial build succeeds, for incremental builds, you can simply do:

    dotnet-local.cmd build Xamarin.Android.sln

[developer-prompt]: https://docs.microsoft.com/dotnet/framework/tools/developer-command-prompt-for-vs

## Windows Build Notes

Opening `Xamarin.Android.sln` in Visual Studio currently tends to hold file
locks on output assemblies containing MSBuild tasks.  If you are only making
changes to Xamarin.Android.Build.Tasks, one way to avoid this issue is to open
`Xamarin.Android.Build.Tasks.sln` instead.  But if you are working on changes
outside of the build tasks, then you might prefer to work in an editor like
Visual Studio Code instead and build via the command-line.

@jonathanpeppers gave a talk at [Xamarin Developer Summit
2019][xamdevsummit] with a full walkthrough:

[![Build Xamarin.Android](https://img.youtube.com/vi/8qaQleb6Tbk/maxresdefault.jpg)][xamdevsummit]

[xamdevsummit]: https://youtu.be/8qaQleb6Tbk

## Creating a local .NET for Android Workload

`dotnet msbuild Xamarin.Android.sln -t:Prepare` provisions a
specific build of .NET to `bin\$(Configuration)\dotnet`.

Once the prepare target is complete, you can set up a local
.NET for Android workload install with:

    dotnet-local.cmd build Xamarin.Android.sln -t:BuildDotNet

Your local `bin\$(Configuration)\lib\packs` directory will be
populated with a local Android "workload" in
`Microsoft.Android.Sdk.$(HostOS)` matching your operating system.

Create a new project with `dotnet-local.cmd new android`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0-android</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Build the project in `cmd` with:

    > dotnet-local.cmd build foo.csproj

Or in powershell:

    > dotnet-local.cmd build foo.csproj

Using the `dotnet-local` script will execute the `dotnet` provisioned in
`bin\$(Configuration)\dotnet` and will use the locally built binaries.

See the [One .NET Documentation](../../guides/OneDotNet.md) for further details.

## Creating installers

Once `dotnet msbuild Xamarin.Android.sln -t:Prepare` is complete,
.NET for Android workload packs can be built with:

    dotnet-local.cmd build Xamarin.Android.sln -t:BuildDotNet,PackDotNet

Several `.nupkg` files will be output in `.\bin\Build$(Configuration)\nuget-unsigned`.

Commercial packages will be created by this command if the
`dotnet-local.cmd build Xamarin.Android.sln -t:BuildExternal`
command was ran before building.

## Building Unit Tests

Once `dotnet-local.cmd build Xamarin.Android.sln` has completed, the unit tests may
be built with e.g.:

    dotnet-local.cmd build Xamarin.Android-Tests.sln /restore /p:Configuration=Debug /bl:bin\TestDebug\msbuild-build-tests.binlog

Note that the `Debug` in `bin\Debug` must match the Configuration
which was built.  If xamarin-android was built with `-c Release`, then
this should be `bin\Release`, not `bin\Debug`.

*NOTE*: There is currently no equivalent to [`make
jenkins`](../unix/instructions.md) on Windows.

*Troubleshooting*: Ensure you check your .NET version (`dotnet --version`)
and path for the proper version of `dotnet`.

## Running Unit Tests

All `.apk`-based unit tests can be executed via

    dotnet-local.cmd build Xamarin.Android.sln /t:RunApkTests

### Listing Nunit Tests

In order to get a list of the tests you can use the `ListNUnitTests` target

    dotnet-local.cmd build Xamarin.Android.sln /t:ListNUnitTests

This will produce a list of the tests in all of the test assemblies.

### Running Specific Nunit Tests

You can run then a single (or a group) of tests using the `$(TEST)` MSBuild property.

    dotnet-local.cmd build Xamarin.Android.sln /t:RunNunitTests /p:TEST=Xamarin.Android.Build.Tests.Aapt2Tests.Aapt2Compile

### Running Individual `.apk` Projects

See also the [`tests/RunApkTests.targets`](../../tests/RunApkTests.targets) and
[`build-tools/scripts/TestApks.targets`](../../build-tools/scripts/TestApks.targets)
files.

All `.apk`-based unit test projects provide the following targets:

* `DeployTestApks`: Installs the associated `.apk` to an Android device.

* `UndeployTestApks`: Uninstalls the associated `.apk` from an Android device.

* `RunTestApks`: Executes the unit tests contained within a `.apk`.
  This target must be executed *after* the `DeployTestApks` target.

To run an individual `.apk`-based test project, a package must be built, using the
`SignAndroidPackage` target, installed, and executed.

### Running `.apk` Projects with Include/Exclude

If an `.apk`-based unit test uses the NUnit `[Category]` custom attribute, then
those tests can be explicitly included or excluded from execution by setting
the `$(IncludeCategories)` or `$(ExcludeCategories)` MSBuild properties.

For example, to exclude tests that use the internet (`InetAccess`) category:

    dotnet-local.cmd build Xamarin.Android.sln /t:RunApkTests /p:ExcludeCategories=InetAccess

`$(IncludeCategories)` functions in the same fashion.

To specify multiple categories, separate each category with a `:` character.
