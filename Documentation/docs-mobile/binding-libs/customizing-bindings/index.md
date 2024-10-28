---
title: "Customizing Bindings"
description: "You can customize a .NET for Android binding by editing the metadata that controls the binding process. These manual modifications are often necessary for resolving build errors and for shaping the resulting API so that it is more consistent with C#/.NET. These guides explain the structure of this metadata and how to modify the metadata."
ms.author: jopobst
ms.date: 05/06/2024
---

# Customizing bindings

.NET for Android automates much of the binding process; however, C# and Java are
different languages that do not support exactly the same features, and thus there
are cases where manual modification is required to fix differences that cannot be
resolved automatically.
 
Some examples of these issues are:

- Resolving build errors caused by missing types, obfuscated types, 
    duplicate names, class visibility issues, and other situations that 
    cannot be resolved by the .NET for Android tooling. 

- Removing unused types that do not need to be bound. 

- Adding types that have no counterpart in the underlying Java API. 

Additionally it may be desirable to make some ergonomic customizations to make bindings more pleasant to use, like:

- Changing the namespace containing the bound types.

You can make some or all of these changes by modifying the metadata
that controls the binding process.

## Guides

The following guides describe the metadata that controls the binding process and 
explain how to modify this metadata to address these issues:

- [Java Bindings Metadata](java-bindings-metadata.md)
    provides an overview of the metadata that goes into a Java binding.
    It describes the various manual steps that are sometimes required to
    complete a Java binding library, and it explains how to shape an API
    exposed by a binding to more closely follow .NET design guidelines.

- [Namespace Customization](namespace-customization.md)
    explains how to customize the namespace(s) that bound types are placed in.

- [Creating Enumerations](creating-enums.md)
    explains how to map collections of Java integer constants into .NET enumerations.
