# Installation of Native Libraries

An Android `.apk` file can contain native libraries for one or more
architectures.  Historically, all native libraries for the target
device are extracted at `.apk` installation time.  This would result
in *two* copies of native libraries on the target device: a set of
native libraries which are stored compressed within the `.apk`, and
a separate set of native libraries on the Android target filesystem.

Starting with Android v6.0 (API-23), Android added an ability for
native libraries to be stored *uncompressed* within the `.apk` along
with the ability to load those native libraries *from* the `.apk`
without requiring a separate filesystem copy of the native libraries.

Android Things *requires* this new mechanism; `.apk` files installed
on Android Things will no longer have *any* native libraries extracted.

As a result, the `.apk` will be *larger*, because the native
libraries are stored uncompressed within the `.apk`, but the
install size will be *smaller*, because there isn't a second "copy"
of the native libraries (one compressed in the `.apk`, one outside
of the `.apk`).

On Android versions older than Android v6.0, the native libraries
will continue to be extracted during `.apk` installation.

In order to indicate to Android v6.0 and later that native libraries
do not need to be extracted, the
[`//application/@android:extractNativeLibs`][extractNativeLibs]
attribute within `AndroidManifest.xml` must be set to `false.`

[extractNativeLibs]: https://developer.android.com/guide/topics/manifest/application-element#extractNativeLibs

## OSS Implementation Details

When `AndroidManifest.xml` contains an XML attribute matching
`//application[@android:extractNativeLibs='false']`, the
Xamarin.Android build system will do the following:

 1. The `$(AndroidStoreUncompressedFileExtensions)` MSBuild property
    will be automatically updated to contain the `.so` file
    extension, causing native libraries to be stored uncompressed
    within the `.apk`.

 2. The `__XA_DSO_IN_APK` environment variable will be set within the
    created `.apk` file with the value of `1`, indicating to
    the app that native libraries should be loaded from the `.apk`
    itself instead of from the filesystem.

