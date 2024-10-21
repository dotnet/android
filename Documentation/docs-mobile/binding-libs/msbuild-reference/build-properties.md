---
title: Binding Projects MSBuild Properties
description: This guide documents the various MSBuild properties available for bindings projects.
ms.author: jopobst
ms.date: 05/08/2024
---

# Binding projects MSBuild properties

> [!NOTE]
> In .NET for Android there is technically no distinction between an application and a bindings project, so these properties will work in both. In practice it is highly recommended to create separate application and bindings projects. Build properties that are primarily used in application projects are documented in the [MSBuild properties](../../building-apps/build-properties.md) reference guide.

## Build properties

| Property | Default | Description |
|----------------------------------|------------------------| - | - |
| `AndroidBoundInterfacesContainConstants` | `true` | A boolean property that specifies if binding constants on interfaces will be supported, or if the legacy workaround of creating an `IMyInterfaceConsts` class will be used.<br /><br />[Documentation](#androidboundinterfacescontainconstants) |
| `AndroidBoundInterfacesContainStatic`<br />`AndDefaultInterfaceMethods` | `true` | A boolean property that specifies if default and static members on interfaces will be supported, or the legacy workaround of creating a sibling class containing static members like `abstract class MyInterface` will be used.<br /><br />[Documentation](#androidboundinterfacescontainstaticanddefaultinterfacemethods) |
| `AndroidBoundInterfacesContainTypes` | `true` | A boolean property that specifies if types nested in interfaces will be supported, or the legacy workaround of creating a non-nested type like `IMyInterfaceMyNestedClass` will be used.<br /><br />[Documentation](#androidboundinterfacescontaintypes) |
| `AndroidEnableObsoleteOverrideInheritance`<br />_Added in .NET 8_ | `true` | An boolean property that specifies if bound methods that override `@Deprecated` Java methods are automatically marked as `@Deprecated`.<br /><br />[Documentation](#androidenableobsoleteoverrideinheritance)|
| `AndroidEnableRestrictToAttributes`<br />_Added in .NET 8_ | `obsolete` | An enum-style property with valid values of `obsolete` and `disable` that specifies if the .NET `[Obsolete]` attribute is added to bound API that is marked with `@RestrictTo` in a Java library.<br /><br />[Documentation](#androidenablerestricttoattributes)|
| `AndroidJavadocVerbosity` | `intellisense` | An enum-style property with valid values `intellisense` and `full` that specifies how "verbose" [C# XML Documentation Comments](/dotnet/csharp/codedoc) should be when importing Javadoc documentation within binding projects using the [`@(JavaSourceJar)`](build-items.md#javasourcejar) build action.<br /><br />[Documentation](#androidjavadocverbosity)|

### AndroidBoundInterfacesContainConstants

A boolean property that specifies if binding constants on interfaces will be supported, or if the legacy workaround of creating an `IMyInterfaceConsts` class will be used.

The default value is `true`.

This is only recommended if trying to maintain public API compatibility with a legacy bindings library created 
before C# 8 was released.

[More details](/dotnet/maui/migration/android-binding-projects#interface-constants)

### AndroidBoundInterfacesContainStaticAndDefaultInterfaceMethods

A boolean property that specifies if default and static members on interfaces will be supported, or if the legacy workaround of creating a sibling class containing static members like `abstract class MyInterface` will be used.

The default value is `true`.

This is only recommended if trying to maintain public API compatibility with a legacy bindings library created 
before C# 8 was released.

[More details](/dotnet/maui/migration/android-binding-projects#nested-interface-types)

### AndroidBoundInterfacesContainTypes

A boolean property that specifies if types nested in interfaces will be supported, or if the legacy workaround of creating a non-nested type like `IMyInterfaceMyNestedClass` will be used.

The default value is `true`.

This is only recommended if trying to maintain public API compatibility with a legacy bindings library created 
before C# 8 was released.

[More details](/dotnet/maui/migration/android-binding-projects#static-and-default-interface-members-dim)

### AndroidEnableRestrictToAttributes

An enum-style property with valid values of `obsolete` and `disable` that controls if the .NET `[Obsolete]` 
attribute is added to bound API that is marked with `@RestrictTo` in a Java library.

This property is set to `obsolete` by default.

When set to `obsolete`, types and members that are marked with the Java annotation 
`androidx.annotation.RestrictTo` *or* are in non-exported Java packages will 
be marked with an `[Obsolete]` attribute in the C# binding.

This `[Obsolete]` attribute has a descriptive message explaining that the
Java package owner considers the API to be "internal" and warns against its use.

This attribute also has a custom warning code `XAOBS001` so that it can be suppressed
independently of "normal" obsolete API.

When set to `disable`, API will be generated as normal with no additional
attributes. (This is the same behavior as before .NET 8.)

Adding `[Obsolete]` attributes instead of automatically removing the API was done to 
preserve API compatibility with existing packages. If you would instead prefer to 
*remove* members that have the `@RestrictTo` annotation *or* are in non-exported 
Java packages, you can use [metadata](../customizing-bindings/java-bindings-metadata.md#metadataxml-transform-file) in addition to
this property to prevent these types from being bound:

```xml
<remove-node path="//*[@annotated-visibility]" />
```

Support for this property was added in .NET 8.

### AndroidEnableObsoleteOverrideInheritance

A boolean property that specifies if bound methods that override `@Deprecated` Java methods are automatically marked as `@Deprecated`.

It is extremely rare to need to change this property.

Support for this property was added in .NET 8.

### AndroidJavadocVerbosity

An enum-style property with valid values `intellisense` and `full` that specifies how "verbose" [C# XML Documentation Comments](/dotnet/csharp/codedoc) should be when importing Javadoc documentation within binding projects using the [`@(JavaSourceJar)`](build-items.md#javasourcejar) build action.

This property is set to `intellisense` by default.

  * `intellisense`: Only emit the XML comments:
    [`<exception/>`](/dotnet/csharp/codedoc#exception),
    [`<param/>`](/dotnet/csharp/codedoc#param),
    [`<returns/>`](/dotnet/csharp/codedoc#returns),
    [`<summary/>`](/dotnet/csharp/codedoc#summary).
  * `full`: Emit listed `intellisense` elements, as well as
    [`<remarks/>`](/dotnet/csharp/codedoc#remarks),
    [`<seealso/>`](/dotnet/csharp/codedoc#seealso),
    and anything else that's supportable.
