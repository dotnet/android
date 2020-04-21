## Unofficial MSBuild Project SDK support

This is an unofficial package which introduces a Xamarin.Android [MSBuild Project SDK](https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk).
In some ways this is a standalone implementation / extension of MSBuild.Sdk.Extras for Xamarin.Android.

Default Android related file globbing behavior is defined in `Microsoft.Android.Sdk.DefaultItems.props`.

### How to use it

  1) Install the [latest .NET 5 preview][0].

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
<Project Sdk="Microsoft.Android.Sdk/10.4.99.7">
  <PropertyGroup>
    <TargetFramework>netcoreapp5.0</TargetFramework>
    <RuntimeIdentifier>android.21-arm64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

  4) Publish (and optionally install) the project:

```
dotnet publish -t:Install *.csproj --self-contained
```

[0]:  https://github.com/dotnet/installer#installers-and-binaries
