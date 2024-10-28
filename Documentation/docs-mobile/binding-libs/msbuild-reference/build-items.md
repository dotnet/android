---
title: Binding Projects MSBuild Items
description: This guide documents the various MSBuild items available for bindings projects.
ms.author: jopobst
ms.date: 05/08/2024
---

# Binding projects MSBuild items

> [!NOTE]
> In .NET for Android there is technically no distinction between an application and a bindings project, so these items will work in both. In practice it is highly recommended to create separate application and bindings projects. Build items that are primarily used in application projects are documented in the [MSBuild items](../../building-apps/build-items.md) reference guide.

## Build items

| Item | Description |
| - | - |
| `AndroidAdditionalJavaManifest`<br />_Added in .NET 9_ | Represents additional POM files needed to verify Java dependencies.<br /><br />[Documentation](#androidadditionaljavamanifest) |
| `AndroidIgnoredJavaDependency`<br />_Added in .NET 9_ | Represents a Java dependencies that should be ignored when verifying Java dependency.<br /><br />[Documentation](#androidignoredjavadependency) |
| `AndroidJavaSource` | Represents Java source files (`.java`) that should be compiled and included in the project.<br /><br />[Documentation](#androidjavasource) |
| `AndroidLibrary` | Represents a `.jar`/`.aar` file to be bound by the bindings project.<br /><br />[Documentation](#androidlibrary) |
| `AndroidMavenLibrary`<br />_Added in .NET 9_ | Represents a `.jar`/`.aar` file that should be downloaded from a Maven repository and bound by the bindings project.<br /><br />[Documentation](#androidmavenlibrary) |
| `AndroidNamespaceReplacement ` | Represents a transform that should be applied to a Java package name to make the resulting managed namespace better fit .NET conventions.<br /><br />[Documentation](../customizing-bindings/namespace-customization.md) |
| `JavaSourceJar` | Represents a Java **source code** `.jar` that API documentation should be imported from.<br /><br />[Documentation](#javasourcejar) |

## Deprecated build items

These MSBuild items have been deprecated. While they continue to function, it is recommended to migrate to
the listed newer items.

| Item | Description |
| - | - |
| `AndroidAarLibrary`<br />_Deprecated_ | Represents an Android `.aar` file to be included in the project output.<br /><br />[Documentation](#androidaarlibrary) |
| `AndroidJavaLibrary`<br />_Deprecated_ | Represents an Android `.jar` file to be included in the project output.<br /><br />[Documentation](#androidjavalibrary) |
| `EmbeddedJar`<br />_Deprecated_ | Represents an Android `.jar` file to be bound and included in the project output.<br /><br />[Documentation](#embeddedjar) |
| `EmbeddedReferenceJar`<br />_Deprecated_ | Represents an Android `.jar` file to be included in the project output.<br /><br />[Documentation](#embeddedreferencejar) |
| `LibraryProjectZip`<br />_Deprecated_ | Represents an Android `.aar` file to be included in the project output.<br /><br />[Documentation](#libraryprojectzip) |

### AndroidAarLibrary

_This build item is deprecated and is replaced by the **AndroidLibrary** item._

```xml
<!-- Deprecated -->
<AndroidAarLibrary Include="mylib.aar" />

<!-- Recommended -->
<AndroidLibrary Include="mylib.aar" />
```

The Build action of `AndroidAarLibrary` should be used to directly
reference `.aar` files. This build action will be most commonly used
by Xamarin Components. Namely to include references to `.aar` files
that are required to get Google Play and other services working.

Files with this Build action will be treated in a similar fashion to
the embedded resources found in Library projects. The `.aar` will be
extracted into the intermediate directory. Then any assets, resource
and `.jar` files will be included in the appropriate item groups.


### AndroidAdditionalJavaManifest

`<AndroidAdditionalJavaManifest>` is used in conjunction with
[Java Dependency Resolution](../advanced-concepts/java-dependency-verification.md).

It is used to specify additional POM files that will be needed to verify dependencies.
These are often parent or imported POM files referenced by a Java library's POM file.

```xml
<ItemGroup>
  <AndroidAdditionalJavaManifest Include="mylib-parent.pom" JavaArtifact="com.example:mylib-parent" JavaVersion="1.0.0" />
</ItemGroup>
```

| Item metadata name | Description |
| - | - |
| JavaArtifact | Required string. The group and artifact id of the Java library matching the specifed POM file in the form `{GroupId}:{ArtifactId}`. |
| JavaVersion | Required string. The version of the Java library matching the specified POM file. |
  
See the [Java Dependency Resolution documentation](../advanced-concepts/java-dependency-verification.md)
for more details.

Support for this build item was introduced in .NET 9.

### AndroidIgnoredJavaDependency

`<AndroidIgnoredJavaDependency>` is used in conjunction with [Java Dependency Resolution](../advanced-concepts/java-dependency-verification.md).

It is used to specify a Java dependency that should be ignored. This can be
used if a dependency will be fulfilled in a way that Java dependency resolution
cannot detect.

```xml
<!-- Include format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidIgnoredJavaDependency Include="com.google.errorprone:error_prone_annotations" Version="2.15.0" />
</ItemGroup>
```

| Item metadata name | Description |
| - | - |
| Version | Required string. The version of the Java library matching the specified dependency. |

See the [Java Dependency Resolution documentation](../advanced-concepts/java-dependency-verification.md)
for more details.

Support for this build item was introduced in .NET 9.

### AndroidJavaLibrary

_This build item is deprecated and is replaced by the **AndroidLibrary** item._

```xml
<!-- Deprecated -->
<AndroidJavaLibrary Include="mylib.jar" />

<!-- Recommended -->
<AndroidLibrary Include="mylib.jar" />
```

Files with a Build action of `AndroidJavaLibrary` are Java
Archives ( `.jar` files) that will be included in the final Android
package.

### AndroidJavaSource

`AndroidJavaSource` files are Java source code that
will be compiled and included in the final Android package.

Starting with .NET 7, all `**\*.java` files within the project directory
automatically have a Build action of `AndroidJavaSource`, *and* will be
bound prior to the Assembly build.  Allows C# code to easily use
types and members present within the `**\*.java` files.

| Item metadata name | Description |
| - | - |
| Bind | Optional boolean. Specifies whether the Java source file should have a C# binding generated for it. Defaults to `true`. |

Support for this build item was introduced in .NET 7.

### AndroidLibrary

Represents a `.jar`/`.aar` file to be bound and included in the project.

```xml
<ItemGroup>
  <AndroidLibrary Include="foo.jar" />
  <AndroidLibrary Include="bar.aar" />
</ItemGroup>
```

| Item metadata name | Description |
| - | - |
| Bind | Optional boolean. Specifies whether the Java library should have a C# binding generated for it. Defaults to `true`. |
| Pack | Optional boolean. Specifies whether the Java library should be included in the project output. Defaults to `true`. |

### AndroidMavenLibrary

Represents a `.jar`/`.aar` file that should be downloaded from a Maven repository and bound 
by the bindings project.

This can be useful to simplify maintenance of .NET for Android bindings for artifacts 
hosted in Maven.

```xml
<!-- Include format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidMavenLibrary Include="com.squareup.okhttp3:okhttp" Version="4.9.3" />
</ItemGroup>
```

| Item metadata name | Description |
| - | - |
| Version | Required string. The version of the Java library that should be downloaded from Maven. Defaults to `true`. |
| Repository | Optional string. Specifies Maven repository to use. Supported values are `Central`, `Google`, or an `https` URL to a Maven repository. Defaults to `Central`. |
| Bind | Optional boolean. Specifies whether the Java library should have a C# binding generated for it. Defaults to `true`. |
| Pack | Optional boolean. Specifies whether the Java library should be included in the project output. Defaults to `true`. |

See the [AndroidMavenLibrary documentation](../advanced-concepts/android-maven-library.md)
for more details.

Support for this build item was introduced in .NET 9.

### EmbeddedJar

_This build item is deprecated and is replaced by the **AndroidLibrary** item._

```xml
<!-- Deprecated -->
<EmbeddedJar Include="mylib.jar" />

<!-- Recommended -->
<AndroidLibrary Include="mylib.jar" />
```

In a .NET for Android binding project, the **EmbeddedJar** build action
binds the Java/Kotlin library and embeds the `.jar` file into the
library. When a .NET for Android application project consumes the
library, it will have access to the Java/Kotlin APIs from C# as well
as include the Java/Kotlin code in the final Android application.

### EmbeddedReferenceJar

_This build item is deprecated and is replaced by the **AndroidLibrary** item with the **Bind** metadata set to `false`._

```xml
<!-- Deprecated -->
<EmbeddedReferenceJar Include="mylib.jar" />

<!-- Recommended -->
<AndroidLibrary Include="mylib.jar" Bind="false" />
```

In a .NET for Android binding project, the **EmbeddedReferenceJar**
build action embeds the `.jar` file into the library but does not
create a C# binding as [**EmbeddedJar**](#embeddedjar) does. When a
.NET for Android application project consumes the library, it will
include the Java/Kotlin code in the final Android application.

### JavaSourceJar

Represents a Java **source code** `.jar` containing [Javadoc documentation comments](https://www.oracle.com/technical-resources/articles/java/javadoc-tool.html) 
that API documentation should be imported from.

Javadoc will be converted into
[C# XML Documentation Comments](/dotnet/csharp/codedoc)
within the generated binding source code.

[`$(AndroidJavadocVerbosity)`](build-properties.md#androidjavadocverbosity)
controls how "verbose" or "complete" the imported Javadoc is.

| Item metadata name | Description |
| - | - |
| CopyrightFile | Optional string. A path to a file that contains copyright information for the Javadoc contents, which will be appended to all imported documentation. |
| UrlPrefix | Optional string. A URL prefix to support linking to online documentation within imported documentation. |
| UrlStyle | Optional string. The "style" of URLs to generate when linking to online documentation. Only one style is currently supported: `developer.android.com/reference@2020-Nov`. |
| DocRootUrl | Optional string. A URL prefix to use in place of all `{@docroot}` instances in the imported documentation. |

### LibraryProjectZip

_This build item is deprecated and is replaced by the **AndroidLibrary** build item._

```xml
<!-- Deprecated -->
<LibraryProjectZip Include="mylib.aar" />

<!-- Recommended -->
<AndroidLibrary Include="mylib.aar" />
```

The **LibraryProjectZip** build
action binds the Java/Kotlin library and embeds the `.zip` or `.aar`
file into the library. When a .NET for Android application project
consumes the library, it will have access to the Java/Kotlin APIs from
C# as well as include the Java/Kotlin code in the final Android
application.
