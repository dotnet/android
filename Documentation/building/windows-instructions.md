# Building Xamarin.Android on Windows

Building Xamarin.Android on Windows requires .NET and the `msbuild` command
be available within the Command-Line environment.
(The **Developer Command Prompt** that Visual Studio installs is sufficient.)

MSBuild version 15 or later is required.

# Building Xamarin.Android

 1. Install the [build dependencies](dependencies.md).

 2. Clone the xamarin-android repo:

        git clone https://github.com/xamarin/xamarin-android.git

 3. Navigate to the `xamarin-android` directory

 4. (Optional) [Configure the build](configuration.md).

 5. Prepare the project:

        msbuild Xamarin.Android.sln /t:Prepare

    This will ensure that the build dependencies are installed, perform
    `git submodule update`, download NuGet dependencies, and other
    "preparatory" and pre-build tasks that need to be performed.

 6. Build the project:

        msbuild Xamarin.Android.sln


## Windows Build Notes

Currently Windows avoids building many of the macOS dependencies by downloading
a zip bundle of mono-related binaries previously built on macOS. This speeds up
the build and enables development on Windows, in general.

A simple way to ensure you have the needed dependencies on Windows is to install
Visual Studio 2017 (> 15.3.x) along with the Xamarin workload. This will ensure you have
the correct version of Xamarin.Android, the Android SDK, and Java needed.

It also is worth noting that opening `Xamarin.Android.sln` in Visual Studio tends
to hold file locks on output assemblies containing MSBuild tasks. Until there is a solution
for this, it might be more advisable to use an editor like Visual Studio Code and build via
the command-line.


# Building Unit Tests

Once `msbuild Xamarin.Android.sln` has completed, the unit tests may
be built with:

	msbuild Xamarin.Android-Tests.sln /p:XAIntegratedTests=False

*NOTE*: There is currently no equivalent to [`make jenkins`](unix-instructions.md) on Windows.

*Troubleshooting*: Ensure you check your MSBuild version (`msbuild -version`)
and path for the proper version of MSBuild.


# Running Unit Tests

All `.apk`-based unit tests can be executed via

	msbuild Xamarin.Android.sln /t:RunApkTests

## Running Individual `.apk` Projects

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

For example:

	$ bin/Debug/bin/xabuild /t:SignAndroidPackage  tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj
	$ bin/Debug/bin/xabuild /t:DeployTestApks      tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj
	$ bin/Debug/bin/xabuild /t:RunTestApks         tests/locales/Xamarin.Android.Locale-Tests/Xamarin.Android.Locale-Tests.csproj


## Running `.apk` Projects with Include/Exclude

If an `.apk`-based unit test uses the NUnit `[Category]` custom attribute, then
those tests can be explicitly included or excluded from execution by setting
the `$(IncludeCategories)` or `$(ExcludeCategories)` MSBuild properties.

For example, to exclude tests that use the internet (`InetAccess`) category:

	msbuild Xamarin.Android.sln /t:RunApkTests /p:ExcludeCategories=InetAccess

`$(IncludeCategories)` functions in the same fashion.

To specify multiple categories, separate each category with a `:` character.
