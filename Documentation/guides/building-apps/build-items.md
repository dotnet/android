---
title: "Build Items"
description: "This document will list all supported item groups in the Xamarin.Android build process."
ms.prod: xamarin
ms.assetid: 5EBEE1A5-3879-45DD-B1DE-5CD4327C2656
ms.technology: xamarin-android
author: jonpryor
ms.author: jopryo
ms.date: 09/23/2020
---

# Build items

Build items control how a Xamarin.Android application
or library project is built.

## AndroidAsset

Supports [Android Assets](https://developer.android.com/guide/topics/resources/providing-resources#OriginalFiles),
files that would be included in the `assets` folder in a Java Android project.

## AndroidAarLibrary

The Build action of `AndroidAarLibrary` should be used to directly
reference `.aar` files. This build action will be most commonly used
by Xamarin Components. Namely to include references to `.aar` files
which are required to get Google Play and other services working.

Files with this Build action will be treated in a similar fashion to
the embedded resources found in Library projects. The `.aar` will be
extracted into the intermediate directory. Then any assets, resource
and `.jar` files will be included in the appropriate item groups.

## AndroidAotProfile

Used to provide an AOT profile, for use with profile-guided AOT.

## AndroidBoundLayout

Indicates that the layout file is to have code-behind generated for it in case when
the `AndroidGenerateLayoutBindings` property is set to `false`. In all other aspects
it is identical to `AndroidResource` described above. This action can be used **only**
with layout files:

```xml
<AndroidBoundLayout Include="Resources\layout\Main.axml" />
```

## AndroidEnvironment

Files with a Build action of `AndroidEnvironment` are used
to [initialize environment variables and system properties during process startup](~/android/deploy-test/environment.md).
The `AndroidEnvironment` Build action may be applied to
multiple files, and they will be evaluated in no particular order (so don't
specify the same environment variable or system property in multiple
files).

## AndroidFragmentType

Specifies the default fully qualified type to be used for all `<fragment>` layout
elements when generating the layout bindings code. The property defaults to the standard
Android `Android.App.Fragment` type.

## AndroidJavaLibrary

Files with a Build action of `AndroidJavaLibrary` are Java
Archives ( `.jar` files) which will be included in the final Android
package.

## AndroidJavaSource

Files with a Build action of `AndroidJavaSource` are Java source code which
will be included in the final Android package.

## AndroidLintConfig

The Build action 'AndroidLintConfig' should be used in conjunction with the
[`$(AndroidLintEnabled)`](~/android/deploy-test/building-apps/build-properties.md#androidlintenabled)
property. Files with this build action will be merged together and passed to the
android `lint` tooling. They should be XML files which contain information on
which tests to enable and disable.

See the [lint documentation](https://developer.android.com/studio/write/lint)
for more details.

## AndroidManifestOverlay

The build action `AndroidManifestOverlay` can we used to provide additional
`AndroidManifest.xml` files to the [Manifest Merger]([~/android/deploy-test/building-apps/build-properties.md#](https://developer.android.com/studio/build/manifest-merge)) tool.
Files with this build action will be passed to the Manifest Merger along with
the main `AndroidManifest.xml` file and any additional manifest files from
references. These will then be merged into the final manifest.

You can use this build action to provide additional changes and settings to
your app depending on your build configuration. For example if you need to
have a specific permission only while debugging, you can use the overlay to
inject that permission when debugging. For example given the following
overlay file contents

```
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <uses-permission android:name="android.permission.CAMERA" />
</manifest>
```

you can use the following to add this for a debug build.

```
<ItemGroup>
  <AndroidManifestOverlay Include="DebugPermissions.xml" Condition=" '$(Configuration)' == 'Debug' " />
</ItemGroup>
```

Introduced in Xamarin.Android 11.2

## AndroidNativeLibrary

[Native libraries](~/android/platform/native-libraries.md)
are added to the build by setting their Build action to
`AndroidNativeLibrary`.

Note that since Android supports multiple Application Binary Interfaces
(ABIs), the build system must know which ABI the native library is
built for. There are two ways this can be done:

1. Path "sniffing".
2. Using the  `Abi` item attribute.

With path sniffing, the parent directory name of the native library is
used to specify the ABI that the library targets. Thus, if you add
`lib/armeabi-v7a/libfoo.so` to the build, then the ABI will be "sniffed" as
`armeabi-v7a`.

### Item Attribute Name

**Abi** &ndash; Specifies the ABI of the native library.

```xml
<ItemGroup>
  <AndroidNativeLibrary Include="path/to/libfoo.so">
    <Abi>armeabi-v7a</Abi>
  </AndroidNativeLibrary>
</ItemGroup>
```

## AndroidResource

All files with an *AndroidResource* build action are compiled into
Android resources during the build process and made accessible via `$(AndroidResgenFile)`.

```xml
<ItemGroup>
  <AndroidResource Include="Resources\values\strings.xml" />
</ItemGroup>
```

More advanced users might perhaps wish to have different resources used in
different configurations but with the same effective path. This can be achieved
by having multiple resource directories and having files with the same relative
paths within these different directories, and using MSBuild conditions to
conditionally include different files in different configurations. For
example:

```xml
<ItemGroup Condition="'$(Configuration)'!='Debug'">
  <AndroidResource Include="Resources\values\strings.xml" />
</ItemGroup>
<ItemGroup  Condition="'$(Configuration)'=='Debug'">
  <AndroidResource Include="Resources-Debug\values\strings.xml"/>
</ItemGroup>
<PropertyGroup>
  <MonoAndroidResourcePrefix>Resources;Resources-Debug<MonoAndroidResourcePrefix>
</PropertyGroup>
```

**LogicalName** &ndash; Specifies the resource path explicitly. Allows
&ldquo;aliasing&rdquo; files so that they will be available as multiple
distinct resource names.

```xml
<ItemGroup Condition="'$(Configuration)'!='Debug'">
  <AndroidResource Include="Resources/values/strings.xml"/>
</ItemGroup>
<ItemGroup Condition="'$(Configuration)'=='Debug'">
  <AndroidResource Include="Resources-Debug/values/strings.xml">
    <LogicalName>values/strings.xml</LogicalName>
  </AndroidResource>
</ItemGroup>
```

## AndroidResourceAnalysisConfig

The Build action `AndroidResourceAnalysisConfig` marks a file as a
severity level configuration file for the Xamarin Android Designer
layout diagnostics tool. This is currently only used in the layout
editor and not for build messages.

See the [Android Resource Analysis
documentation](https://aka.ms/androidresourceanalysis) for more
details.

Added in Xamarin.Android 10.2.

## Content

The normal `Content` Build action is not supported (as we
haven't figured out how to support it without a possibly costly first-run
step).

Starting in Xamarin.Android 5.1, attempting to use the `@(Content)`
Build action will result in a `XA0101` warning.

## LinkDescription

Files with a *LinkDescription* build action are used to
[control linker behavior](~/cross-platform/deploy-test/linker.md).

## ProguardConfiguration

Files with a *ProguardConfiguration* build action contain options which
are used to control `proguard` behavior. For more information about
this build action, see
[ProGuard](~/android/deploy-test/release-prep/proguard.md).

These files are ignored unless the
[`$(EnableProguard)`](~/android/deploy-test/building-apps/build-properties.md#enableproguard)
MSBuild property is `True`.
