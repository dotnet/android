---
title: "Build Process"
description: "This document will provide an overview of the Xamarin.Android build process."
ms.prod: xamarin
ms.assetid: 3BE5EE1E-3FF6-4E95-7C9F-7B443EE3E94C
ms.technology: xamarin-android
author: davidortinau
ms.author: daortin
ms.date: 09/11/2020
---

# Build Process

The Xamarin.Android build process is responsible for gluing everything
together:
[generating `Resource.designer.cs`](~/android/internals/api-design.md),
supporting the
[`@(AndroidAsset)`](~/android/deploy-test/building-apps/build-items.md#androidasset),
[`@(AndroidResource)`](~/android/deploy-test/building-apps/build-items.md#androidresource),
and other [build actions](~/android/deploy-test/building-apps/build-items.md),
generating
[Android-callable wrappers](~/android/platform/java-integration/android-callable-wrappers.md),
and generating a `.apk` for execution on Android devices.

## Application Packages

In broad terms, there are two types of Android application packages
(`.apk` files) which the Xamarin.Android build system can generate:

- **Release** builds, which are fully self-contained and don't
  require additional packages to execute. These are the
  packages which would be provided to an App store.

- **Debug** builds, which are not.

Not coincidentally, these match the MSBuild `Configuration` which
produces the package.

## Shared Runtime

The *shared runtime* is a pair of additional Android packages which
provide the Base Class Library (`mscorlib.dll`, etc.) and the
Android binding library (`Mono.Android.dll`, etc.). Debug builds
rely upon the shared runtime in lieu of including the Base Class Library and
Binding assemblies within the Android application package, allowing the
Debug package to be smaller.

The shared runtime may be disabled in Debug builds by setting the
[`$(AndroidUseSharedRuntime)`](~/android/deploy-test/building-apps/build-properties.md#androidusesharedruntime)
property to `False`.

<a name="Fast_Deployment"></a>

## Fast Deployment

*Fast deployment* works in concert with the shared runtime to further
shrink the Android application package size. This is done by not
bundling the app's assemblies within the package. Instead, they are
copied onto the target via `adb push`. This process speeds up the
build/deploy/debug cycle because if *only* assemblies are changed,
the package is not reinstalled. Instead, only the updated assemblies are
re-synchronized to the target device.

Fast deployment is known to fail on devices which block `adb` from
synchronizing to the directory
`/data/data/@PACKAGE_NAME@/files/.__override__`.

Fast deployment is enabled by default, and may be disabled in Debug builds
by setting the `$(EmbedAssembliesIntoApk)` property to `True`.

## MSBuild Projects

The Xamarin.Android build process is based on MSBuild, which is also
the project file format used by Visual Studio for Mac and Visual Studio.
Ordinarily, users will not need to edit the MSBuild files by hand
&ndash; the IDE creates fully functional projects and updates them with
any changes made, and automatically invoke build targets as needed.

Advanced users may wish to do things not supported by the IDE's GUI, so
the build process is customizable by editing the project file directly.
This page documents only the Xamarin.Android-specific features and
customizations &ndash; many more things are possible with the normal
MSBuild items, properties and targets.

<a name="Build_Targets"></a>

## Binding Projects

The following MSBuild properties are used with
[Binding projects](~/android/platform/binding-java-library/index.md):

- [`$(AndroidClassParser)`](~/android/deploy-test/building-apps/build-properties.md#androidclassparser)
- [`$(AndroidCodegenTarget)`](~/android/deploy-test/building-apps/build-properties.md#androidcodegentarget)

## `Resource.designer.cs` Generation

The Following MSBuild properties are used to control generation of the
`Resource.designer.cs` file:

- [`$(AndroidAapt2CompileExtraArgs)`](~/android/deploy-test/building-apps/build-properties.md#androidaapt2compileextraargs)
- [`$(AndroidAapt2LinkExtraArgs)`](~/android/deploy-test/building-apps/build-properties.md#androidaapt2linkextraargs)
- [`$(AndroidExplicitCrunch)`](~/android/deploy-test/building-apps/build-properties.md#androidexplicitcrunch)
- [`$(AndroidR8IgnoreWarnings)`](~/android/deploy-test/building-apps/build-properties.md#androidr8ignorewarnings)
- [`$(AndroidResgenExtraArgs)`](~/android/deploy-test/building-apps/build-properties.md#androidresgenextraargs)
- [`$(AndroidResgenFile)`](~/android/deploy-test/building-apps/build-properties.md#androidresgenfile)
- [`$(AndroidUseAapt2)`](~/android/deploy-test/building-apps/build-properties.md#androiduseaapt2)
- [`$(MonoAndroidResourcePrefix)`](~/android/deploy-test/building-apps/build-properties.md#monoandroidresourceprefix)

## Signing Properties

Signing properties control how the Application package is signed so
that it may be installed onto an Android device. To allow
quicker build iteration, the Xamarin.Android tasks do not sign packages
during the build process, because signing is quite slow. Instead, they
are signed (if necessary) before installation or during export, by the
IDE or the *Install* build target. Invoking the *SignAndroidPackage*
target will produce a package with the `-Signed.apk` suffix in the
output directory.

By default, the signing target generates a new debug-signing key if
necessary. If you wish to use a specific key, for example on a build
server, the following MSBuild properties are used:

- [`$(AndroidDebugKeyAlgorithm)`](~/android/deploy-test/building-apps/build-properties.md#androiddebugkeyalgorithm)
- [`$(AndroidDebugKeyValidity)`](~/android/deploy-test/building-apps/build-properties.md#androiddebugkeyvalidity)
- [`$(AndroidDebugStoreType)`](~/android/deploy-test/building-apps/build-properties.md#androiddebugstoretype)
- [`$(AndroidKeyStore)`](~/android/deploy-test/building-apps/build-properties.md#androidkeystore)
- [`$(AndroidSigningKeyAlias)`](~/android/deploy-test/building-apps/build-properties.md#androidsigningkeyalias)
- [`$(AndroidSigningKeyPass)`](~/android/deploy-test/building-apps/build-properties.md#androidsigningkeypass)
- [`$(AndroidSigningKeyStore)`](~/android/deploy-test/building-apps/build-properties.md#androidsigningkeystore)
- [`$(AndroidSigningStorePass)`](~/android/deploy-test/building-apps/build-properties.md#androidsigningstorepass)
- [`$(JarsignerTimestampAuthorityCertificateAlias)`](~/android/deploy-test/building-apps/build-properties.md#jarsignertimestampauthoritycertificatealias)
- [`$(JarsignerTimestampAuthorityUrl)`](~/android/deploy-test/building-apps/build-properties.md#jarsignertimestampauthorityurl)

### `keytool` Option Mapping

Consider the following `keytool` invocation:

```shell
$ keytool -genkey -v -keystore filename.keystore -alias keystore.alias -keyalg RSA -keysize 2048 -validity 10000
Enter keystore password: keystore.filename password
Re-enter new password: keystore.filename password
...
Is CN=... correct?
  [no]:  yes

Generating 2,048 bit RSA key pair and self-signed certificate (SHA1withRSA) with a validity of 10,000 days
        for: ...
Enter key password for keystore.alias
        (RETURN if same as keystore password): keystore.alias password
[Storing filename.keystore]
```

To use the keystore generated above, use the property group:

```xml
<PropertyGroup>
    <AndroidKeyStore>True</AndroidKeyStore>
    <AndroidSigningKeyStore>filename.keystore</AndroidSigningKeyStore>
    <AndroidSigningStorePass>keystore.filename password</AndroidSigningStorePass>
    <AndroidSigningKeyAlias>keystore.alias</AndroidSigningKeyAlias>
    <AndroidSigningKeyPass>keystore.alias password</AndroidSigningKeyPass>
</PropertyGroup>
```

## Build Extension Points

The Xamarin.Android build system exposes a few public extension points
for users wanting to hook into our build process. To use one of these
extension points you will need to add your custom target to the
appropriate MSBuild property in a `PropertyGroup`. For example:

```xml
<PropertyGroup>
   <AfterGenerateAndroidManifest>
      $(AfterGenerateAndroidManifest);
      YourTarget;
   </AfterGenerateAndroidManifest>
</PropertyGroup>
```

Extension points include:

- [`$(AfterGenerateAndroidManifest)](~/android/deploy-test/building-apps/build-properties.md#aftergenerateandroidmanifest)
- [`$(BeforeGenerateAndroidManifest)](~/android/deploy-test/building-apps/build-properties.md#beforegenerateandroidmanifest)

A word of caution about extending the build process: If not
written correctly, build extensions can affect your build
performance, especially if they run on every build. It is
highly recommended that you read the MSBuild [documentation](https://docs.microsoft.com/visualstudio/msbuild/msbuild)
before implementing such extensions.

## Target Definitions

The Xamarin.Android-specific parts of the build process are defined in
`$(MSBuildExtensionsPath)\Xamarin\Android\Xamarin.Android.CSharp.targets`,
but normal language-specific targets such as *Microsoft.CSharp.targets*
are also required to build the assembly.

The following build properties must be set before importing any language
targets:

```xml
<PropertyGroup>
  <TargetFrameworkIdentifier>MonoDroid</TargetFrameworkIdentifier>
  <MonoDroidVersion>v1.0</MonoDroidVersion>
  <TargetFrameworkVersion>v2.2</TargetFrameworkVersion>
</PropertyGroup>
```

All of these targets and properties can be included for C# by
importing *Xamarin.Android.CSharp.targets*:

```xml
<Import Project="$(MSBuildExtensionsPath)\Xamarin\Android\Xamarin.Android.CSharp.targets" />
```

This file can easily be adapted for other languages.
