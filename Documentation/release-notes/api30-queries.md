#### Application and library build and deployment

Starting in [Android 11][0], for Fast Deployment to work on an API 30
device or emulator, the following `<queries/>` entries must be present in
`AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android" android:versionCode="1" android:versionName="1.0" package="com.xamarin.android.helloworld">
  <uses-sdk android:targetSdkVersion="30" />
  <!-- ... -->
  <queries>
    <package android:name="Mono.Android.DebugRuntime" />
    <package android:name="Mono.Android.Platform.ApiLevel_30" />
  </queries>
</manifest>
```

These will be generated if `$(AndroidUseSharedRuntime)` is `true` and
`android:targetSdkVersion` is 30 or higher.

### manifestmerger.jar version update to 27.0.0

The version of the [manifest merger][1] included in Xamarin.Android
has been updated from 26.5.0 to 27.0.0.

[0]: https://developer.android.com/preview/privacy/package-visibility#package-name
[1]: https://developer.android.com/studio/build/manifest-merge.html
