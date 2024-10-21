---
title: Binding a Java Library from Maven
description: <AndroidMavenLibrary> allows a Maven artifact to be specified which will automatically be downloaded and added to a .NET for Android binding project. This can be useful to simplify maintenance of .NET for Android bindings for artifacts hosted in Maven.
ms.author: jopobst
ms.date: 05/11/2024
---

# Binding a Java library from Maven

A common binding scenario is binding a Java library hosted in a Maven repository (like [Maven Central](https://central.sonatype.com/)).

.NET 9 introduces support for automatically downloading a Java library from a Maven repository and
verifying its dependencies to help make this scenario easier and more accurate.

> [!TIP]
> If using a .NET version before .NET 9 or binding a Java library that isn't from Maven, see the [Binding a Java Library](binding-java-library.md) documentation.

## Walkthrough

In this walkthrough, we are going to bind version `3.1.0` of CircleImageView, a library that displays an image in a circular canvas.

From the [Maven repository](https://mvnrepository.com/artifact/de.hdodenhof/circleimageview/3.1.0), we can see the following
identifiers for this library which will be needed later:

```xml
<dependency>
    <groupId>de.hdodenhof</groupId>
    <artifactId>circleimageview</artifactId>
    <version>3.1.0</version>
</dependency>
```

### Creating the bindings library

First, create a new Bindings Library project. This can be done with the "Android Java Binding Library" project
template available in Visual Studio or via the `dotnet` command line with:

```dotnetcli
dotnet new android-bindinglib
```

Open the project file (`.csproj`) created by the template. We'll add an `AndroidMavenLibrary` element inside an
`ItemGroup` to specify the Java library we want to bind:

```xml
<!-- Include format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidMavenLibrary Include="de.hdodenhof:circleimageview" Version="3.1.0" />
</ItemGroup>
```

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
  <AndroidMavenLibrary Include="de.hdodenhof:circleimageview" Version="3.1.0" Bind="false" />
</ItemGroup>
```

## Next steps

- **`AndroidMavenLibrary` Options** - The walkthrough library was automatically downloaded from Maven
Central which is the default repository. Other Maven repositories and options can be specified.

- **Java Dependency Verification** - The Java library bound in the walkthrough is trivial and did not depend
on any other Java packages. Most libraries will depend on other Java packages and errors will be surfaced to
ensure these dependencies can be resolved.

These errors must be fixed before the binding can build, and look like:

```dotnetcli
error XA4241: Java dependency 'androidx.collection:collection:1.0.0' is not satisfied.
error XA4242: Java dependency 'org.jetbrains.kotlin:kotlin-stdlib:1.9.0' is not satisfied. Microsoft maintains the NuGet package 'Xamarin.Kotlin.StdLib' that could fulfill this dependency.
```

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