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

To build Xamarin.Android, load `Xamarin.Android.sln` into Xamarin Studio 6
and Build the project.
