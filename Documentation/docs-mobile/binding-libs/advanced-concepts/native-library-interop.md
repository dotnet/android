---
title: AndroidMavenLibrary Build Action .NET for Android
description: AndroidMavenLibrary Build Action .NET for Android
ms.date: 10/21/2024
---
# NativeLibraryInterop

## Overview
Native Library Interop (formerly referred to as the "Slim Binding" approach), refers to a
pattern for accessing native SDKs in .NET for Android and .NET MAUI projects.

Starting in .NET 9, the .NET for Android SDK supports building Gradle projects
by using the `@(AndroidGradleProject)` build action. This is declared in
an MSBuild ItemGroup in a project file:

```xml
<ItemGroup>
  <AndroidGradleProject Include="path/to/project/build.gradle.kts" ModuleName="mylibrary" />
</ItemGroup>
```

When an `@(AndroidGradleProject)` item is added to a .NET for Android project, the build process
will attempt to create an AAR or APK file from the specified Gradle project. Any AAR output files
will be added to the .NET project as an `@(AndroidLibrary)` to be bound.

Please see the [build-items](../../building-apps/build-items.md) docs for more information about
the `@(AndroidGradleProject)` build action.

Additional documentation and references can be found below:

* https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop
* https://github.com/CommunityToolkit/Maui.NativeLibraryInterop
