---
title: .NET for Android Build Process
description: .NET for Android Build Process
ms.date: 04/11/2024
---

# Build process

The .NET for Android build process is responsible for gluing everything
together:
[generating `Resource.designer.cs`](/xamarin/android/internals/api-design),
supporting the
[`@(AndroidAsset)`](build-items.md#androidasset),
[`@(AndroidResource)`](build-items.md#androidresource),
and other [build actions](build-items.md),
generating
[Android-callable wrappers](/xamarin/android/platform/java-integration/android-callable-wrappers),
and generating a `.apk` for execution on Android devices.

## Application packages

In broad terms, there are two types of Android application packages
(`.apk` files) which the .NET for Android build system can generate:

- **Release** builds, which are fully self-contained and don't
  require extra packages to execute. These are the
  packages that are provided to an App store.

- **Debug** builds, which are not.

These package types match the MSBuild `Configuration` which
produces the package.

<a name="Fast_Deployment"></a>

## Fast deployment

*Fast deployment* works by further shrinking Android application
package size. This is done by excluding the app's assemblies from the
package, and instead deploying the app's assemblies directly to the
application's internal `files` directory, usually located
in `/data/data/com.some.package`. The internal `files` directory is
not a globally writable folder, so the `run-as` tool is used to execute
all the commands to copy the files into that directory.

This process speeds up the build/deploy/debug cycle because the package
is not reinstalled when *only* assemblies are changed.
Only the updated assemblies are resynchronized to the target device.

> [!WARNING]
> Fast deployment is known to fail on devices which block `run-as`, which often includes devices older than Android 5.0.

Fast deployment is enabled by default, and may be disabled in Debug builds
by setting the `$(EmbedAssembliesIntoApk)` property to `True`.

The [Enhanced Fast Deployment](build-properties.md#androidfastdeploymenttype) mode can
be used in conjunction with this feature to speed up deployments even further.
This will deploy both assemblies, native libraries, typemaps and dexes to the `files`
directory. But you should only really need to enable this if you are changing
native libraries, bindings or Java code.

## MSBuild projects

The .NET for Android build process is based on MSBuild, which is also
the project file format used by Visual Studio for Mac and Visual Studio.
Ordinarily, users will not need to edit the MSBuild files by hand
&ndash; the IDE creates fully functional projects and updates them with
any changes made, and automatically invoke build targets as needed.

Advanced users may wish to do things not supported by the IDE's GUI, so
the build process is customizable by editing the project file directly.
This page documents only the .NET for Android-specific features and
customizations &ndash; many more things are possible with the normal
MSBuild items, properties and targets.

<a name="Build_Targets"></a>

## Binding projects

The following MSBuild properties are used with
[Binding projects](/xamarin/android/platform/binding-java-library):

- [`$(AndroidClassParser)`](build-properties.md#androidclassparser)
- [`$(AndroidCodegenTarget)`](build-properties.md#androidcodegentarget)

## `Resource.designer.cs` Generation

The Following MSBuild properties are used to control generation of the
`Resource.designer.cs` file:

- [`$(AndroidAapt2CompileExtraArgs)`](build-properties.md#androidaapt2compileextraargs)
- [`$(AndroidAapt2LinkExtraArgs)`](build-properties.md#androidaapt2linkextraargs)
- [`$(AndroidExplicitCrunch)`](build-properties.md#androidexplicitcrunch)
- [`$(AndroidR8IgnoreWarnings)`](build-properties.md#androidr8ignorewarnings)
- [`$(AndroidResgenExtraArgs)`](build-properties.md#androidresgenextraargs)
- [`$(AndroidResgenFile)`](build-properties.md#androidresgenfile)
- [`$(MonoAndroidResourcePrefix)`](build-properties.md#monoandroidresourceprefix)

## Signing properties

Signing properties control how the Application package is signed so
that it may be installed onto an Android device. To allow
quicker build iteration, the .NET for Android tasks do not sign packages
during the build process, because signing is quite slow. Instead, they
are signed (if necessary) before installation or during export, by the
IDE or the *Install* build target. Invoking the *SignAndroidPackage*
target will produce a package with the `-Signed.apk` suffix in the
output directory.

By default, the signing target generates a new debug-signing key if
necessary. If you wish to use a specific key, for example on a build
server, the following MSBuild properties are used:

- [`$(AndroidDebugKeyAlgorithm)`](build-properties.md#androiddebugkeyalgorithm)
- [`$(AndroidDebugKeyValidity)`](build-properties.md#androiddebugkeyvalidity)
- [`$(AndroidDebugStoreType)`](build-properties.md#androiddebugstoretype)
- [`$(AndroidKeyStore)`](build-properties.md#androidkeystore)
- [`$(AndroidSigningKeyAlias)`](build-properties.md#androidsigningkeyalias)
- [`$(AndroidSigningKeyPass)`](build-properties.md#androidsigningkeypass)
- [`$(AndroidSigningKeyStore)`](build-properties.md#androidsigningkeystore)
- [`$(AndroidSigningStorePass)`](build-properties.md#androidsigningstorepass)
- [`$(JarsignerTimestampAuthorityCertificateAlias)`](build-properties.md#jarsignertimestampauthoritycertificatealias)
- [`$(JarsignerTimestampAuthorityUrl)`](build-properties.md#jarsignertimestampauthorityurl)

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

## Build extension points

The .NET for Android build system exposes a few public extension points
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

- [`$(AfterGenerateAndroidManifest)](build-properties.md#aftergenerateandroidmanifest)
- [`$(AndroidPrepareForBuildDependsOn)](build-properties.md#androidprepareforbuilddependson)
- [`$(BeforeGenerateAndroidManifest)](build-properties.md#beforegenerateandroidmanifest)
- [`$(BeforeBuildAndroidAssetPacks)`](build-properties.md#beforebuildandroidassetpacks)

A word of caution about extending the build process: If not
written correctly, build extensions can affect your build
performance, especially if they run on every build. It is
highly recommended that you read the MSBuild [documentation](/visualstudio/msbuild/msbuild)
before implementing such extensions.

## Target definitions

The .NET for Android-specific parts of the build process are defined in
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
