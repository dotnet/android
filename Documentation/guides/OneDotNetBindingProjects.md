# .NET 6+ - Xamarin.Android Binding Migration

## Consolidation of binding projects

In .NET 6+, there is no longer a concept of a [binding
project][binding] as a separate project type. Any of the MSBuild item
groups or build actions that currently work in binding projects will
be supported through a .NET 6+ Android application or library.

For example, a binding library would be identical to a class library:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0-android</TargetFramework>
  </PropertyGroup>
</Project>
```

**Note:** It is still recommended that you create separate project(s) for binding your
libraries, just that the project file will look the same as an application.

[binding]: https://docs.microsoft.com/xamarin/android/platform/binding-java-library/

## Unsupported Legacy Options

The following legacy options are no longer supported.  The supported alternatives
have been available for several years, and the smoothest migration option is to update
 and test your current projects with these options **first** before migrating them to .NET 6+.

### `<AndroidClassParser>`

`jar2xml` is no longer a valid option for `<AndroidClassParser>`.  As `class-parse` is
now the only valid option, this setting will no longer affect anything, and `class-parse`
will always be used.

`class-parse` takes advantage of many new modern features not available in `jar2xml`, such as:

* Automatic parameter names for class methods (if your Java code is compiled with `javac -parameters`)
* Kotlin support
* Static/Default interface member (DIM) support
* Java Nullable reference type (NRT) annotations support

### `<AndroidCodegenTarget>`

`XamarinAndroid` is no longer a valid option for `<AndroidCodegenTarget>`.  `XAJavaInterop1`
is now the default and only supported option.

If you have hand-bound code in your `Additions` files that interacts with the generated binding 
plumbing (which is rare), it may need to be updated to be compatible with `XAJavaInterop1`.

## Default file inclusion

File structure:

    Transforms/
        Metadata.xml
    foo.jar

`Transforms\*.xml` files are automatically included as a
`@(TransformFile)` item, and `.jar`/`.aar` files are automatically included
as a `@(AndroidLibrary)` item.

This will bind C# types for the Java types found in `foo.jar` using
the metadata fixups from `Transforms\Metadata.xml`.

Default Android related file globbing behavior is defined in [AutoImport.props][default-items].
This behavior can be disabled for Android items by setting `$(EnableDefaultAndroidItems)` to `false`, or
all default item inclusion behavior can be disabled by setting `$(EnableDefaultItems)` to `false`.

[default-items]: https://github.com/xamarin/xamarin-android/blob/main/src/Xamarin.Android.Build.Tasks/Microsoft.Android.Sdk/Sdk/AutoImport.props

## Embedded `.jar`./`.aar`

In Xamarin.Android Classic, the Java `.jar` or `.aar` was often embedded into the binding `.dll`
as an Embedded Resource.

However, this led to slow builds, as each `.dll` must be opened and scanned for Java code.  If 
found, it must be extracted to disk to be used.

In .NET 6+, Java code is no longer embedded in the `.dll`.  The application build process will 
automatically include any `.jar` or `.aar` files it finds in the same directory as a referenced `.dll`.

If a project references a binding via `<PackageReference>` or `<ProjectReference>` then everything
just works and no additional considerations are needed.

However if a project references a binding via `<Reference>`, the `.jar`/`.aar` must be located next
to the `.dll`.

That is, for a reference like this:

```xml
<Reference Include="MyBinding.dll" />
```

A directory like this will not work:

```
\lib
  - MyBinding.dll
```

The directory must contain the native code as well:

```
\lib
  - MyBinding.dll
  - mybinding.jar
```

## Migration Considerations

There are several new features set by default to help produce new bindings that better match their
Java counterparts.  However, if you are migrating an existing binding project, these features may create
bindings that are not API compatible with your existing released bindings.  In order to maintain
compatibility, you may wish to disable or modify these new features.

### Interface Constants

Traditionally, C# has not allowed constants to be declared in an `interface`, which is a common pattern
in Java code:

```java
public interface Foo {
     public static int BAR = 1;
}
```

This pattern was previously supported by creating an alternative `class` that contains the constant(s):

```csharp
public abstract class Foo : Java.Lang.Object
{
   public static int Bar = 1;
}
```

With C# 8, we can now put these constants on the `interface` just like Java:

```csharp
public interface IFoo {
    public static int Bar = 1;
}
```

However this means we no longer generate the alternative class that existing code may depend on.

Setting `<AndroidBoundInterfacesContainConstants>false</AndroidBoundInterfacesContainConstants>` will revert to the legacy behavior.

### Nested Interface Types

Traditionally, C# has not allowed nested types to be declared in an `interface`, which is allowed
in Java code:

```java
public interface Foo {
     public class Bar { }
}
```

This pattern was supported by moving the nested type to a top-level type with a generated name composed
of the interface and nested type name:

```csharp
public interface IFoo { }

public class IFooBar : Java.Lang.Object { }
```

With C# 8, we can now put these nested type in the `interface` just like Java:

```csharp
public interface IFoo {
  public class Bar : Java.Lang.Object { }
}
```

However this means we no longer generate the top-level class that existing code may depend on.

Setting `<AndroidBoundInterfacesContainTypes>false</AndroidBoundInterfacesContainTypes>` will globally 
revert to the old behavior.

If you wish to use a hybrid approach, for example, to keep existing nested types moved to a top-level
type, but allow any future nested types to remain nested, you can specify this at the `interface` level using
`metadata` to set the `unnest` attribute.

Setting it to `true` will result in "un-nesting" any nested types (legacy behavior):

```xml
<attr path="/api/package[@name='my.package']/interface[@name='Foo']" name="unnest">true</attr>
```

Setting it to `false` will result in nested types remaining nested in the `interface` (.NET 6+ behavior):

```xml
<attr path="/api/package[@name='my.package']/interface[@name='Foo']" name="unnest">false</attr>
```

Using this approach, you could leave `<AndroidBoundInterfacesContainTypes>` as `true` and set `unnest` to
`true` for every `interface` with nested types you have **today**.  These will always remain top-level
types, while any new nested types introduced later will be nested.

### Static and Default Interface Members (DIM)

Traditionally, C# has not allowed interfaces to contain `static` members and `default` methods.

```java
public interface Foo {
  public static void Bar () { ... }
  public default void Baz () { ... }
}
```

Static members on interfaces has been supported by moving them to a sibling `class`:

```csharp
public interface IFoo { }

public class Foo {
  public static void Bar () { ... }
}
```

`default` interface methods have traditionally not been bound, since they are not required and there
wasn't a C# construct to support them.

With C# 8, `static` and `default` members are supported on interfaces, mirroring the Java interface:

```csharp
public interface IFoo {
  public static void Bar () { ... }
  public default void Baz () { ... }
}
```

However this means the alternative sibling `class` containing `static` members will no longer be generated.

Setting `<AndroidBoundInterfacesContainStaticAndDefaultInterfaceMethods>false</AndroidBoundInterfacesContainStaticAndDefaultInterfaceMethods>` will globally 
revert to the old behavior.


### Nullable Reference Types

Support for Nullable Reference Types (NRT) was added in [Xamarin.Android 11.0][11-0-release-notes].

This continues to be enabled/disabled using the same mechanism as all .NET projects:

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

As the default for .NET 6+ is `disable`, the same applies for Xamarin Android projects.

Use `enable` as shown above to enable NRT support.

[11-0-release-notes]: https://docs.microsoft.com/en-us/xamarin/android/release-notes/11/11.0

### `Resource.designer.cs`

In Xamarin.Android, Java binding projects did not support generating a `Resource.designer.cs` file.
Since binding projects are just class libraries in .NET 6+, this file will be generated. This could
be a breaking change when migrating existing projects.

One example of a failure from this change, is if your binding generates a class named `Resource`
in the root namespace:

```
error CS0101: The namespace 'MyBinding' already contains a definition for 'Resource'
```

Or in the case of AndroidX, we have project files with `-` in the name such as
`androidx.window/window-extensions.csproj`. This results in the root namespace `window-extensions`
and invalid C# in `Resource.designer.cs`:

```
error CS0116: A namespace cannot directly contain members such as fields, methods or statements
error CS1514: { expected 
error CS1022: Type or namespace definition, or end-of-file expected
```

To disable `Resource.designer.cs` generation, set `$(AndroidGenerateResourceDesigner)` to `false`
in your `.csproj`:

```xml
<PropertyGroup>
  <AndroidGenerateResourceDesigner>false</AndroidGenerateResourceDesigner>
</PropertyGroup>
```
