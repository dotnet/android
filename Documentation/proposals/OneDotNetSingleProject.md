# One .NET "Single Project"

One theme for Xamarin in .NET 6 is to simplify the cross-platform
experience between iOS and Android. We would like to provide
cross-platform alternatives to `AndroidManifest.xml` and `Info.plist`.
The eventual goal would be to remove the need for these files for
simple apps and project templates. Developers would only need to
create them when a less widely used feature is needed. We would need
to create item templates for `AndroidManifest.xml` and `Info.plist`
when (and if) they are removed from project templates.

The concepts for a Xamarin "single project" go much beyond this. This
document is a starting point for what needs to go in the .NET Android
and Apple SDKs, while much of the remaining features would be in the
Xamarin.Forms/MAUI MSBuild targets.

For starters, we can add the following MSBuild properties to the
Xamarin.Android and Xamarin.iOS/Mac SDKs:

* `$(ApplicationId)` maps to `/manifest/@package` and
  `CFBundleIdentifier`
* `$(ApplicationVersion)` maps to `android:versionName` or
  `CFBundleVersion`. This is a version string that must be incremented
  for each iOS App Store or TestFlight submission.
* `$(AndroidVersionCode)` maps to `android:versionCode` (_Android
  only)_. This is unfortunately an integer and must be incremented for
  each Google Play submission.
* `$(AppleShortVersion)` maps to `CFBundleShortVersionString` (_iOS
  only)_. This can default to `$(ApplicationVersion)` when blank.
* `$(ApplicationTitle)` maps to `/application/@android:title` or
  `CFBundleDisplayName`

The final value that is generated in the `Info.plist` or
`AndroidManifest.xml` can be overridden at different times. The final
source of truth is determined in order of:

1. `Info.plist` or `AndroidManifest.xml` in the iOS/Android head project.
2. iOS/Android head `.csproj` defines the MSBuild properties
3. _(To be implemented in MAUI/Forms)_ set in a shared `.csproj`.
4. The properties set by MSBuild via other means such as
   `Directory.Build.props`, etc.

Even if we did not complete the goal of complete removal of
`AndroidManifest.xml` and `Info.plist` from Xamarin project templates,
these new MSBuild properties would be useful in their own right.

## Opting out

.NET Core introduced `$(GenerateAssemblyInfo)` which defaults to `true`.
Projects migrating to .NET Core might set this to `false` if they have
an existing `Properties/AssemblyInfo.cs`. We should have a similar
property to disable the behavior.

`$(GenerateApplicationManifest)` defaults to `true` in .NET 6 and
`false` in "legacy" Xamarin.Android/Xamarin.iOS.

In most cases, developers would only use `$(GenerateApplicationManifest)`
if they want to try the new features in "legacy" Xamarin.

## AssemblyVersion and FileVersion

Since we are adding *more* version properties, we should consider
adding defaults to consolidate the assembly-level attributes when
using `$(GenerateAssemblyInfo)`.

The full list of defaults might be something like:

```xml
<PropertyGroup>
  <ApplicationVersion Condition=" '$(ApplicationVersion)' == '' ">1.0</ApplicationVersion>
  <!-- Android only -->
  <AndroidVersionCode Condition=" '$(AndroidVersionCode)' == '' ">1</AndroidVersionCode>
  <!-- Apple platforms only -->
  <AppleShortVersion Condition=" '$(AppleShortVersion)' == '' ">$(ApplicationVersion)</ApplicationVersion>
  <AssemblyVersion Condition=" '$(AssemblyVersion)' == '' ">$(ApplicationVersion)</AssemblyVersion>
  <FileVersion Condition=" '$(FileVersion)' == '' ">$(ApplicationVersion)</FileVersion>
</PropertyGroup>
```

## Android Template

The default Android project template would include:

```xml
<!-- .csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0-android</TargetFramework>
    <ApplicationTitle>@string/application_title</ApplicationTitle>
    <ApplicationId>com.companyname.myapp</ApplicationId>
    <ApplicationVersion>1.0</ApplicationVersion>
    <AndroidVersionCode>1</AndroidVersionCode>
  </PropertyGroup>
</Project>

<!-- Resources/values/strings.xml -->
<resources>
    <string name="application_title">MyApp</string>
</resources>
```

Removed from `AndroidManifest.xml` in the project template:

* `/manifest/@android:versionCode="1"`
* `/manifest/@android:versionName="1.0"`
* `/manifest/@package="com.companyname.myapp"`
* `/application/@android:label="MyApp"`

All values could be added later to the `AndroidManifest.xml` and
override the MSBuild properties.

## iOS Template

The default iOS project template would include:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0-ios</TargetFramework>
    <ApplicationTitle>MyApp</ApplicationTitle>
    <ApplicationId>com.companyname.myapp</ApplicationId>
    <ApplicationVersion>1.0</ApplicationVersion>
  </PropertyGroup>
</Project>
```

`$(AppleShortVersion)` can default to `$(ApplicationVersion)` when
blank.

Removed from `Info.plist` in the project template:

* `CFBundleDisplayName`
* `CFBundleIdentifier`
* `CFBundleVersion`
* `CFBundleShortVersionString`

All values could be added later to the `Info.plist` and override the
MSBuild properties.

## Example

You could setup a cross-platform solution in .NET 6 with:

* `Hello/Hello.csproj` - `net6.0` shared code
* `HelloAndroid/HelloAndroid.csproj` - `net6.0-android`
* `HelloiOS/HelloiOS.csproj` - `net6.0-android`
* `Hello.sln`
* `Directory.Build.props`

Where `Directory.Build.props` can be setup for both platforms at once
with:

```xml
<Project>
  <PropertyGroup>
    <ApplicationTitle>Hello!</ApplicationTitle>
    <ApplicationId>com.companyname.hello</ApplicationId>
    <ApplicationVersion>1.0.0</ApplicationVersion>
    <AndroidVersionCode>1</AndroidVersionCode>
  </PropertyGroup>
</Project>
```

In this project, a developer would increment `$(ApplicationVersion)`
and `$(AndroidVersionCode)` for each public release.

For our long-term vision, we could one day have a single project that
multi-targets:

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net6.0-android;net6.0-ios</TargetFrameworks>
    <ApplicationTitle>Hello!</ApplicationTitle>
    <ApplicationId>com.companyname.hello</ApplicationId>
    <ApplicationVersion>1.0.0</ApplicationVersion>
    <AndroidVersionCode>1</AndroidVersionCode>
  </PropertyGroup>
</Project>
```

## Localization

`$(ApplicationTitle)` can easily be localized on Android by using an
**AndroidResource** as the value.

However, on iOS you would need to specify
[`LSHasLocalizedDisplayName`][0] in the `Info.plist` and provide
`CFBundleName` or `CFBundleDisplayName` in a `.strings` file for each
supported language. For our first implementation, you would omit
`$(ApplicationTitle)` from the `.csproj` file if you need to localize
it.

One can imagine supporting a `.resx` key via a new
`$(LocalizedApplicationTitle)` property. This would likely need to be
implemented in Xamarin.Forms/MAUI MSBuild tasks as a way to provide a
single `.resx` file to be translated to the appropriate format for iOS
and Android. This is a consideration for the future.

[0]: https://developer.apple.com/library/archive/documentation/General/Conceptual/MOSXAppProgrammingGuide/BuildTimeConfiguration/BuildTimeConfiguration.html

## Other Future Work

In future iterations, we can consider additional MSBuild properties
beyond `$(ApplicationTitle)`, `$(ApplicationId)`,
`$(ApplicationVersion)`, `$(AndroidVersionCode)`, and
`$(AppleShortVersion)`.

This is a list of additional properties that cover most of the
property pages in Visual Studio:

* `$(AndroidMinSdkVersion)`
* `$(AndroidTargetSdkVersion)`
* `$(AndroidInstallLocation)`
* `$(iOSMinimumOSVersion)`, `$(tvOSMinimumOSVersion)`,
  `$(MacMinimumOSVersion)`, and potentially other variants for
  Catalyst, etc.
* `$(AppleLaunchScreen)`
* `$(AppleDeviceFamily)` - `iPhone`, `iPad`, `Universal`, `TV`, and
  potentially other variants for Catalyst, etc.
* `$(AppleMainStoryboard)`
* `$(ApplicationIcon)`
* `$(ApplicationRoundIcon)` or `$(AndroidRoundIcon)`

Some settings would make more sense as an item group:

* `@(iOSSupportedInterfaceOrientations)` _iOS only_ - an item group
  that needs to support `UISupportedInterfaceOrientations` and
  `UISupportedInterfaceOrientations~ipad`
* `@(ApplicationPermission)` - an item group that maps to common
  permissions, similar to some behavior in Xamarin.Essentials.

To completely remove `AndroidManifest.xml` we would need to somehow
provide (or emit by default):

* `/android/@android:allowBackup="true"`
* `/android/@android:supportsRtl="true"`

To completely remove `Info.plist` we would need to somehow
provide (or emit by default):

* `LSRequiresIPhoneOS`
* `UIRequiredDeviceCapabilities` for `armv7`
