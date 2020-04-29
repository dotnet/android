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

`$(AndroidBoundExceptionType)` will be `System` by default.  This will
[alter the types of exceptions thrown from various methods][abet-sys] to
better align with existing .NET 5 semantics, at the cost of compatibility with
previous Xamarin.Android releases.

[abet-sys]: https://github.com/xamarin/xamarin-android/issues/4127

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
<Project Sdk="Microsoft.Android.Sdk/10.0.100">
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

## Package Versioning Scheme

Our NuGet packages are versioned using [Semver 2.0.0][2].

This is the scheme: `OS-Major.OS-Minor.InternalRelease[-prereleaseX]+sha.1b2c3d4`.

* Major: The major OS version.
* Minor: The minor OS version.
* Patch: Our internal release version based on `100` as a starting point.
    * Service releases will bump the last two digits of the patch version
    * Feature releases will round the patch version up to the nearest 100
      (this is the same as bumping the first digit of the patch version, and
      resetting the last two digits to 0).
    * This follows [how the dotnet SDK does it][1].
* Pre-release: Optional (e.g.: Android previews, CI, etc.)
    * For CI we use a `ci` prefix + the branch name (cleaned up to only be
      alphanumeric) + the commit distance (number of commits since any of the
      major.minor.patch versions changed).
        * Example: `Android 10.0.100-ci.master.1234`
        * Alphanumeric means `a-zA-Z0-9-`: any character not in this range
          will be replaced with a `-`.
    * Pull requests have `pr` prefix, followed by `gh`+ PR number + commit
      distance.
        * Example: `Android 10.1.200-ci.pr.gh3333.1234`
    * If we have a particular feature we want people to subscribe to (such as
      an Android preview release), we publish previews with a custom pre-release
      identifier:
        * Example: `Android 10.1.100-android_q.beta.1`
        * This way people can sign up for only official previews, by
          referencing `Android *-android_q.beta.*`
        * It's still possible to sign up for all `android_q` builds, by
          referencing `Android *-ci.android_q.*`
* Build metadata: Required Hash
    * This is `sha.` + the short commit hash.
        * Use the short hash because the long hash is quite long and
          cumbersome. This leaves the complete version open for duplication,
          but this is extremely unlikely.
    * Example: `Android 10.0.100+sha.1a2b3c`
    * Example (CI build): `Android 10.0.100-ci.master.1234+sha.1a2b3c`
    * Since the build metadata is required for all builds, we're able to
      recognize incomplete version numbers and determine if a particular
      version string refers to a stable version or not.
        * Example: `Android 10.0.100`: incomplete version
        * Example: `Android 10.0.100+sha.1a2b3c`: stable
        * Example: `Android 10.0.100-ci.d17_0.1234+sha.1a2b3c`: CI build
        * Example: `Android 10.0.100-android_q.beta.1+sha.1a2b3c`: official
          preview
            * Technically it's possible to remove the prerelease part, but
              we’d still be able to figure out it’s not a stable version by
              using the commit hash.


[0]: https://github.com/dotnet/installer#installers-and-binaries
[1]: https://github.com/dotnet/designs/blob/master/accepted/2018/sdk-version-scheme.md
[2]: https://semver.org
