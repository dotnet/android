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

## Changes to MSBuild properties

`$(AndroidUseIntermediateDesignerFile)` will be `True` by default.

## dotnet cli

There are currently two "verbs" we are aiming to get working in
Xamarin.Android:

    dotnet build
    dotnet publish

Currently in .NET Core (aka .NET 5), `dotnet publish` is where all the
work to produce a self-contained "app" happens:

* The linker via the `<IlLink/>` MSBuild task
* .NET Core's version of AOT, named "ReadyToRun"

https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#readytorun-images

This means Xamarin.Android would run the following during `dotnet
build`:

* Run `aapt` to generate `Resource.designer.cs` and potentially emit
  build errors for issues in `@(AndroidResource)` files.
* Compile C# code

Almost everything else happens during `donet publish`:

* Generate java stubs, `AndroidManifest.xml`, etc. This must happen
  after the linker.
* Compile java code via `javac`
* Convert java code to `.dex` via d8/r8
* Create an `.apk` and sign it
