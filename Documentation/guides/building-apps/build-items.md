---
title: "Build Items"
description: "This document will list all supported item groups in the Xamarin.Android build process."
ms.prod: xamarin
ms.assetid: 5EBEE1A5-3879-45DD-B1DE-5CD4327C2656
ms.technology: xamarin-android
author: jonpryor
ms.author: jopryo
ms.date: 07/26/2022
---

# Build Items

Build items control how a Xamarin.Android application
or library project is built.

## AndroidAsset

Supports [Android Assets](https://developer.android.com/guide/topics/resources/providing-resources#OriginalFiles),
files that would be included in the `assets` folder in a Java Android project.

## AndroidAarLibrary

The Build action of `AndroidAarLibrary` should be used to directly
reference `.aar` files. This build action will be most commonly used
by Xamarin Components. Namely to include references to `.aar` files
that are required to get Google Play and other services working.

Files with this Build action will be treated in a similar fashion to
the embedded resources found in Library projects. The `.aar` will be
extracted into the intermediate directory. Then any assets, resource
and `.jar` files will be included in the appropriate item groups.

## AndroidAotProfile

Used to provide an AOT profile, for use with profile-guided AOT.

It can be also used from Visual Studio by setting the `AndroidAotProfile`
build action to a file containing an AOT profile.

## AndroidAppBundleMetaDataFile

Specifies a file that will be included as metadata in the Android App Bundle.
The format of the flag value is `<bundle-path>:<physical-file>` where
`bundle-path` denotes the file location inside the App Bundle's metadata
directory, and `physical-file` is an existing file containing the raw data
to be stored.

```xml
<ItemGroup>
  <AndroidAppBundleMetaDataFile
    Include="com.android.tools.build.obfuscation/proguard.map:$(OutputPath)mapping.txt"
  />
</ItemGroup>
```

See [bundletool](https://developer.android.com/studio/build/building-cmdline#build_your_app_bundle_using_bundletool) documentation for more details.

Added in Xamarin.Android 12.3.

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

## AndroidJavaLibrary

Files with a Build action of `AndroidJavaLibrary` are Java
Archives ( `.jar` files) that will be included in the final Android
package.

## AndroidJavaSource

Files with a Build action of `AndroidJavaSource` are Java source code that
will be included in the final Android package.

Starting with .NET 7, all `**\*.java` files within the project directory
automatically have a Build action of `AndroidJavaSource`, *and* will be
bound prior to the Assembly build.  Allows C# code to easily use
types and members present within the `**\*.java` files.

Set `%(AndroidJavaSource.Bind)` to False to disable this behavior.

## AndroidLibrary

**AndroidLibrary** is a new build action for simplifying how
`.jar` and `.aar` files are included in projects.

Any project can specify:

```xml
<ItemGroup>
  <AndroidLibrary Include="foo.jar" />
  <AndroidLibrary Include="bar.aar" />
</ItemGroup>
```

The result of the above code snippet has a different effect for each
Xamarin.Android project type:

* Application and class library projects:
  * `foo.jar` maps to [**AndroidJavaLibrary**](#androidjavalibrary).
  * `bar.aar` maps to [**AndroidAarLibrary**](#androidaarlibrary).
* Java binding projects:
  * `foo.jar` maps to [**EmbeddedJar**](#embeddedjar).
  * `foo.jar` maps to [**EmbeddedReferenceJar**](#embeddedreferencejar)
    if `Bind="false"` metadata is added.
  * `bar.aar` maps to [**LibraryProjectZip**](#libraryprojectzip).

This simplification means you can use **AndroidLibrary** everywhere.

This build action was added in Xamarin.Android 11.2.

## AndroidLintConfig

The Build action 'AndroidLintConfig' should be used in conjunction with the
[`$(AndroidLintEnabled)`](~/android/deploy-test/building-apps/build-properties.md#androidlintenabled)
property. Files with this build action will be merged together and passed to the
android `lint` tooling. They should be XML files containing information on
tests to enable and disable.

See the [lint documentation](https://developer.android.com/studio/write/lint)
for more details.

## AndroidManifestOverlay

The `AndroidManifestOverlay` build action can be used to provide
`AndroidManifest.xml` files to the [Manifest Merger](https://developer.android.com/studio/build/manifest-merge) tool.
Files with this build action will be passed to the Manifest Merger along with
the main `AndroidManifest.xml` file and manifest files from
references. These will then be merged into the final manifest.

You can use this build action to provide changes and settings to
your app depending on your build configuration. For example, if you need to
have a specific permission only while debugging, you can use the overlay to
inject that permission when debugging. For example, given the following
overlay file contents:

```
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <uses-permission android:name="android.permission.CAMERA" />
</manifest>
```

You can use the following to add a manifest overlay for a debug build:

```
<ItemGroup>
  <AndroidManifestOverlay Include="DebugPermissions.xml" Condition=" '$(Configuration)' == 'Debug' " />
</ItemGroup>
```

This build action was introduced in Xamarin.Android 11.2.

## AndroidInstallModules

Specifies the modules that get installed by **bundletool** command when
installing app bundles.

This build action was introduced in Xamarin.Android 11.3.

## AndroidKnownDesignerAssemblies

This is an ItemGroup that is used to work around issues when upgrading the old `Resource.designer.cs`
system to the new Resource Assembly system. This can happen if an assembly is referencing a 
`Resource.designer.cs` class from anther assembly which was built on an older framework. 
If you see runtime errors such as 

```
System.TypeLoadException: 'Could not resolve type with token 010001d8 from typeref (expected class 'Style' in assembly '')'
```

You can make use of this ItemGroup to work around these issues.

You need to figure out which assembly is missing the designer and add its name to this 
ItemGroup 

```
<AndroidKnownDesignerAssemblies Include="Some.Assembly" />
```

This will enable some additional processing to ensure the IL is fixed.

This build item was introduced in .NET 8.


## AndroidNativeLibrary

[Native libraries](~/android/platform/native-libraries.md)
are added to the build by setting their Build action to
`AndroidNativeLibrary`.

Note that since Android supports multiple Application Binary Interfaces
(ABIs), the build system must know the ABI the native library is
built for. There are two ways the ABI can be specified:

1. Path "sniffing".
2. Using the  `%(Abi)` item metadata.

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

## AndroidPackagingOptionsExclude

A set of file glob compatible items which will allow for items to be
excluded from the final package. The default values are as follows

```
<ItemGroup>
	<AndroidPackagingOptionsExclude Include="DebugProbesKt.bin" />
	<AndroidPackagingOptionsExclude Include="$([MSBuild]::Escape('*.kotlin_*')" />
</ItemGroup>
```
Items can use file blob characters for wildcards such as `*` and `?`.
However these Items MUST use URL encoding or '$([MSBuild]::Escape(''))'.
This is so MSBuild does not try to interpret them as actual file wildcards.

NOTE: `*`, `?` and `.` will be replaced in the `BuildApk` task with the
appropriate RegEx expressions.

Added in Xamarin.Android 13.1 and .NET 7.

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
  <MonoAndroidResourcePrefix>Resources;Resources-Debug</MonoAndroidResourcePrefix>
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
documentation](../../user-interface/android-designer/diagnostics.md) for more
details.

Added in Xamarin.Android 10.2.

## Content

The normal `Content` Build action is not supported (as we
haven't figured out how to support it without a possibly costly first-run
step).

Starting in Xamarin.Android 5.1, attempting to use the `@(Content)`
Build action will result in a `XA0101` warning.

## EmbeddedJar

In a Xamarin.Android binding project, the **EmbeddedJar** build action
binds the Java/Kotlin library and embeds the `.jar` file into the
library. When a Xamarin.Android application project consumes the
library, it will have access to the Java/Kotlin APIs from C# as well
as include the Java/Kotlin code in the final Android application.

Since Xamarin.Android 11.2, you can use the
[**AndroidLibrary**](#androidlibrary) build action as an alternative
such as:

```xml
<Project>
  <ItemGroup>
    <AndroidLibrary Include="Library.jar" />
  </ItemGroup>
</Project>
```

## EmbeddedNativeLibrary

In a Xamarin.Android class library or Java binding project, the
**EmbeddedNativeLibrary** build action bundles a native library such
as `lib/armeabi-v7a/libfoo.so` into the library. When a
Xamarin.Android application consumes the library, the `libfoo.so` file
will be included in the final Android application.

Since Xamarin.Android 11.2, you can use the
[**AndroidNativeLibrary**](#androidnativelibrary) build action as an
alternative.

## EmbeddedReferenceJar

In a Xamarin.Android binding project, the **EmbeddedReferenceJar**
build action embeds the `.jar` file into the library but does not
create a C# binding as [**EmbeddedJar**](#embeddedjar) does. When a
Xamarin.Android application project consumes the library, it will
include the Java/Kotlin code in the final Android application.

Since Xamarin.Android 11.2, you can use the
[**AndroidLibrary**](#androidlibrary) build action as an alternative
such as `<AndroidLibrary Include="..." Bind="false" />`:

```xml
<Project>
  <ItemGroup>
    <!-- A .jar file to bind & embed -->
    <AndroidLibrary Include="Library.jar" />
    <!-- A .jar file to only embed -->
    <AndroidLibrary Include="Dependency.jar" Bind="false" />
  </ItemGroup>
</Project>
```

## JavaDocJar

In a Xamarin.Android binding project, the **JavaDocJar** build action
is used on `.jar` files that contain *Javadoc HTML*.  The Javadoc HTML
is parsed in order to extract parameter names.

Only certain "Javadoc HTML variations" are supported, including:

  * JDK 1.7 `javadoc` output.
  * JDK 1.8 `javadoc` output.
  * Droiddoc output.

This build action is deprecated in Xamarin.Android 11.3, and will not be
supported in .NET 6.
The `@(JavaSourceJar)` build action is preferred.

## JavaSourceJar

In a Xamarin.Android binding project, the **JavaSourceJar** build action
is used on `.jar` files that contain *Java source code*, that contain
[Javadoc documentation comments](https://www.oracle.com/technical-resources/articles/java/javadoc-tool.html).

Prior to Xamarin.Android 11.3, the Javadoc would be converted into HTML
via the `javadoc` utility during build time, and later turned into
XML documentation.

Starting with Xamarin.Android 11.3, Javadoc will instead be converted into
[C# XML Documentation Comments](/dotnet/csharp/codedoc)
within the generated binding source code.

`$(AndroidJavadocVerbosity)` controls how "verbose" or "complete" the imported Javadoc is.

Starting in Xamarin.Android 11.3, the following MSBuild metadata is supported:

* `%(CopyrightFile)`: A path to a file that contains copyright
    information for the Javadoc contents, which will be appended to
    all imported documentation.

* `%(UrlPrefix)`: A URL prefix to support linking to online
    documentation within imported documentation.

* `%(UrlStyle)`: The "style" of URLs to generate when linking to
    online documentation.  Only one style is currently supported:
    `developer.android.com/reference@2020-Nov`.

Starting in Xamarin.Android 12.3, the following MSBuild metadata is supported:

* `%(DocRootUrl)`: A URL prefix to use in place of all {@docroot}
    instances in the imported documentation.


## LibraryProjectZip

In a Xamarin.Android binding project, the **LibraryProjectZip** build
action binds the Java/Kotlin library and embeds the `.zip` or `.aar`
file into the library. When a Xamarin.Android application project
consumes the library, it will have access to the Java/Kotlin APIs from
C# as well as include the Java/Kotlin code in the final Android
application.

> [!NOTE]
> Only a single **LibraryProjectZip** can be included in a
> Xamarin.Android binding project. This limitation will be removed
> in .NET 6.

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
