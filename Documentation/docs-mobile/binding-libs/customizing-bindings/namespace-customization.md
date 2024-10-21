---
title: "Customizing Namespaces"
description: "Renaming Java package names to better fit C# namespaces conventions is a very common use of binding metadata. In order to make this task easier, customizations can be made in the MSBuild project file instead of writing metadata."
ms.author: jopobst
ms.date: 05/06/2024
---

# Customizing namespaces

Java package names often do not match C# namespace conventions due to issues such as extra prefixes
and capitalization differences.

For example, the Java package name `com.microsoft.streamwriters` would be automatically translated to
`Com.Microsoft.Streamwriters` because the binding process automatically translates namespaces to Pascal
case. However a better fit would be `Microsoft.StreamWriters`.

This can be accomplished by adding an `<AndroidNamespaceReplacement>` item to the project file:

```xml
<ItemGroup>
  <AndroidNamespaceReplacement Include='com.microsoft.streamwriters' Replacement='Microsoft.StreamWriters' />
</ItemGroup>
```

Although Java packages can also be renamed using traditional `metadata`, it can be tedious to rename many similar packages:

```xml
<attr path="/api/package[@name='com.microsoft.accessibility']" name="managedName">Microsoft.Accessibility</attr>
<attr path="/api/package[@name='com.microsoft.content']" name="managedName">Microsoft.Content</attr>
<attr path="/api/package[@name='com.microsoft.core']" name="managedName">Microsoft.Core</attr>
...
```

With `<AndroidNamespaceReplacement>`, the same MSBuild transform placed in the project file
can be applied to all matching packages:

```xml
<ItemGroup>
  <AndroidNamespaceReplacement Include='com.microsoft' Replacement='Microsoft' />
</ItemGroup>
```

## Specification

These replacements will **_only_** be run for `<package>` elements that do not specify a `metadata` `@managedName` attribute.  
If `@managedName` is used, you are opting to provide the exact desired name, it will not be processed further.

Unlike unused metadata, these replacement will not raise a warning if they are unused.

### Case sensitivity

Replacements run **_after_** the automatic Pascal case transform, but the compare is case-insensitive.

Thus, both of the following are equivalent:
```xml
<AndroidNamespaceReplacement Include='Androidx' Replacement='AndroidX' />
<AndroidNamespaceReplacement Include='androidx' Replacement='AndroidX' />
```

### Word bounds

Replacements take place **_only_** on full words (namespace parts).

Thus,  
```xml
<AndroidNamespaceReplacement Include='Com' Replacement='' />
```

Matches matches `Com.Google.Library`, but not `Common.Google.Library` or `Google.Imaging.Dicom`.

Multiple full words can be used:

```xml
<AndroidNamespaceReplacement Include='Com.Google' Replacement='Google' />
<AndroidNamespaceReplacement Include='Com.Androidx' Replacement='Microsoft.AndroidX' />
```

### Word position

The word part match can be constrained to the beginning or end of a namespace by appending a `.` or prepending a `.`, respectively.

```xml
<AndroidNamespaceReplacement Include='Androidx.' Replacement='Microsoft.AndroidX' />
```

matches `Androidx.Core`, but not `Square.OkHttp.Androidx`.

Similarly,

```xml
<AndroidNamespaceReplacement Include='.Compose' Replacement='ComposeUI' />
```

matches `Google.AndroidX.Compose`, but not `Google.Compose.Writer`.

Both prepending and appending a `.` makes it an exact match.

```xml
<AndroidNamespaceReplacement Include='.Androidx.' Replacement='Microsoft.AndroidX' />
```

matches `Androidx`, but not `Google.Androidx.Core`.

### Replacement order

Replacements run in the order specified by the `<ItemGroup>`, however adding to this group at different times may result in an unintended order.

Replacements are run sequentially, and multiple replacements may affect a single namespace.

```xml
<AndroidNamespaceReplacement Include='Androidx' Replacement='Microsoft.AndroidX' />
<AndroidNamespaceReplacement Include='View' Replacement='Views' />
```

changes `Androidx.View` to `Microsoft.AndroidX.Views`.

## Relation to metadata

Technically `@(AndroidNamespaceReplacement)` is implemented as a special case of `<metadata>`, and can be placed directly in a `metadata.xml` file in  
metadata form if desired.

That is, the MSBuild item:
```xml
<ItemGroup>
  <AndroidNamespaceReplacement Include='com.microsoft' Replacement='Microsoft' />
</ItemGroup>
```

could instead be placed in `metadata.xml` as:
```xml
<metadata>
  <ns-replace source='com.microsoft' replacement='Microsoft' />
</metadata>
```