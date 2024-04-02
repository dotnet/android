# Java Dependency Verification

Note: This feature is only available in .NET 9+.

## Description

A common problem when creating Java binding libraries for .NET Android is not providing the required Java dependencies. The binding process ignores API that requires missing dependencies, so this can result in large portions of desired API not being bound.

Unlike .NET assemblies, a Java library does not specify its dependencies in the package. The dependency information is stored in external files called POM files. In order to consume this information to ensure correct dependencies an additional layer of files must be added to a binding project.

Note: the preferred way of interacting with this system is to use [`<AndroidMavenLibrary>`](AndroidMavenLibrary.md) which will automatically download any needed POM files.

For example:

```xml
<AndroidMavenLibrary Include="com.squareup.okio:okio" Version="1.17.4" />
```

automatically gets expanded to:

```xml
<AndroidLibrary 
  Include="<MavenCacheDir>/Central/com.squareup.okio/okio/1.17.4/com.squareup.okio_okio.jar" 
  Manifest="<MavenCacheDir>/Central/com.squareup.okio/okio/1.17.4/com.squareup.okio_okio.pom"
  JavaArtifact="com.squareup.okio:okio" 
  JavaVersion="1.17.4" />
  
<AndroidAdditionalJavaManifest
  Include="<MavenCacheDir>/Central/com.squareup.okio/okio-parent/1.17.4/okio-parent-1.17.4.pom"
  JavaArtifact="com.squareup.okio:okio-parent"
  JavaVersion="1.17.4" />
  
etc.
```

However it is also possible to manually opt in to Java dependency verification using the build items documented here.

## Specification

To manually opt in to Java dependency verification, add the `Manifest`, `JavaArtifact`, and `JavaVersion` attributes to an `<AndroidLibrary>` item:

```xml
<!-- JavaArtifact format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidLibrary
    Include="my_binding_library.jar"
    Manifest="my_binding_library.pom"
    JavaArtifact="com.example:mybinding"
    JavaVersion="1.0.0" />
</ItemGroup>
```

Building the binding project now should result in verification errors if `my_binding_library.pom` specifies dependencies that are not met.

For example:

```
error : Java dependency 'androidx.collection:collection' version '1.0.0' is not satisfied.
```

Seeing these error(s) or no errors should indicate that the Java dependency verification is working. Follow the [Resolving Java Dependencies](ResolvingJavaDependencies.md) guide to fix any missing dependency errors.

## Additional POM Files

POM files can sometimes have some optional features in use that make them more complicated than the above example.

That is, a POM file can depend on a "parent" POM file:

```xml
<parent>
  <groupId>com.squareup.okio</groupId>
  <artifactId>okio-parent</artifactId>
  <version>1.17.4</version>
</parent>
```

Additionally, a POM file can "import" dependency information from another POM file:

```xml
<dependencyManagement>
  <dependencies>
    <dependency>
      <groupId>com.squareup.okio</groupId>
      <artifactId>okio-bom</artifactId>
      <version>3.0.0</version>
      <type>pom</type>
      <scope>import</scope>
    </dependency>
  </dependencies>
</dependencyManagement>
```

Dependency information cannot be accurately determined without also having access to these additional POM files, and will results in an error like:

```
error : Unable to resolve POM for artifact 'com.squareup.okio:okio-parent:1.17.4'.
```

In this case, we need to provide the POM file for `com.squareup.okio:okio-parent:1.17.4`:

```xml
<!-- JavaArtifact format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidAdditionalJavaManifest
    Include="com.square.okio.okio-parent.1.17.4.pom"
    JavaArtifact="com.squareup.okio:okio-parent"
    JavaVersion="1.17.4" />
</ItemGroup>
```

Note that as "Parent" and "Import" POMs can themselves have parent and imported POMs, this step may need to be repeated until all POM files can be resolved.

Note also that if using `<AndroidMavenLibrary>` this should all be handled automatically.

At this point, if there are dependency errors, follow the [Resolving Java Dependencies](ResolvingJavaDependencies.md) guide to fix any missing dependency errors.