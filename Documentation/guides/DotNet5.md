# .NET 5 and Xamarin.Android

_NOTE: this document is very likely to change, as the requirements for
.NET 5 are better understood._

A .NET 5 project for Xamarin.Android will look something like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net5.0-android</TargetFramework>
  </PropertyGroup>
</Project>
```

See the [Target Framework Names in .NET 5][net5spec] spec for details.

[net5spec]: https://github.com/dotnet/designs/blob/5e921a9dc8ecce33b3195dcdb6f10ef56ef8b9d7/accepted/2020/net5/net5.md

## Changes to MSBuild tasks

In .NET 5 the behavior of the following MSBuild tasks will change, but
"legacy" projects will stay the same:

* `<ValidateJavaVersion/>` - used to require Java 1.6, 1.7, or 1.8
  based on the version of the Android Build Tools or
  `$(TargetFrameworkVersion)`. .NET 5 will require Java 1.8.

* `<ResolveAndroidTooling/>` - used to support the
  `$(AndroidUseLatestPlatformSdk)` setting or multiple
  `$(TargetFrameworkVersion)`. .NET 5 will always target the latest
  Android APIs for `Mono.Android.dll`.
