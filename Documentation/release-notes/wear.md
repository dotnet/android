#### Deprecation of Android Wear Embedding

* warning XA4312: Embedding an [Android Wear][0] application inside an
  Android application is deprecated. Distribute the Wear application
  as a [standalone application][1] instead.

From a `Foo.Android.csproj` Xamarin.Android project, you could
reference a `Foo.Wear.csproj` such as:

```xml
<ProjectReference Include="..\Foo.Wear\Foo.Wear.csproj">
  <Name>Wearable</Name>
  <IsAppExtension>True</IsAppExtension>
  <ReferenceOutputAssembly>False</ReferenceOutputAssembly>
</ProjectReference>
```

This would embed `com.foo.wear.apk` *inside* `com.foo.android.apk` in
`Resources/raw`.

Distribute `Foo.Wear.csproj` in the above example as a [standalone][1]
Android Wear application instead.

##### Android Wear 1.x

Note that only Android Wear 2.0 and higher is supported by
Xamarin.Android. Android Wear 1.x applications fail to compile with:

```
error XA0121: Assembly 'Xamarin.Android.Wearable' is using '[assembly: Java.Interop.JavaLibraryReferenceAttribute]', which is no longer supported. Use a newer version of this NuGet package or notify the library author.
error XA0121: Assembly 'Xamarin.Android.Wearable' is using '[assembly: Android.IncludeAndroidResourcesFromAttribute]', which is no longer supported. Use a newer version of this NuGet package or notify the library author.
```

[0]: https://docs.microsoft.com/xamarin/android/wear/get-started/intro-to-wear
[1]: https://developer.android.com/training/wearables/apps/standalone-apps
