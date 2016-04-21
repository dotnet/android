Xamarin.Android
===============

Xamarin.Android provides open-source bindings of the Android SDK for use with
.NET managed languages such as C#.

# Build Configuration

Building Xamarin.Android requires the Java Development Kit (JDK), several
pieces of the Android SDK, and the Android NDK.

The Java Development Kit may be downloaded from the
[Oracle Java SE Downloads page][download-jdk].

[download-jdk]: http://www.oracle.com/technetwork/java/javase/downloads/

To simplify building Xamarin.Android, important pieces of the Android SDK
and Android NDK will be automatically downloaded and installed from
Google's website. Downloaded files are cached locally, by default into
`$(HOME)\android-archives`. The Android NDK and SDK will be installed by
default into `$(HOME)\android-toolchain`.

These directories may be changed by creating the file
`Configuration.Override.props` in the toplevel directory and editing
the MSBuild properties:

* `$(AndroidToolchainCacheDirectory)`: The directory to cache the downloaded
    Android NDK and SDK files.
* `$(AndroidToolchainDirectory)`: The directory to install the downloaded
    Android NDK and SDK files.

The file [Configuration.Override.props.in][Configuration.Override.props.in]
may be used as a template file for creating `Configuration.Override.props`.

The files that will be downloaded and installed are controlled by
[build-tools/android-toolchain/android-toolchain.projitems][android-toolchain.projitems]
via the `@(AndroidNdkItem)` and `@(AndroidSdkItem)` item groups, and the
URL to download files from is controlled by the `$(AndroidUri)` property.

[android-toolchain.projitems]: build-tools/android-toolchain/android-toolchain.projitems

# Build

At this point in time, building Xamarin.Android is only supported on OS X.
We will work to improve this.

To build Xamarin.Android, first prepare the project:

    make prepare

This will perform `git submodule update`, and any other pre-build tasks
that need to be performed.

Then, you may do one of the following:

1. Run make:

        make

2. Load `Xamarin.Android.sln` into Xamarin Studio and Build the project.

    *Note*: The `Mono.Android` project may *fail* on the first build
    because it generates sources, and those sources won't exist on the
    initial project load. Rebuild the project should this happen.

# Build Output Directory Structure

There are two configurations, `Debug` and `Release`, controlled by the
`$(Configuration)` MSBuild property.

The `bin\Build$(Configuration)` directory, e.g. `bin\BuildDebug`, contains
artifacts needed for *building* the repository. They should not be needed
for later execution.

The `bin\$(Configuration)` directory, e.g. `bin\Debug`, contains
*redistributable* artifacts, such as tooling and runtimes. This directory
acts as a *local installation prefix*, in which the directory structure
mirrors that of the OS X Xamarin.Android.framework directory structure:

* `bin\$(Configuration)\lib\xbuild\Xamarin\Android`: MSBuild-related support
    files and required runtimes used by the MSBuild tooling.
* `bin\$(Configuration)\lib\xbuild-frameworks\MonoAndroid`: Xamarin.Android
    profiles.
* `bin\$(Configuration)\lib\xbuild-frameworks\MonoAndroid\v1.0`: Xamarin.Android
    Base Class Library assemblies such as `mscorlib.dll`.
* `bin\$(Configuration)\lib\xbuild-frameworks\MonoAndroid\*`: Contains
    `Mono.Android.dll` for a given Xamarin.Android `$(TargetFrameworkVersion)`.

# Xamarin.Android `$(TargetFrameworkVersion)`s

Xamarin.Android uses the MSBuild `$(TargetFrameworkVersion)` mechanism
to provide a separate `Mono.Android.dll` *binding assembly* for each API
level.

This means there is no *single* `Mono.Android.dll`, there is instead a *set*
of them.

This complicates the "mental model" for the `Mono.Android` project, as
a *project* can have only one output, not many (...within reason...).
As such, building the `Mono.Android` project will only generate a single
`Mono.Android.dll`.

To control which API level is bound, set the `$(ApiLevel)` and
`$(XAFrameworkVersion)` properties. `$(ApiLevel)` is the Android API level,
*usually* a number, while `$(XAFrameworkVersion)` is the Xamarin.Android
`$(TargetFrameworkVersion)`.

The default values will target Android API-23, Android 6.0.

For example, to generate `Mono.Android.dll` for API-19 (Android 4.4):

    cd src/Mono.Android
    xbuild /p:ApiLevel=19 /p:XAFrameworkVersion=v4.4
    # creates bin\Debug\lib\xbuild-frameworks\MonoAndroid\v4.4\Mono.Android.dll
