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

 4. (Optional) [Configure the build](../configuration.md).

 5. In a [Developer Command Prompt][developer-prompt], prepare the project:

        msbuild Xamarin.Android.sln /t:Prepare

    This will ensure that the build dependencies are installed, perform
    `git submodule update`, download NuGet dependencies, and other
    "preparatory" and pre-build tasks that need to be performed.

 6. Build the project:

        msbuild Xamarin.Android.sln

After the solution has built successfully, you can [use your
build][using-your-build] to build Xamarin.Android application and library
projects.  Note that by default `Xamarin.Android.sln` only builds support for
the `$(TargetFrameworkVersion)` specified in the `$(AndroidFrameworkVersion)`
property of the [`Configuration.props`][configprops-master] file (`v9.0` when
this guide was last updated), so you will need to ensure that your application
and library projects are configured to use that particular target framework
version.

[developer-prompt]: https://docs.microsoft.com/dotnet/framework/tools/developer-command-prompt-for-vs
[using-your-build]: https://github.com/xamarin/xamarin-android/blob/master/Documentation/workflow/UsingYourBuild.md
[configprops-master]: https://github.com/xamarin/xamarin-android/blob/master/Configuration.props

## Windows Build Notes

Currently Windows avoids building many of the macOS dependencies by downloading
a zip bundle of mono-related binaries previously built on macOS.  This speeds up
the build and enables development on Windows, in general.

Opening `Xamarin.Android.sln` in Visual Studio currently tends to hold file
locks on output assemblies containing MSBuild tasks.  If you are only making
changes to Xamarin.Android.Build.Tasks, one way to avoid this issue is to open
`Xamarin.Android.Build.Tasks.sln` instead.  But if you are working on changes
outside of the build tasks, then you might prefer to work in an editor like
Visual Studio Code instead and build via the command-line.

## Alternatives to Developer Command Prompt

The [Developer Command Prompt][developer-prompt] is not explicitly required,
you mostly just need a way to easily invoke `MSBuild.exe`.

So for example:

* You *could* use:
  `"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"`.
* You could add `MSBuild.exe` to your `%PATH%` via
  [Windows environment variables GUI][windows_path].
* You could setup a powershell alias to `msbuild` using [Set-Alias][set_alias].

[windows_path]: https://www.java.com/en/download/help/path.xml
[set_alias]: https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/set-alias?view=powershell-6

# Building Unit Tests

Once `msbuild Xamarin.Android.sln` has completed, the unit tests may
be built with:

	msbuild Xamarin.Android-Tests.sln /p:XAIntegratedTests=False

*NOTE*: There is currently no equivalent to [`make
jenkins`](../unix/instructions.md) on Windows.

*Troubleshooting*: Ensure you check your MSBuild version (`msbuild -version`)
and path for the proper version of MSBuild.


# Running Unit Tests

All `.apk`-based unit tests can be executed via

	msbuild Xamarin.Android.sln /t:RunApkTests

## Listing Nunit Tests

In order to get a list of the tests you can use the `ListNUnitTests` target

    msbuild Xamarin.Android.sln /t:ListNUnitTests

This will produce a list of the tests in all of the test assemblies.

## Running Specific Nunit Tests

You can run then a single (or a group) of tests using the `$(TEST)` msbuild property.

    msbuild Xamarin.Android.sln /t:RunNunitTests /p:TEST=Xamarin.Android.Build.Tests.Aapt2Tests.Aapt2Compile

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
