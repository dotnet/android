### XA0119 error for unsupported use of Android App Bundle format in Debug configuration

The Android App Bundle publishing format is not currently compatible with the
recommended fast deployment settings for Debug configuration deployments.
Previously, projects that had **Android Package Format**
(`AndroidPackageFormat`) set to **aab** in the Debug configuration along with
the recommended **Use Shared \[Mono\] Runtime** setting would produce a build
warning, but they would also fail to launch on device.

Since this configuration is not currently supported, Xamarin.Android now emits a
build error for it instead of warning:

```
error XA0119: Using the shared runtime and Android App Bundles at the same time is not currently supported. Use the shared runtime for Debug configurations and Android App Bundles for Release configurations.
```

To resolve this error, change the **Android Package Format** setting in the
Visual Studio project property pages to **apk** for the Debug configuration.
This corresponds to the `apk` value for the `AndroidPackageFormat` MSBuild
property in the _.csproj_ file:

```xml
<PropertyGroup>
  <AndroidPackageFormat>apk</AndroidPackageFormat>
</PropertyGroup>
```

This error is only relevant for Debug configuration builds. Release
configuration builds can continue to use the Android App Bundle packaging
format.

