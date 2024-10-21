---
title: Resolving Java Dependencies in .NET for Android
description: Resolving Java Dependencies in .NET for Android
ms.author: jopobst
ms.date: 05/11/2024
---

# Resolving Java dependencies

> [!NOTE]
> This feature is only available in .NET 9+.

Once Java dependency verification has been enabled for a bindings project, either automatically via `<AndroidMavenLibrary>` or manually via `<AndroidLibrary>`, there may be errors to resolve, such as:

```
error : Java dependency 'androidx.collection:collection' version '1.0.0' is not satisfied.
```

These dependencies can be fulfilled in many different ways.

## `<PackageReference>`

In the best case scenario, there is already an existing binding of the Java dependency available on NuGet.org. This package may be provided by Microsoft or the .NET community. Packages maintained by Microsoft may be surfaced in the error message like this:

```
error : Java dependency 'androidx.collection:collection' version '1.0.0' is not satisfied. Microsoft maintains the NuGet package 'Xamarin.AndroidX.Collection' that could fulfill this dependency.
```

Adding the `Xamarin.AndroidX.Collection` package to the project should automatically resolve this error, as the package provides metadata to advertise that it provides the `androidx.collection:collection` dependency. This is done by looking for a specially crafted NuGet tag.  For example, for the AndroidX Collection library, the tag looks like this:

```xml
<!-- artifact={GroupId}:{ArtifactId}:{Java Library Version} -->
<PackageTags>artifact=androidx.collection:collection:1.0.0</PackageTags>
```

However there may be NuGet packages which fulfill a dependency but have not had this metadata added to it.  In this case, you will need to explicitly specify which dependency the package contains with `JavaArtifact`:

```xml
<PackageReference 
  Include="Xamarin.Kotlin.StdLib" 
  Version="1.7.10" 
  JavaArtifact="org.jetbrains.kotlin:kotlin-stdlib:1.7.10" />
```

With this, the binding process knows the Java dependency is satisfied by the NuGet package.

> [!NOTE]
> NuGet packages specify their own dependencies, so you will not need to worry about transitive dependencies.

## `<ProjectReference>`

If the needed Java dependency is provided by another project in your solution, you can annotate the `<ProjectReference>` to specify the dependency it fulfills:

```xml
<ProjectReference 
  Include="..\My.Other.Binding\My.Other.Binding.csproj" 
  JavaArtifact="my.other.binding:helperlib:1.0.0" />
```

With this, the binding process knows the Java dependency is satisfied by the referenced project.

> [!NOTE]
> Each project specifies their own dependencies, so you will not need to worry about transitive dependencies.

## `<AndroidLibrary>`

If you are creating a public NuGet package, you will want to follow NuGet's "one library per package" policy so that the NuGet dependency graph works.  However, if creating a binding for private use, you may want to include your Java dependencies directly inside the parent binding.

This can be done by adding additional `<AndroidLibrary>` items to the project:

```xml
<ItemGroup>
  <AndroidLibrary Include="mydependency.jar" JavaArtifact="my.library:dependency-library:1.0.0" />
</ItemGroup>
```

To include the Java library but not produce C# bindings for it, mark it with `Bind="false"`:

```xml
<ItemGroup>
  <AndroidLibrary Include="mydependency.jar" JavaArtifact="my.library:dependency-library:1.0.0" Bind="false" />
</ItemGroup>
```

Alternatively, `<AndroidMavenLibrary>` can be used to retrieve a Java library from a Maven repository:

```xml
<ItemGroup>
  <AndroidMavenLibrary Include="my.library:dependency-library" Version="1.0.0" />
  <!-- or, if the Java library doesn't need to be bound -->
  <AndroidMavenLibrary Include="my.library:dependency-library" Version="1.0.0" Bind="false" />
</ItemGroup>
```

> [!NOTE]
> If the dependency library has its own dependencies, you will be required to ensure they are fulfilled.

## `<AndroidIgnoredJavaDependency>`

As a last resort, a needed Java dependency can be ignored. An example of when this is useful is if the dependency library is a collection of Java annotations that are only used at compile type and not runtime.

Note that while the error message will go away, it does not mean the package will magically work. If the dependency is actually needed at runtime and not provided the Android application will crash with a `Java.Lang.NoClassDefFoundError` error.

```xml
<ItemGroup>
  <AndroidIgnoredJavaDependency Include="com.google.errorprone:error_prone_annotations:2.15.0" />
</ItemGroup>
```

> [!NOTE]
> Any usage of `JavaArtifact` can specify multiple artifacts by delimiting them with a comma or semicolon.
