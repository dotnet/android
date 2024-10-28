---
title: Binding a Java Library
description: Bind an existing Java library to use it from a .NET for Android application.
ms.author: jopobst
ms.date: 05/07/2024
---

# Binding a Java library

The Android community offers many Java libraries that you may want to
use in your app. These Java libraries are often packaged in .JAR (Java
Archive) or .AAR (Android Archive) format, but you can package a .JAR/.AAR in a *Java Bindings
Library* so that its functionality is available to .NET for Android
applications. The purpose of the Java Bindings library is to make the APIs in
the .JAR/.AAR file available to C# code through automatically-generated code
wrappers.

> [!TIP]
> .NET 9 introduces support for automatically downloading and binding a Java library from a Maven repository. See the [Binding a Java Library from Maven](binding-java-maven-library.md) documentation to simplify this scenario.

## Walkthrough

In this walkthrough, we are going to bind version `3.1.0` of CircleImageView, a library that displays an image in a circular canvas.

From the [Maven repository](https://mvnrepository.com/artifact/de.hdodenhof/circleimageview/3.1.0), 
download `circleimageview-3.1.0.aar` locally to be bound.

### Creating the bindings library

First, create a new Bindings Library project. This can be done with the "Android Java Binding Library" project
template available in Visual Studio or via the `dotnet` command line with:

```dotnetcli
dotnet new android-bindinglib
```

Copy the `circleimageview-3.1.0.aar` file into the project directory.

Like [.NET SDK style projects](/dotnet/core/project-sdk/overview#default-includes-and-excludes), 
.NET for Android binding projects automatically include any .JAR/.AAR files in the project directory
as an `AndroidLibrary` type file, so no additional configuration is needed.

Now build the project using Visual Studio's Build command, or from the command line:

```dotnetcli
dotnet build
```

This Java library has now been bound and it ready to be referenced by a .NET for Android application project
or published to NuGet for public consumption.

## Additional options

### Skip managed bindings

By default, C# bindings will be created for any .JAR/.AAR placed in the project. However C# bindings can be
tricky to create and are not necessary if you do not intend to call the Java API from C#.

This is especially the case when the Java library is simply a dependency of another Java library and does
not need to be called from C# directly.  In this case, the `Bind="false"` attribute can be used to only
include the Java dependency but not bind it.

```xml
<ItemGroup>
  <AndroidLibrary Update="circleimageview-3.1.0.aar" Bind="false" />
</ItemGroup>
```

Note if using automatic imports you will need to use `Update` to change the automatically imported file
instead of adding an additional copy with `Include`.

### Java dependencies

A Java library may depend on other Java libraries which will be required to be packaged with your application
in order for your application to work. This information is traditionally provided in a .POM file, and it
is your responsibility to ensure that any needed dependencies are properly referenced, usually via a NuGet 
package or by bundling the needed .JAR/.AAR files in your project.

In .NET 9, the Java Dependency Verification feature was added. By providing the .POM file, the binding tooling can
help ensure you have met all required Java dependencies.

To enable Java Dependency Verification for our walkthrough, ensure you are using .NET 9+ and your project
targets `net9.0-android` or greater.

From the [Maven repository](https://mvnrepository.com/artifact/de.hdodenhof/circleimageview/3.1.0), 
download `circleimageview-3.1.0.pom` locally and place it in your project folder. Note that .POM files will
not get detected automatically because they need to be associated with the correct .JAR/.AAR.

Update the automatically imported `AndroidLibrary` to specify the location of the .POM file with `Manifest` 
attribute.  Additionally, specify the `JavaArtifact` and `JavaVersion` of the Java library:

```xml
<!-- JavaArtifact format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidLibrary
    Update="circleimageview-3.1.0.aar"
    Manifest="circleimageview-3.1.0.pom"
    JavaArtifact="de.hdodenhof:circleimageview"
    JavaVersion="3.1.0" />
</ItemGroup>
```

This library is trivial and does not have any Java dependencies, but if did and they were unmet, error like
this would be emitted:

```dotnetcli
error XA4241: Java dependency 'androidx.collection:collection:1.0.0' is not satisfied.
error XA4242: Java dependency 'org.jetbrains.kotlin:kotlin-stdlib:1.9.0' is not satisfied. Microsoft maintains the NuGet package 'Xamarin.Kotlin.StdLib' that could fulfill this dependency.
```

Additional information on configuring Java Dependency Verification and how to satisfy dependencies can
by found in the [documentation](../advanced-concepts/java-dependency-verification.md).

## Next steps

- **Customizing Bindings with Metadata** - The Java library bound in the walkthrough is trivial and the
binding tooling was able to fully automatically convert it to a C# API. Unfortunately this is often not
the case and there will often be compile errors. These errors must be fixed with "metadata" to manually 
instruct the binding tooling how to resolve differences between the Java and C# languages.

- **Changing Namespaces** - The types in the walkthrough end up in the .NET namespace `DE.Hdodenhof.Circleimageview`.
Java package names tend to be more verbose than .NET namespaces and it may be more desirable to change it, for example to
`CircleImageViewLibrary` using an `AndroidNamespaceReplacement`:

```xml
<ItemGroup>
  <AndroidNamespaceReplacement Include='DE.Hdodenhof.Circleimageview' Replacement='CircleImageViewLibrary' />
</ItemGroup>

```