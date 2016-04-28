Xamarin.Android
===============

Xamarin.Android provides open-source bindings of the Android SDK for use with
.NET managed languages such as C#.

# Configuration.Overrides.props

The Xamarin.Android build is heavily dependent on MSBuild, with the *intention*
that it should (eventually?) be possible to build the project simply by
checking out the repo, loading `Xamarin.Android.sln` into an IDE, and Building
the solution. (This isn't currently possible, and may never be, but it's
the *vision*.)

However, some properties may need to be altered in order to suit your
requirements, such as the location of a cache directory to store
the Android SDK and NDK.

To modify the build process, copy
[`Configuration.Override.props.in`][Configuration.Override.props.in]
to `Configuration.Override.props`, and edit the file as appropriate.
`Configuration.Override.props` is `<Import/>`ed by `Configuration.props`
and will override any default values specified in `Configuration.props`.

Overridable MSBuild properties include:

* `$(AndroidApiLevel)`: The Android API level to bind in `src/Mono.Android`.
* `$(AndroidFrameworkVersion)`: The Xamarin.Android `$(TargetFrameworkVersion)`
    version which corresponds to `$(AndroidApiLevel)`.
* `$(AndroidToolchainCacheDirectory)`: The directory to cache the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-archives`.
* `$(AndroidToolchainDirectory)`: The directory to install the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-toolchain`.
* `$(HostCc)`, `$(HostCxx)`: The C and C++ compilers to use to generate
    host-native binaries.

# Build Requirements

Building Xamarin.Android requires the Java Development Kit (JDK), several
pieces of the Android SDK, and the Android NDK.

The Java Development Kit may be downloaded from the
[Oracle Java SE Downloads page][download-jdk].

[download-jdk]: http://www.oracle.com/technetwork/java/javase/downloads/

To simplify building Xamarin.Android, important pieces of the Android SDK
and Android NDK will be automatically downloaded and installed from
Google's website. Downloaded files are cached locally, by default into
`$(AndroidToolchainDirectory)`. The Android NDK and SDK will be installed by
default into `$(AndroidToolchainDirectory)`.

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

To control which API level is bound, set the `$(AndroidApiLevel)` and
`$(AndroidFrameworkVersion)` properties. `$(AndroidApiLevel)` is the
Android API level, *usually* a number, while `$(AndroidFrameworkVersion)`
is the Xamarin.Android `$(TargetFrameworkVersion)`.

The default values will target Android API-23, Android 6.0.

For example, to generate `Mono.Android.dll` for API-19 (Android 4.4):

    cd src/Mono.Android
    xbuild /p:AndroidApiLevel=19 /p:AndroidFrameworkVersion=v4.4
    # creates bin\Debug\lib\xbuild-frameworks\MonoAndroid\v4.4\Mono.Android.dll

## Contributing ##

### Mailing Lists

To discuss this project, and participate in the design, we use the [macios-devel@lists.xamarin.com](http://lists.xamarin.com/mailman/listinfo/android-devel) mailing list.   

### Coding Guidelines

We use [Mono's Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

### Reporting Bugs

We use [Bugzilla](https://bugzilla.xamarin.com/newbug) to track issues.

