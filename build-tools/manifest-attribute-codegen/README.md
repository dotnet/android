# What is this?

`manifest-attribute-codegen` a tool that:

 1. Collects Android SDK resource information about what XML elements can be placed in an
    `AndroidManifest.xml` by reading Android SDK `attrs_manifest.xml` files, and
 2. Verifies that `metadata.xml` (within this directory) appropriately mirrors (1), and
 3. (Optionally) emits `manifest-definition.xml` (within this directory), based on (1),
    which specifies in which API level a given element or attribute was added.

Using `metadata.xml`, we can generate `[(Application|Activity|etc)Attribute]` classes that can be used
by both `Mono.Android` and `Xamarin.Android.Build.Tasks`.

This generally only needs to be done each time we bind a new Android API level.

# How to use

Ensure all Android platform SDK levels are installed.  This can be done with `xaprepare`;
run the following command from the checkout toplevel:

```dotnetcli
dotnet run --project "build-tools/xaprepare/xaprepare/xaprepare.csproj" -- -s AndroidTestDependencies --android-sdk-platforms=all
```

Next, from this directory, run:

```sh
dotnet build -t:GenerateManifestAttributes

# alternatively, from toplevel
(cd build-tools/manifest-attribute-codegen && dotnet build -t:GenerateManifestAttributes)
```

If (1) and (2) are consistent with each other, new `*Attribute.cs` files will be generated.

If (1) and (2) are *inconsistent* -- typically because a new element or attribute was added to
`AndroidManifest.xml` -- then a list of unaccounted elements and/or attributes will be specified
in the error output, and you will be required to specify how to handle the new pieces.

# `metadata.xml` file structure

There are two types of elements in `metadata.xml` file: `<type/>` and `<element/>`.
These represent valid instances of entries and attributes respectively that can be placed in an
`AndroidManifest.xml` file as specified in the Android SDK `attrs_manifest.xml` files.

For example, consider the following valid `AndroidManifest.xml` entry:

```xml
<activity name="MyActivity" />
```

This is partially declared within the Android SDK `attrs_manifest.xml` as:

```xml
<declare-styleable name="AndroidManifestActivity" parent="AndroidManifestApplication">
    <attr name="name" />
</declare-styleable>
```

(Note: a degree of "squinting" must be done to map `AndroidManifestActivity` to `activity`.)

The `<activity name="…"/>` element is represented in `metadata.xml` via both a `<type/>`
and an `<element/>` entry:

```xml
<type name="activity" namespace="Android.App" outputFile="src\Xamarin.Android.NamingCustomAttributes\Android.App\ActivityAttribute.cs" usage="AttributeTargets.Class" jniNameProvider="true" />
<element path="activity.name" visible="true" />
```

The required/optional attributes for `<type>` and `<element>` are described further below.

# Handling new types (elements)

When a new type (element) like `<activity>` or `<uses-permission>` is found, there are 2 choices:
ignore it or bind it.

## Ignore the type

The majority of the found types are not bound so the most likely scenario may be to ignore the new type:

```xml
<type name="element-to-ignore" ignore="true" />
```

## Bind the type

Alternatively, the type can be bound:

```xml
<type name="activity" namespace="Android.App" outputFile="src\Xamarin.Android.NamingCustomAttributes\Android.App\ActivityAttribute.cs" usage="AttributeTargets.Class" />
```

Required attributes:

- **name** - The name of the new element in `AndroidManifest.xml`, like `activity` or `application`
- **namespace** - The C# namespace the element will be placed in
- **outputFile** - The path to write the generated `.cs`
- **usage** - The `validOn` attribute usage information passed to `[AttributeUsage]`

Optional attributes:

- **allowMultiple** - The `allowMultiple` attribute usage information passed to `[AttributeUsage]`; defaults to `false`
- **jniNameProvider** - Whether the attribute should implement `Java.Interop.IJniNameProviderAttribute`; defaults to `false`
- **defaultConstructor** - Whether a parameterless constructor should be generated; defaults to `true`
- **sealed** - Whether the attribute type should be `sealed`; defaults to `true`
- **managedName** - The name of the managed attribute class if the default isn't correct
- **generateMapping** - Whether to generate the `mapping` field used by `Xamarin.Android.Build.Tasks`; defaults to `true`

Note that if a new type is created, there will be additional manual code that needs to be written to configure
`Xamarin.Android.Build.Tasks` to translate the attribute to AndroidManifest xml.

Example:

 - src/Xamarin.Android.Build.Tasks/Mono.Android/ApplicationAttribute.Partial.cs
 - src/Xamarin.Android.Build.Tasks/Utilities/ManifestDocument.cs

# Handling new attributes

When a new attribute like `<activity newattr="bar" />` is added, it also must be specified if the attribute should be visible to users or not.

## Hide the attribute

To hide the attribute:

```xml
<element path="activity.newattr" visible="false" />
```

## Surface the attribute

To surface the attribute:

```xml
<element path="activity.newattr" visible="true" />
```

Required metadata

- **path** - A specifier for this attribute, of the form `{element_name}.{attribute_name}`

Optional metadata (note that if any metadata is set, `visible` is assumed to be `true` unless specified otherwise):

- **type** - C# type to override missing type information from the manifest definition
- **name** - The name of the managed attribute property if the default isn't correct
- **obsolete** - A string describing the reason for this member being `[Obsolete ("reason")]`
- **readonly** - Whether to generate the property with a `private set`; defaults to `false`
- **manualMap** - Whether to exclude the property from the `mapping` field used by `Xamarin.Android.Build.Tasks`; defaults to `false`
