<img src="Documentation/banner.png" alt="Xamarin.Android banner" height="145" >

Xamarin.Android
===============

Xamarin.Android provides open-source bindings of the Android SDK for use with
.NET managed languages such as C#.

[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/xamarin/xamarin-android?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

# Build Status

| Platform              | Status |
|-----------------------|--------|
| **OSS macOS**         | [![OSS macOS x86_64][oss-macOS-x86_64-icon]][oss-macOS-x86_64-status] |
| **OSS Ubuntu**        | [![OSS Linux/Ubuntu x86_64][oss-ubuntu-x86_64-icon]][oss-ubuntu-x86_64-status] |

[oss-macOS-x86_64-icon]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android/badge/icon
[oss-macOS-x86_64-status]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android/
[oss-ubuntu-x86_64-icon]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-linux/badge/icon
[oss-ubuntu-x86_64-status]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-linux/

# Downloads

| Platform        | Status |
|-----------------|--------|
| **Commercial Xamarin.Android 7.1 (Cycle 9)** for macOS       | [![Commercial Xamarin.Android 7.1, macOS][commercial-c9-macOS-x86_64-icon]][commercial-c9-macOS-x86_64-status] |
| **Commercial Xamarin.Android 7.3 (d15-2)** for macOS         | [![Commercial Xamarin.Android 7.3, macOS][commercial-d15-2-macOS-x86_64-icon]][commercial-d15-2-macOS-x86_64-status] |
| **Commercial Xamarin.Android 7.3.99 (master)** for macOS     | [![Commercial Xamarin.Android 7.3.99, macOS][commercial-master-macOS-x86_64-icon]][commercial-master-macOS-x86_64-status] |

[commercial-c9-macOS-x86_64-icon]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-builds-cycle9/badge/icon
[commercial-c9-macOS-x86_64-status]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-builds-cycle9/
[commercial-d15-2-macOS-x86_64-icon]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-builds-d15-2/badge/icon
[commercial-d15-2-macOS-x86_64-status]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-builds-d15-2/
[commercial-master-macOS-x86_64-icon]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-builds-master/badge/icon
[commercial-master-macOS-x86_64-status]: https://jenkins.mono-project.com/view/Xamarin.Android/job/xamarin-android-builds-master/

# Build Requirements

Building Xamarin.Android requires:

* [Mono 4.4 or later](#mono-sdk)
* [The Java Development Kit (JDK)](#jdk)
* [Autotools (`autoconf`, `automake`, etc.)](#autotools)
* [The Android SDK and NDK](#ndk)

The `make prepare` build step (or `PrepareWindows.targets` on Windows) will
check that all required dependencies are present.
If you would like `make prepare` to automatically install
required dependencies, set the `$(AutoProvision)` MSBuild property to True
and (if necessary) set the `$(AutoProvisionUsesSudo)` property to True.
(This is not supported on all operating systems.)

If `$(AutoProvision)` is False (the default) and a dependency is missing,
then the build will fail and an error message will be displayed attempting
to provide install instructions to obtain the missing dependency, e.g.:

    error : Could not find required program '7za'. Please run: brew install 'p7zip'.

<a name="mono-sdk" />

## Mono MDK

Mono 4.4 or later is required to build on [OS X][osx-mono] and Linux.

(This is because the build system uses the [XmlPeek][xmlpeek] task, which
was first added in Mono 4.4.)

[osx-mono]: http://www.mono-project.com/download/#download-mac
[xmlpeek]: https://msdn.microsoft.com/en-us/library/ff598684.aspx

<a name="jdk" />

## Java Development Kit

The Java Development Kit may be downloaded from the
[Oracle Java SE Downloads page][download-jdk].

[download-jdk]: http://www.oracle.com/technetwork/java/javase/downloads/

<a name="autotools" />

## Autotools

Autotools -- including `autoconf` and `automake` -- are required to build
the Mono runtimes.

On OS X, autotools are distributed with [Mono.framework][osx-mono].

If you run into issues regarding `autoconf` or `automake` try to install it with `brew` via:

    brew install automake

<a name="ndk" />

## Android NDK, SDK

*Note*: A xamarin-android checkout maintains *its own* Android NDK + SDK
to ensure consistent builds and build behavior, permitting reproducible
builds and providing greater flexibility around when we need to perform
Android SDK + NDK updates. The Android SDK and NDK are maintained by default
via two directories in your home directory:

* `$(AndroidToolchainCacheDirectory)`: Where downloaded files are cached.
    Defaults to the `$HOME/android-archives` directory.
* `$(AndroidToolchainDirectory)`: Where the Android NDK and SDK are installed.
    Defaults to the `$HOME/android-toolchain` directory.

Developers may use these directories for their own use, but *please* **DO NOT**
update or alter the contents of the `$(AndroidToolchainDirectory)`, as that may
prevent the xamarin-android build from working as expected.

The files that will be downloaded and installed are controlled by
[build-tools/android-toolchain/android-toolchain.projitems][android-toolchain.projitems]
via the `@(AndroidNdkItem)` and `@(AndroidSdkItem)` item groups, and the
URL to download files from is controlled by the `$(AndroidUri)` property.

[android-toolchain.projitems]: build-tools/android-toolchain/android-toolchain.projitems

# Build Configuration

The Xamarin.Android build is heavily dependent on MSBuild, with the *intention*
that it should (eventually?) be possible to build the project simply by
checking out the repo, loading `Xamarin.Android.sln` into an IDE, and Building
the solution. (This isn't currently possible, and may never be, but it's
the *vision*.)

However, some properties may need to be altered in order to suit your
requirements, such as the location of a cache directory to store
the Android SDK and NDK.

To modify the build process, copy
[`Configuration.Override.props.in`](Configuration.Override.props.in)
to `Configuration.Override.props`, and edit the file as appropriate.
`Configuration.Override.props` is `<Import/>`ed by `Configuration.props`
and will override any default values specified in `Configuration.props`.

Overridable MSBuild properties include:

* `$(AutoProvision)`: Automatically install required dependencies, if possible.
* `$(AutoProvisionUsesSudo)`: Use `sudo` when installing dependencies.
* `$(AndroidApiLevel)`: The Android API level to bind in `src/Mono.Android`.
    This is an integer value, e.g. `15` for
    [API-15 (Android 4.0.3)](http://developer.android.com/about/versions/android-4.0.3.html).
* `$(AndroidFrameworkVersion)`: The Xamarin.Android `$(TargetFrameworkVersion)`
    version which corresponds to `$(AndroidApiLevel)`. This is *usually* the
    Android version number with a leading `v`, e.g. `v4.0.3` for API-15.
* `$(AndroidSupportedHostJitAbis)`: The Android ABIs for which to build a
    host JIT *and* Xamarin.Android base class libraries (`mscorlib.dll`/etc.).
    The "host JIT" is used e.g. with the Xamarin Studio Designer, to render
    Xamarin.Android apps on the developer's machine.
    There can also be support for cross-compiling mono for a different
    host, e.g. to build Windows `libmonosgen-2.0.dll` from OS X.
    Supported host values include:

    * `Darwin`
    * `Linux`
    * `mxe-Win64`: Cross-compile Windows 64-bit binaries from Unix.

    The default value is `$(HostOS)`, where `$(HostOS)` is based on probing
    various environment variables and filesystem locations.
    On OS X, the default would be `Darwin`.

* `$(AndroidSupportedTargetAotAbis)`: The Android ABIs for which to build the
    Mono AOT compilers. The AOT compilers are required in order to set the
    [`$(AotAssemblies)`][aot-assemblies] app configuration property to True.
    
    [aot-assemblies]: https://developer.xamarin.com/guides/android/under_the_hood/build_process/#AotAssemblies

* `$(AndroidSupportedTargetJitAbis)`: The Android ABIs for which to build the
    the Mono JIT for inclusion within apps. This is a `:`-separated list of
    ABIs to build. Supported values are:

    * `armeabi`
    * `armeabi-v7a`
    * `arm64-v8a`
    * `x86`
    * `x86_64`

* `$(AndroidToolchainCacheDirectory)`: The directory to cache the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-archives`.
* `$(AndroidToolchainDirectory)`: The directory to install the downloaded
    Android NDK and SDK files. This value defaults to
    `$(HOME)\android-toolchain`.
* `$(HostCc)`, `$(HostCxx)`: The C and C++ compilers to use to generate
    host-native binaries.
* `$(JavaInteropSourceDirectory)`: The Java.Interop source directory to
    build and reference projects from. By default, this is
    `external/Java.Interop` directory, maintained by `git submodule update`.
* `$(MakeConcurrency)`: **make**(1) parameters to use intended to influence
    the number of CPU cores used when **make**(1) executes. By default this uses
    `-jCOUNT`, where `COUNT` is obtained from `sysctl hw.ncpu`.
* `$(MonoRequiredMinimumVersion)`: The minimum *system* mono version that is
    supported in order to allow a build to continue. Policy is to require a
    system mono which corresponds vaguely to the [`external/mono`](external)
    version. This is not strictly required; older mono versions *may* work, they
    just are not tested, and thus not guaranteed or supported.  
    By default this is `4.9.3`.
* `$(MonoSgenBridgeVersion)`: The Mono SGEN Bridge version to support.
    Valid values include:

    * `4`: Mono 4.6 support.
    * `5`: Mono 4.8 and above support. This is the default.

# Build

Xamarin.Android can be built on Linux, macOS, and Windows.

## Linux and macOS

To build Xamarin.Android, first prepare the project:

    make prepare

This will perform `git submodule update`, and any other pre-build tasks
that need to be performed. After this process is completed, ensure there
is no existing git changes in the `external` folder.

On the main repo, you can use `git status` to ensure a clean slate.

Next, run `make`:

    make

The default `make all` target will only build a *subset* of runtime ABIs
and `$(TargetFrameworkVersion)`s. If you want a complete environment --
*all* the ABIs, all the API levels -- then instead use:

    make jenkins

Unit tests are build in a separate target:

    make all-tests

## Windows

To build Xamarin.Android, ensure you are using MSBuild version 15+ and run:

    msbuild build-tools\scripts\PrepareWindows.targets
    msbuild Xamarin.Android.sln

These are roughly the same as how `make prepare` and `make` are used on other platforms.

_NOTE: there is not currently an equivalent of `make jenkins` or `make all-tests` on Windows._

_Troubleshooting: Ensure you check your MSBuild version(`msbuild -version`) and path for the proper version of MSBuild._

## Linux build notes

If you have the `binfmt_misc` module enabled with any of Mono or Wine installed and
you plan to cross-build the Windows compilers and tools (by enabling the `mxe-Win32`
or `mxe-Win64` host targets) as well as LLVM+AOT targets, you will need to disable 
`binfmt_misc` for the duration of the build or the Mono/LLVM configure scripts will
fail to detect they are cross-compiling and they will produce Windows PE executables
for tools required by the build.

To disable `binfmt_misc` you need to issue the following command as root:

    echo 0 > /proc/sys/fs/binfmt_misc/status

and to enable it again, issue the following command:

    echo 1 > /proc/sys/fs/binfmt_misc/status

## macOS Build Notes

The [`android-toolchain.projitems`](build-tools/android-toolchain/android-toolchain.projitems),
[`libzip.projitems`](build-tools/libzip/libzip.projitems), and
[`monodroid.projitems`](src/monodroid/monodroid.projitems) project files, among
others, use the `@(RequiredProgram)` build action to check for the existence
of a program within `$PATH` during the build. If a required program doesn't
exist, then the build will fail and a suggested `brew install` command line
will be provided to install the missing commands.

### Brew Programs

Suggested `brew install` commands:

    brew install cmake
    brew install libtool
    brew install p7zip
    brew install gdk-pixbuf
    brew install gettext
    brew install coreutils
    brew install findutils
    brew install gnu-tar
    brew install gnu-sed
    brew install gawk
    brew install gnutls
    brew install gnu-indent
    brew install gnu-getopt
    brew install intltool
    brew install scons
    brew install wget
    brew install xz

If any program is still not found, try to ensure it's linked via:

    brew link <package name>

# Using Your Build

Once the build has finished, [`tools/scripts/xabuild`](tools/scripts/xabuild)
may be used on Unix-like platforms to build projects.
See the [Samples](#Samples) section for example usage.

Windows users will need to use the `setup-windows.exe` tool as described in
[`Documentation/UsingJenkinsBuildArtifacts.md`](Documentation/UsingJenkinsBuildArtifacts.md#oss-xamarinandroidzip-installation).

# Using Jenkins Build Artifacts

Please see
[`Documentation/UsingJenkinsBuildArtifacts.md`](Documentation/UsingJenkinsBuildArtifacts.md)
for details on using prebuilt Xamarin.Android binaries.

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

* `bin\$(Configuration)\lib\xamarin.android\xbuild\Xamarin\Android`: MSBuild-related support
    files and required runtimes used by the MSBuild tooling.
* `bin\$(Configuration)\lib\xamarin.android\xbuild-frameworks\MonoAndroid`: Xamarin.Android
    profiles.
* `bin\$(Configuration)\lib\xamarin.android\xbuild-frameworks\MonoAndroid\v1.0`: Xamarin.Android
    Base Class Library assemblies such as `mscorlib.dll`.
* `bin\$(Configuration)\lib\xamarin.android\xbuild-frameworks\MonoAndroid\*`: Contains
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

The default values will target Android API-25, Android 7.1.

For example, to generate `Mono.Android.dll` for API-19 (Android 4.4):

    cd src/Mono.Android
    xbuild /p:AndroidApiLevel=19 /p:AndroidFrameworkVersion=v4.4
    # creates bin\Debug\lib\xamarin.android\xbuild-frameworks\MonoAndroid\v4.4\Mono.Android.dll

<a name="Samples" />

# Samples

The [HelloWorld](samples/HelloWorld) sample may be built with the
[xabuild](tools/scripts/xabuild) script:

    $ tools/scripts/xabuild /t:SignAndroidPackage samples/HelloWorld/HelloWorld.csproj

`xabuild /t:SignAndroidPackage` will generate an `.apk` file, which may be
installed onto an Android device with the [`adb install`][adb-commands]
command:

[adb-commands]: http://developer.android.com/tools/help/adb.html#commandsummary

    $ adb install samples/HelloWorld/bin/Debug/com.xamarin.android.helloworld-Signed.apk

**HelloWorld** may be launched manually through the Android app launcher,
or via `adb shell am`:

    $ adb shell am start com.xamarin.android.helloworld/example.MainActivity

# Contributing

This project has adopted the code of conduct defined by the Contributor Covenant
to clarify expected behavior in our community. For more information, see the
[.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## Mailing Lists

To discuss this project, and participate in the design, we use the [android-devel@lists.xamarin.com](http://lists.xamarin.com/mailman/listinfo/android-devel) mailing list.   

## Coding Guidelines

We use [Mono's Coding Guidelines](http://www.mono-project.com/community/contributing/coding-guidelines/).

## Reporting Bugs

We use [Bugzilla](https://bugzilla.xamarin.com/enter_bug.cgi?product=Android) to track issues.

# Maintainer FAQ

See [DevelopmentTips.md](Documentation/DevelopmentTips.md).
