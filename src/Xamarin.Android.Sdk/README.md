## Unofficial MSBuild Project SDK support

This is an unofficial package which introduces a Xamarin.Android [MSBuild Project SDK](https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk).
In some ways this is a standalone implementation / extension of MSBuild.Sdk.Extras for Xamarin.Android.

The `TargetFramework` element is the only required `.csproj` file element, this ensures NuGet restore behaves as expected on Windows and macOS.

```xml
<Project Sdk="Xamarin.Android.Sdk/0.0.1">
  <PropertyGroup>
    <TargetFramework>MonoAndroid10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Default Android related file globbing behavior is defined in `Xamarin.Android.Sdk.DefaultItems.props`.
