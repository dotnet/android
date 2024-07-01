# MSBuild Reference Assemblies

MSBuild and Roslyn support generating [reference
assemblies][msbuild_refassemblies] by setting the
`$(ProduceReferenceAssembly)` MSBuild property to True.

Why do we care?  What's this mean?

Assume you have a solution with two projects.  Project
`Referenced.csproj` has no further references.  Project
`Referencer.csproj` has a `@(ProjectReference)` to
`Referenced.csproj`.

The developer makes a change to something within `Referenced.csproj`.

Question: Does `Referencer.csproj` need to be rebuilt?

In the "original" MSBuild world -- the world that .NET for Android
still lives in -- the answer is *yes*, `Referencer.csproj` *must
**always*** be rebuilt, because the change to `Referenced.csproj`
*may* contain an API breaking change which would prevent
`Referencer.csproj` from building.

In the new `$(ProduceReferenceAssembly)=True` world order, the answer
is instead ***maybe***: `Referencer.csproj` *only* needs to be built
if the reference assembly produced as part of the `Referenced.csproj`
build is updated, which in turn only happens when the *public* API
changes.  Meaning if a change *doesn't* alter the public API -- adding
comments, fixing a method implementation, adding `private`/`internal`
members, etc. -- then `Referencer.csproj` need not be rebuilt *at
all*.

In more concrete terms, assume you have a Xamarin.Forms solution
containing a Xamarin.Forms PCL project and a referencing Android App
project.  *Currently*, whenever the PCL project is changed, the App
project must *always* be rebuilt.  In a
`$(ProduceReferenceAssembly)=True` order, the App project would need
to be rebuilt *less often*.

[msbuild_refassemblies]: https://github.com/dotnet/roslyn/blob/master/docs/features/refout.md

# Reference Assemblies in Practice

Adding the following to a `csproj` file:

```xml
<ProduceReferenceAssembly>True</ProduceReferenceAssembly>
```

Causes a `bin\Debug\ref\MyLibrary.dll` to exist alongside
`bin\Debug\MyLibrary.dll`.

Two item groups, `@(ReferenceCopyLocalPaths)` and `@(ReferencePath)`
will have new `%(ReferenceAssembly)` metadata:

    bin\Debug\MyLibrary.dll
        ReferenceAssembly = C:\full\path\to\bin\Debug\ref\MyLibrary.dll

If an assembly does *not* include a reference assembly, it will still
maintain `%(ReferenceAssembly)` metadata pointing to itself:

    bin\Debug\OtherLibrary.dll
        ReferenceAssembly = C:\full\path\to\bin\Debug\OtherLibrary.dll

In some cases this metadata might not be there, such as using a path
as input to `ResolveAssemblies` (`$(OutDir)$(TargetFileName)`), so our
`ResolveAssemblies` MSBuild task should produce the value if it does
not exist.

Another option is to use the `@(ReferencePathWithRefAssemblies)` item
group in place of `@(ReferencePath)`, which would have a value of:

    C:\full\path\to\bin\Debug\ref\MyLibrary.dll
    C:\full\path\to\bin\Debug\OtherLibrary.dll

This item group is populated by the
`FindReferenceAssembliesForReferences` MSBuild target, so be sure to
only use this `<ItemGroup/>` *after* it.

We can rely on these since MSBuild 15.3.

# Problems with Reference Assemblies

## Problem 1

The original thought here was we could use
`$(ProduceReferenceAssembly)` in the following scenario:

1. You have a Xamarin.Forms solution, containing a Xamarin.Android
   head project and a NetStandard library with your Xamarin.Forms
   code.
2. You make a XAML change in the NetStandard library.
3. Parts of the build in the Xamarin.Android could be skipped, since
   *most of the time* XAML changes would not impact the public API.

Unfortunately, we discovered that the reference assembly will contain
`EmbeddedResource` items. And so XAML changes *always* cause the
reference assembly to be updated! An issue has been fixed in Roslyn,
which should surface in Dev16 Preview 3 on Windows. Unknown how soon
this will land in Mono.

See the following Github issues for more info:

* [msbuild #2646][msbuild_2646]
* [roslyn #31197][roslyn_31197]

[msbuild_2646]: https://github.com/Microsoft/msbuild/issues/2646#issuecomment-439101035
[roslyn_31197]: https://github.com/dotnet/roslyn/issues/31197

## Problem 2

By default the Xamarin.Forms item template adds new Xamarin.Forms
pages such as:

```csharp
public partial class MyPage : ContentPage
{
    public MyPage ()
    {
        InitializeComponent ();
    }
}
```

In `InitializeComponent` the "magic happens", that loads XAML such as:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage ...>
  <StackLayout>
    <Label Text="Welcome to Xamarin.Forms!" />
  </StackLayout>
</ContentPage>
```

Unfortunately, adding `x:Name="MyLabel"` to the `<Label/>` will add a
field to be populated when the XAML loads:

```csharp
public Label MyLabel;
```

This changes the public API! Hence the reference assembly would
change. A similar problem would occur when adding new pages or new
views.

The initial thoughts to workaround this are to remove the `public`
modifier on the item templates.