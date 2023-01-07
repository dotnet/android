# Enumeration Mapping File Documentation

## Background

In order to help with binding enumification (the process of converting groups
of Java constant int fields into C# enums), a file can be created that defines
a map between the fields and the enums to be created.

There is a CSV format and an XML format of this file. In practice, users are 
guided to the XML format via our [documentation][0] and templates. The CSV format 
is mainly used internally for `Mono.Android.dll`, as it converts thousands of 
constants into hundreds of enums.

## CSV Format

The basic format since the beginning of Xamarin contains up to 6 fields:

* **API Level** - This is generally only used by `Mono.Android.dll` to denote
  the Android level the constant was introduced in. For other uses this
  is generally `0`.
* **Enum Type** - C# namespace and type of the enum to create.  For example:
  `Android.Views.WindowProgress`.
* **Enum Member** - C# name of the enum to create. For example:
  `Start`
* **Enum Value** - The value of the enum. For example: `0`.
* **JNI Signature** - The JNI signature of the Java constant to convert. For example:
  `android/view/Window.PROGRESS_START`.
* **Flags** - If this field contains `flags` the enum will be created with the
  `[Flags]` attribute. (Any member will `flags` will make the whole enum `[Flags]`.)

Full example:
```
10,Android.Views.WindowProgress,Start,android/view/Window.PROGRESS_START,0,flags
```

---
**NOTE**

Our CSV files also allow comments using `//`. Lines beginning with this
sequence are ignored.

---

### Transient Mode

By default, Java constants referenced in this format are kept. However the
file can contain a line like this at any point:
```
- ENTER TRANSIENT MODE -
```

Any v1 constants referenced *AFTER* this line will be removed from the bindings.
(This will not affect v2 constants.)

## CSV Format v2

Over time we have found some limitations to the format, such as being able
to specify if the Java constant field should be removed. Additionally, since the
format only specifies constants that *SHOULD* be mapped, we cannot use this
to track constants that we have examined and determined *SHOULD NOT* be mapped.
This has led to various blacklists and tooling of varying success to prevent
us from needing to continually re-audit those constants.

There is now a "v2" version of defining constants. This is a line-level change
and you can mix "v1" and "v2" lines in the same file for backwards compatibility, 
but for consistency it's probably better to stick to one style.

A "v2" line contains up to 9 fields:

* **Action** - The action to perform. This is what denotes a "v2" line, if the first
  character is not one of the following it will be treated as "v1".
  * `E` - Create a C# enum from a Java constant
  * `A` - Create a C# enum not mapped to a Java constant
  * `R` - Remove a Java constant but do not create a C# enum
  * `I` - Explicitly ignore this Java constant
  * `?` - Unknown, an explicit action has not been decided yet, will be ignored
* **API Level** - This is generally only used by `Mono.Android.dll` to denote
  the Android level the constant was introduced in. For other uses this
  is generally `0`.
* **JNI Signature** - The JNI signature of the Java constant to convert. For example:
  `android/view/Window.PROGRESS_START`.
* **Enum Value** - The value of the enum. For example: `0`.
* **Enum Type** - C# namespace and type of the enum to create.  For example:
  `Android.Views.WindowProgress`.
* **Enum Member** - C# name of the enum to create. For example:
  `Start`
* **Field Action** - Action to take on the Java constant. (This replaces Transient mode.)
  * `remove` - Remove the Java constant
  * `keep` - Keeps the Java constant
* **Flags** - If this field contains `flags` the enum will be created with the
  `[Flags]` attribute. (Any member will `flags` will make the whole enum `[Flags]`.)
* **Deprecated Since** - This is generally only used by `Mono.Android.dll` to denote
  the Android level the constant was deprecated in. Specifying "-1" will add an obsolete
  message to the effect of: "This value was incorrectly added to the enumeration and is 
  not a valid value". Leave blank if constant is not deprecated.
  
Full example:
```
E,10,android/view/Window.PROGRESS_START,0,Android.Views.WindowProgress,Start,remove,flags,30
```

[0]: https://docs.microsoft.com/en-us/xamarin/android/platform/binding-java-library/customizing-bindings/java-bindings-metadata#enumfieldsxml-and-enummethodsxml
