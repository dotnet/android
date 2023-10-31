# AndroidMavenLibrary

Note: This feature is only available in .NET 9+.

## Description

`<AndroidMavenLibrary>` allows a Maven artifact to be specified which will automatically be downloaded and added to a .NET Android binding project. This can be useful to simplify maintenance of .NET Android bindings for artifacts hosted in Maven.

## Specification

 A basic use of `<AndroidMavenLibrary>` looks like:

```xml
<!-- Include format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidMavenLibrary Include="com.squareup.okhttp3:okhttp" Version="4.9.3" />
</ItemGroup>
```

This will do two things at build time:
- Download the Java [artifact](https://central.sonatype.com/artifact/com.squareup.okhttp3/okhttp/4.9.3) with group id `com.squareup.okhttp3`, artifact id `okhttp`, and version `4.9.3` from [Maven Central](https://central.sonatype.com/) to a local cache (if not already cached).
- Add the cached package to the .NET Android bindings build as an [`<AndroidLibrary>`](https://github.com/xamarin/xamarin-android/blob/main/Documentation/guides/building-apps/build-items.md#androidlibrary).

Note that only the requested Java artifact is added to the .NET Android bindings build. Any artifact dependencies are not added. If the requested artifact has dependencies, they must be fulfilled individually.

### Options

`<AndroidMavenLibrary>` defaults to using Maven Central, however it should support any Maven repository that does not require authentication.  This can be controlled with the `Repository` attribute.

Supported values are `Central` (default), `Google`, or a URL to another Maven repository.

```xml
<ItemGroup>
  <AndroidMavenLibrary 
    Include="androidx.core:core" 
    Version="1.9.0" 
    Repository="Google" />
</ItemGroup>
```

```xml
<ItemGroup>
  <AndroidMavenLibrary 
    Include="com.github.chrisbanes:PhotoView" 
    Version="2.3.0" 
    Repository="https://repository.mulesoft.org/nexus/content/repositories/public" />
</ItemGroup>
```

Additionally, any attributes applied to the `<AndroidMavenLibrary>` element will be copied to the `<AndroidLibrary>` it creates internally.  Thus, [attributes](https://github.com/xamarin/xamarin-android/blob/main/Documentation/guides/OneDotNetEmbeddedResources.md#msbuild-item-groups) like `Bind` and `Pack` can be used to control the binding process. (Both default to `true`.)

```xml
<ItemGroup>
  <AndroidMavenLibrary 
    Include="androidx.core:core" 
    Version="1.9.0" 
    Repository="Google"
    Bind="false"
    Pack="false" />
</ItemGroup>
```
