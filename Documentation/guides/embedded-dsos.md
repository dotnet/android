# Embedded DSOs

Android v6.0 (API-23) introduced a new way of dealing with the native
shared libraries shipped in the `.apk`. Before API-23, the libraries
would be always extracted and placed in the application data
directory, thus occupying more space than necessary. API-23 added a
new `AndroidManifest.xml` attribute,
`//application/@android:extractNativeLibs`, which if set to `false`
makes Android *not* extract the libraries to the filesystem.  API-23
added a way to instead load those libraries directly from the `.apk`.
In order to support that there are a few requirements which this
commit implements:

  * DSO (`.so`) files must be *stored uncompressed* in the `.apk`.
  * `<application android:extractNativeLibs="false"/>` must be set
  * DSOs in the `.apk` must be aligned on the memory page boundary;
    `zipalign -p` takes care of this.

This commit also implements `libmonodroid.so` suport for loading our
DSOs directly from the `.apk`.  This operation mode is enabled by the
presence of the `$__XA_DSO_IN_APK` environment variable.  This
environment variable is inserted into the application's environment
by way of placing it in the environment file (a file part of the
Xamarin.Android App project that has the `@(AndroidEnvironment)`
build action).  In this mode, the DSOs are *no longer* looked up in
the application data directory but only in the override directories
(if the APK is built in Debug configuration) and the `.apk` itself.

When `/manifest/application/@android:extractNativeLibs` is set to
`false` in `AndroidManifest.xml`, Xamarin.Android should automatically
setup the proper uncompressed file extension settings and environment
variables. Adding the value to the manifest should "just work".

See the official Android documentation for details about
[extractNativeLibs][extractNativeLibs] and its usage.

[extractNativeLibs]: https://developer.android.com/guide/topics/manifest/application-element#extractNativeLibs

# MSBuild Implementation Details

When `/manifest/application/@android:extractNativeLibs` is set to
`false` in `AndroidManifest.xml`, Xamarin.Android's build system would
automatically do the following:

1. `.so` will be automatically added to
   `$(AndroidStoreUncompressedFileExtensions)`. Both the `<Aapt/>` and
   `<BuildApk/>` MSBuild tasks use this property.

3. A new `dsoenvironment.txt` generated file in
   `$(IntermediateOutputPath)` will add `__XA_DSO_IN_APK=1` as an
   `@(AndroidEnvironment)` build item.

To make this happen, Xamarin.Android's MSBuild targets will set the
`$(_EmbeddedDSOsEnabled)` property. A new `_SetupEmbeddedDSOs` MSBuild
target creates a `$(IntermediateOutputPath)dsoenvironment.txt` file
containing `__XA_DSO_IN_APK=1` when `$(_EmbeddedDSOsEnabled)` is
`True`. `_SetupEmbeddedDSOs` also prepends `.so;` to
`$(AndroidStoreUncompressedFileExtensions)`.
