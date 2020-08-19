#### Application and library build and deployment

* [GitHub Issue 4986](https://github.com/xamarin/xamarin-android/issues/4986):
  Updates to Android tooling (`manifest-merger`), caused
  `//application/@android:extractNativeLibs` to be set to `false` by
  default. This can cause an undesirable `.apk` file size increase
  that is more noticeable for Xamarin.Android applications using AOT.
  Xamarin.Android now sets `extractNativeLibs` to `true` by default.

According to the [Android documentation][extractNativeLibs],
`extractNativeLibs` affects `.apk` size and install size:

> Whether or not the package installer extracts native libraries from
> the APK to the filesystem. If set to false, then your native
> libraries must be page aligned and stored uncompressed in the APK.
> No code changes are required as the linker loads the libraries
> directly from the APK at runtime. The default value is "true".

This is a tradeoff that each developer should decide upon on a
per-application basis. Is a smaller install size at the cost of a
larger download size preferred?

Since Xamarin.Android now emits `android:extractNativeLibs="true"` by
default, you can get the opposite behavior with an
`AndroidManifest.xml` such as:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android" android:versionCode="1" android:versionName="1.0" package="com.companyname.hello">
    <uses-sdk android:minSdkVersion="23" android:targetSdkVersion="30" />
    <application android:label="Hello" android:extractNativeLibs="false" />
</manifest>
```

[extractNativeLibs]: https://developer.android.com/guide/topics/manifest/application-element
