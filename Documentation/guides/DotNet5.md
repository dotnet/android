# .NET 5 and Xamarin.Android

_NOTE: this document is very likely to change, as the requirements for
.NET 5 are better understood._

A .NET 5 project for a Xamarin.Android application will look something
like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net5.0-android</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

For a "library" project, you would omit the `$(OutputType)` property
completely or specify `Library`.

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

## Default file inclusion

Default Android related file globbing behavior is defined in `Microsoft.Android.Sdk.DefaultItems.props`.

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
* Create an `.apk` or `.aab` and sign it


### Preview testing

The following instructions can be used for early preview testing.

  1) Install the [latest .NET 5 preview][0]. Preview 4 or later is required.

  2) Create a `nuget.config` file that has a package source pointing to
     local packages or `xamarin-impl` feed, as well as the .NET 5 feed:

```xml
<configuration>
  <packageSources>
    <add key="xamarin-impl" value="https://pkgs.dev.azure.com/azure-public/vside/_packaging/xamarin-impl/nuget/v3/index.json" />
    <add key="dotnet5" value="https://dnceng.pkgs.visualstudio.com/public/_packaging/dotnet5/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

  3) Open an existing Android project (ideally something minimal) and
    tweak it as shown below. The version should match the version of the
    packages you want to use:

```xml
<Project Sdk="Microsoft.Android.Sdk/10.4.99.24">
  <PropertyGroup>
    <TargetFramework>netcoreapp5.0</TargetFramework>
    <RuntimeIdentifier>android.21-arm64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

  4) Publish (and optionally install) the project:

```
dotnet publish -t:Install *.csproj
```

[0]:  https://github.com/dotnet/installer#installers-and-binaries
