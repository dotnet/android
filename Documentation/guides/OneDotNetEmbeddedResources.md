# One .NET: Embedded Resources and `.nupkg` Files

Traditionally, a .NET for Android class library can contain many types
of Android-specific files:

* `@(AndroidAsset)` files in `Assets/`
* `@(AndroidEnvironment)` text files
* `@(AndroidJavaLibrary)` `.jar` files
* `@(AndroidResource)` files in `Resources/`
* `@(EmbeddedNativeLibrary)` files in `lib/[arch]/*.so`

These are packaged in different ways as `EmbeddedResource` files in
the output assembly:

* `@(AndroidEnvironment)`: stored as
  `__AndroidEnvironment__%(Filename)%(Extension)` embedded resources
* `@(AndroidAsset)`, `@(AndroidResource)`: merged into a single
  `__AndroidLibraryProjects__.zip` embedded resource.
  `@(AndroidAsset)` is copied into the `assets` path, and
  `@(AndroidResource)` is copied into the `res` path.
* `@(EmbeddedNativeLibrary)`: merged into a single
  `__AndroidNativeLibraries__.zip` embedded resource.
* `@(AndroidJavaLibrary)`: stored separately as embedded resources.

The problem with this approach, is we have to inspect every assembly
at build time and extract these files to a directory. Because we have
a custom format that Android does not understand, we can't leave the
files as-is. Android command-line tooling like `aapt2` works with
files and directories on disk.

Java binding projects have a slight variation:

* `@(LibraryProjectZip)` can be `.aar` or `.zip` files and is
  repackaged into `__AndroidLibraryProjects__.zip`. You can only have
  one `@(LibraryProjectZip)` per project.
* No support for `@(AndroidResource)` or `@(AndroidAsset)`.
* `@(EmbeddedJar)` and `@(EmbeddedReferenceJar)` are packaged directly
  as `EmbeddedResource` files.

## New Approach in .NET 6

We want to consider an approach that is NuGet-centric. When the above
implementation was written, NuGet was just getting started. Developers
still had the pattern of copying .NET assemblies from `bin` and
committing it to their source control of choice. The single file
approach worked well for this scenario.

If we look at the general structure of a [NuGet package][nuget]:

    lib/
        net6.0-android29/Foo.dll
        net6.0-android30/Foo.dll
    # Optional reference assemblies
    ref/
        net6.0-android29/Foo.dll
        net6.0-android30/Foo.dll
    # Optional native libraries
    runtimes/
        android-arm/native/libFoo.so
        android-arm64/native/libFoo.so
        android-x86/native/libFoo.so
        android-x64/native/libFoo.so

There is not a great place where all Android file types would fit
following this pattern.

In Android Studio, Android libraries are packaged as [`.aar`][aar]
files. A .NET for Android library, `Foo.csproj`, could also generate an
[`.aar`][aar] file in its `$(OutputPath)`:

    Foo.aar
        classes.jar
        res/
        assets/
        libs/*.jar
        jni/[arch]/*.so

Additionally, there is a need for .NET for Android-specific files
within the [`.aar`][aar]. These are placed in a `.net` directory:

    Foo.aar
        .net/__res_name_case_map.txt
        .net/env/MyEnvironment.txt

The `__res_name_case_map.txt` file includes the original casing of
`@(AndroidResource)` files. This is used during `Resource.designer.cs`
generation to ensure that the casing matches the original file for a
layout such as `Resource.Layout.Foo`. It is the same file format
included in `__AndroidLibraryProjects__.zip` in "legacy" Xamarin.Android.

`@(AndroidEnvironment)` files will be placed in `.net/env` so they can
be added to the `@(AndroidEnvironment)` item group for application
projects that consuming the class library.

The name `.net` would be unlikely to collide with anything Google
creates in the `.aar` file format in the future. These folders should
also be completely ignored by Android tooling. The `.net` folder could
also be used for other .NET for Android specific files down the road.

So the output of `Foo.csproj` would look like:

    bin/Debug/$(TargetFramework)/
        Foo.dll
        Foo.aar
    bin/Release/$(TargetFramework)/
        Foo.dll
        Foo.aar

If you ran the `Pack` target, you would get a `.nupkg` file with:

    lib/
        net6.0-android29/Foo.dll
        net6.0-android29/Foo.aar
        net6.0-android30/Foo.dll
        net6.0-android30/Foo.aar

When consuming the `.nupkg` files, .NET for Android will still have to
extract [`.aar`][aar] files on disk for command-line tooling such as
`javac`, `d8/r8` and `manifestmerger`. If users want to copy around
loose build output and consume it, there will only be 1 additional
[`.aar`][aar] file they will need to copy.

### Java/Kotlin Dependencies

A `Foo.csproj` might include `bar.aar` and `baz.jar` files that are
Java/Kotlin dependencies.

`.aar` files should sit alongside the .NET assembly:

    lib
        net6.0-android29/Foo.dll
        net6.0-android29/Foo.aar
        net6.0-android29/bar.aar
        net6.0-android30/Foo.dll
        net6.0-android30/Foo.aar
        net6.0-android30/bar.aar

The `baz.jar` file would be included in the above `Foo.aar` file at:

    Foo.aar
        classes.jar
        libs/baz.jar

### Native Libraries

Since both `.aar` and `.nupkg` files support native libraries,
.NET for Android should support consuming native libraries from both
locations. A .NET for Android class library, `Foo.csproj` will place
native libraries in the `Foo.aar` file by default.

Collisions encountered on the same native library should be ignored.
Applications with large numbers of NuGet packages could have a 
duplicate native library and everything could still work at runtime.

### MSBuild Item Groups

Right now we have a confusing collection of MSBuild item groups:

* `@(InputJar)` - `.jar` file to create a Java binding for, but not
  embed.
* `@(EmbeddedJar)` - `.jar` file to create a Java binding and embed.
* `@(ReferenceJar)` - `.jar` file to reference for the binding
  process.
* `@(EmbeddedReferenceJar)` - `.jar` file to reference and embed, but
  not bind.
* `@(LibraryProjectZip)` - `.aar` or `.zip` file to bind and embed.
  Only one can be used in a project!
* `@(AndroidAarLibrary)` - `.aar` file to include in a application
  project.
* `@(AndroidJavaLibrary)` - `.jar` file to include in a application
  project.
* `@(EmbeddedNativeLibrary)` - `.so` file to embed in a class library
  project or include in an application project.
* `@(AndroidNativeLibrary)` - `.so` file to include in an application
  project.

We could simplify much of the above with a new `@(AndroidLibrary)`
item group. `@(AndroidNativeLibrary)` can be used for
`@(EmbeddedNativeLibrary)` as well:

```xml
<!-- Include and bind -->
<AndroidLibrary Include="foo.aar" />
<!-- Just include, do not bind -->
<AndroidLibrary Include="bar.aar" Bind="false" />
<AndroidLibrary Include="baz.jar" Bind="false" />
<!--
  Bind but do not include in NuGet package.
  %(Pack) is built into NuGet MSBuild targets.
-->
<AndroidLibrary Include="bar.aar" Pack="false" />
<!-- Native libraries need ABI directory or %(Abi) -->
<AndroidNativeLibrary Include="armeabi-v7a\libfoo.so" />
<AndroidNativeLibrary Include="libfoo.so" Abi="x86" />
```

`@(AndroidNativeLibrary)` should remain distinct from
`@(AndroidLibrary)` because the `%(Bind)` and `%(Abi)` metadata do not
really make sense for both native libraries and Java/Kotlin libraries.

The new `@(AndroidLibrary)` item group will simply translate to the
old ones for backwards compatibility. The extension of the file can be
used to determine what kind of library each item is. `%(Bind)` and
`%(Pack)` will both be `true` by default. `%(Pack)` will not do
anything in application projects.

The deprecated item groups will no longer embed, but pack into
`.nupkg` files instead:

* `@(EmbeddedJar)`
* `@(EmbeddedReferenceJar)`

`@(LibraryProjectZip)` will not be supported at all.

## Implementation

Since there is a lot of work here, I've split up the what needs to be
done into different phases.

### "Must Have" for .NET 6 Preview 1

Things we need for the first public release:

* Item group support for `@(AndroidLibrary)` and `%(Bind)=true`.
* Support for packing `.aar` files in `$(OutputPath)` for
  .NET for Android class libraries.
* Support for consuming `.nupkg` files that contain `.aar` files.

These should enable us to compile AndroidX for `net6.0-android`. Then
potentially any other library that depends on AndroidX.

### Next Phase

Things that could happen sometime after .NET 6 Preview 1:

* Support for referencing Android Studio projects in order to build
  them and consume the resulting `.aar` file.

* Support for including Maven dependencies. This might be inclusion of
  a `build.gradle` file that would enable us to run `gradle` and
  download dependencies from Maven repositories.

* Consider not extracting everything in `.aar` files. We can probably
  skip the `assets` and `res` directories and let `aapt2` consume
  `.aar` files directly.

* `<GenerateJavaStubs/>` performance
  * `.aar` files contain `AndroidManifest.xml` files. We could add
    support for developers to add their own manifest to .NET for Android
    class libraries.
  * We could also generate the `AndroidManifest.xml` so attributes
    like `[assembly:UsesPermission]` are done ahead of time.
  * We could generate Java stubs for every `Java.Lang.Object`
  * If all of the above was completed, `<GenerateJavaStubs/>` could
    skip over the assembly completely.

* Build performance could be further improved by included various
  other build-related cache files in the `.aar` file.

It is possible some of these might get pushed out to a future .NET 7
release.

[nuget]: https://docs.microsoft.com/nuget/create-packages/supporting-multiple-target-frameworks
[aar]: https://developer.android.com/studio/projects/android-library#aar-contents
