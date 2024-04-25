---
title: Layout Code Behind in .NET for Android
description: Layout Code Behind in .NET for Android
ms.date: 04/11/2024
---

# Overview

As part of the .NET for Android build, [Android resources][android-res]
are processed, exposing [Android IDs][android-id] via a generated
`_Microsoft.Android.Resource.Designer.dll` assembly.
For example, given the file `Reources\layout\Main.axml` with contents:

[android-res]: https://developer.android.com/guide/topics/resources/providing-resources
[android-id]: https://developer.android.com/guide/topics/resources/more-resources#Id

```xml
<LinearLayout
    xmlns:android="http://schemas.android.com/apk/res/android">
  <Button android:id="@+id/myButton" />
  <fragment
      android:id="@+id/log_fragment"
      android:name="commonsamplelibrary.LogFragment"
  />
  <fragment
      android:id="@+id/secondary_log_fragment"
      android:name="CommonSampleLibrary.LogFragment"
  />
</LinearLayout>
```

Then during build-time a `_Microsoft.Android.Resource.Designer.dll` assembly
with contents similar to:

```csharp
namespace _Microsoft.Android.Resource.Designer;

partial class Resource {
  partial class Id {
    public static int myButton               {get;}
    public static int log_fragment           {get;}
    public static int secondary_log_fragment {get;}
  }
  partial class Layout {
    public static int Main                   {get;}
  }
}
```

*Traditionally*, interacting with Resources would be done in code, using the
constants from the `Resource` type and the `FindViewById<T>()` method:

```csharp
partial class MainActivity : Activity {

  protected override void OnCreate (Bundle savedInstanceState)
  {
    base.OnCreate (savedInstanceState);
    SetContentView (Resource.Layout.Main);
    Button button = FindViewById<Button>(Resource.Id.myButton);
    button.Click += delegate {
        button.Text = $"{count++} clicks!";
    };
  }
}
```

Starting with Xamarin.Android 8.4, there are two additional ways to interact
with Android resources when using C#:

 1. [Bindings](#resource-bindings)
 2. [Code-Behind](#resource-codebehind)

To enable these new features, set the
[`$(AndroidGenerateLayoutBindings)`](../../building-apps/build-properties.md#androidgeneratelayoutbindings)
MSBuild property to `True` either on the msbuild command line:

```dotnetcli
dotnet build -p:AndroidGenerateLayoutBindings=true MyProject.csproj
```

or in your .csproj file:

```xml
<PropertyGroup>
    <AndroidGenerateLayoutBindings>true</AndroidGenerateLayoutBindings>
</PropertyGroup>
```

<a name="resource-bindings"></a>

## Bindings

A *binding* is a generated class, one per *Android layout file*, which contains
strongly typed properties for all of the *ids* within the layout file. Binding
types are generated into the `global::Bindings` namespace, with type names
which mirror the filename of the layout file.

Binding types are created for all layout files which contain any Android IDs.

Given the Android Layout file `Resources\layout\Main.axml`:

```xml
<LinearLayout
    xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools">
  <Button android:id="@+id/myButton" />
  <fragment
      android:id="@+id/fragmentWithExplicitManagedType"
      android:name="commonsamplelibrary.LogFragment"
      xamarin:managedType="CommonSampleLibrary.LogFragment"
  />
  <fragment
      android:id="@+id/fragmentWithInferredType"
      android:name="CommonSampleLibrary.LogFragment"
  />
</LinearLayout>
```

then following type will be generated:

```csharp
// Generated code
namespace Binding {
  sealed class Main : global::Xamarin.Android.Design.LayoutBinding {

    [global::Android.Runtime.PreserveAttribute (Conditional=true)]
    public Main (
      global::Android.App.Activity client,
      global::Xamarin.Android.Design.OnLayoutItemNotFoundHandler itemNotFoundHandler = null)
        : base (client, itemNotFoundHandler) {}

    [global::Android.Runtime.PreserveAttribute (Conditional=true)]
    public Main (
      global::Android.Views.View client,
      global::Xamarin.Android.Design.OnLayoutItemNotFoundHandler itemNotFoundHandler = null)
        : base (client, itemNotFoundHandler) {}

    Button __myButton;
    public Button myButton => FindView (global::Xamarin.Android.Tests.CodeBehindFew.Resource.Id.myButton, ref __myButton);

    CommonSampleLibrary.LogFragment __fragmentWithExplicitManagedType;
    public CommonSampleLibrary.LogFragment fragmentWithExplicitManagedType =>
      FindFragment (global::Xamarin.Android.Tests.CodeBehindFew.Resource.Id.fragmentWithExplicitManagedType, __fragmentWithExplicitManagedType, ref __fragmentWithExplicitManagedType);

    global::Android.App.Fragment __fragmentWithInferredType;
    public global::Android.App.Fragment fragmentWithInferredType =>
      FindFragment (global::Xamarin.Android.Tests.CodeBehindFew.Resource.Id.fragmentWithInferredType, __fragmentWithInferredType, ref __fragmentWithInferredType);
  }
}
```

The binding's base type, `Xamarin.Android.Design.LayoutBinding` is **not** part of the
.NET for Android class library but rather shipped with .NET for Android in source form
and included in the application's build automatically whenever bindings are used.

The generated binding type can be created around `Activity` instances, allowing
for strongly-typed access to IDs within the layout file:

```csharp
// User-written code
partial class MainActivity : Activity {

  protected override void OnCreate (Bundle savedInstanceState)
  {
    base.OnCreate (savedInstanceState);

    SetContentView (Resource.Layout.Main);
    var binding     = new Binding.Main (this);
    Button button   = binding.myButton;
    button.Click   += delegate {
        button.Text = $"{count++} clicks!";
    };
  }
}
```

Binding types may also be constructed around `View` instances, allowing
strongly-typed access to Resource IDs *within* the View or its children:

```csharp
var binding = new Binding.Main (some_view);
```

### Missing Resource IDs

Properties on binding types still use `FindViewById<T>()` in their
implementation. If `FindViewById<T>()` returns `null`, then the default
behavior is for the property to throw an `InvalidOperationException`
instead of returning `null`.

This default behavior may be overridden by passing an error handler delegate to
the generated binding on its instantiation:

```csharp
// User-written code
partial class MainActivity : Activity {

  Java.Lang.Object? OnLayoutItemNotFound (int resourceId, Type expectedViewType)
  {
     // Find and return the View or Fragment identified by `resourceId`
     // or `null` if unknown
     return null;
  }

  protected override void OnCreate (Bundle savedInstanceState)
  {
    base.OnCreate (savedInstanceState);

    SetContentView (Resource.Layout.Main);
    var binding     = new Binding.Main (this, OnLayoutItemNotFound);
  }
}
```

The `OnLayoutItemNotFound()` method is invoked when a resource ID for a `View` or a `Fragment`
could not be found.

The handler *must* return either `null`, in which case the `InvalidOperationException` will be
thrown or, preferably, return the `View` or `Fragment` instance that corresponds to the
ID passed to the handler. The returned object **must** be of the correct type matching the type
of the corresponding Binding property. The returned value is cast to that type, so if the object
isn't correctly typed an exception will be thrown.


<a name="resource-codebehind"></a>

## Code-Behind

Code-Behind involves build-time generation of a `partial` class which contains
strongly typed properties for all of the *ids* within the layout file.

Code-Behind builds atop the Binding mechanism, while requiring that layout
files "opt-in" to Code-Behind generation by using the new
[`xamarin:classes`](#attr-xamarin-classes) XML attribute, which is a `;`-separated
list of full class names to be generated.

Given the Android Layout file `Resources\layout\Main.axml`:

```xml
<LinearLayout
    xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools"
    xamarin:classes="Example.MainActivity">
  <Button android:id="@+id/myButton" />
  <fragment
      android:id="@+id/fragmentWithExplicitManagedType"
      android:name="commonsamplelibrary.LogFragment"
      xamarin:managedType="CommonSampleLibrary.LogFragment"
  />
  <fragment
      android:id="@+id/fragmentWithInferredType"
      android:name="CommonSampleLibrary.LogFragment"
  />
</LinearLayout>
```

at build time the following type will be produced:

```csharp
// Generated code
namespace Example {
  partial class MainActivity {
    Binding.Main __layout_binding;

    public override void SetContentView (global::Android.Views.View view);
    void SetContentView (global::Android.Views.View view,
                         global::Xamarin.Android.Design.LayoutBinding.OnLayoutItemNotFoundHandler onLayoutItemNotFound);

    public override void SetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params);
    void SetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params,
                         global::Xamarin.Android.Design.LayoutBinding.OnLayoutItemNotFoundHandler onLayoutItemNotFound);

    public override void SetContentView (int layoutResID);
    void SetContentView (int layoutResID,
                         global::Xamarin.Android.Design.LayoutBinding.OnLayoutItemNotFoundHandler onLayoutItemNotFound);

    partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn);
    partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn);
    partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn);

    public Button myButton => __layout_binding?.myButton;
    public CommonSampleLibrary.LogFragment fragmentWithExplicitManagedType => __layout_binding?.fragmentWithExplicitManagedType;
    public global::Android.App.Fragment fragmentWithInferredType => __layout_binding?.fragmentWithInferredType;
  }
}
```

This allows for more "intuitive" use of Resource IDs within the layout:

```csharp
// User-written code
partial class MainActivity : Activity {
  protected override void OnCreate (Bundle savedInstanceState)
  {
    base.OnCreate (savedInstanceState);

    SetContentView (Resource.Layout.Main);

    myButton.Click += delegate {
        button.Text = $"{count++} clicks!";
    };
  }
}
```

The `OnLayoutItemNotFound` error handler can be passed as the last parameter of whatever overload
of `SetContentView` the activity is using:

```csharp
// User-written code
partial class MainActivity : Activity {
  protected override void OnCreate (Bundle savedInstanceState)
  {
    base.OnCreate (savedInstanceState);

    SetContentView (Resource.Layout.Main, OnLayoutItemNotFound);
  }

  Java.Lang.Object? OnLayoutItemNotFound (int resourceId, Type expectedViewType)
  {
    // Find and return the View or Fragment identified by `resourceId`
    // or `null` if unknown
    return null;
  }
}
```

As Code-Behind relies on partial classes, *all* declarations of a partial class
*must* use `partial class` in their declaration, otherwise a [CS0260][cs0260]
C# compiler error will be generated at build time.

[cs0260]: /dotnet/csharp/language-reference/compiler-messages/cs0260

### Customization

The generated Code Behind type *always* overrides `Activity.SetContentView()`,
and by default it *always* calls `base.SetContentView()`, forwarding the
parameters. If this is not desired, then one of the `OnSetContentView()`
*`partial` methods* should be overridden, setting `callBaseAfterReturn`
to `false`:

```csharp
// Generated code
namespace Example
{
  partial class MainActivity {
    partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn);
    partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn);
    partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn);
  }
}
```

### Example Generated Code

```csharp
// Generated code
namespace Example
{
  partial class MainActivity {

    Binding.Main? __layout_binding;

    public override void SetContentView (global::Android.Views.View view)
    {
      __layout_binding = new global::Binding.Main (view);
      bool callBase = true;
      OnSetContentView (view, ref callBase);
      if (callBase) {
        base.SetContentView (view);
      }
    }

    void SetContentView (global::Android.Views.View view, global::Xamarin.Android.Design.LayoutBinding.OnLayoutItemNotFoundHandler onLayoutItemNotFound)
    {
      __layout_binding = new global::Binding.Main (view, onLayoutItemNotFound);
      bool callBase = true;
      OnSetContentView (view, ref callBase);
      if (callBase) {
        base.SetContentView (view);
      }
    }

    public override void SetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params)
    {
      __layout_binding = new global::Binding.Main (view);
      bool callBase = true;
      OnSetContentView (view, @params, ref callBase);
      if (callBase) {
        base.SetContentView (view, @params);
      }
    }

    void SetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, global::Xamarin.Android.Design.LayoutBinding.OnLayoutItemNotFoundHandler onLayoutItemNotFound)
    {
      __layout_binding = new global::Binding.Main (view, onLayoutItemNotFound);
      bool callBase = true;
      OnSetContentView (view, @params, ref callBase);
      if (callBase) {
        base.SetContentView (view, @params);
      }
    }

    public override void SetContentView (int layoutResID)
    {
      __layout_binding = new global::Binding.Main (this);
      bool callBase = true;
      OnSetContentView (layoutResID, ref callBase);
      if (callBase) {
        base.SetContentView (layoutResID);
      }
    }

    void SetContentView (int layoutResID, global::Xamarin.Android.Design.LayoutBinding.OnLayoutItemNotFoundHandler onLayoutItemNotFound)
    {
      __layout_binding = new global::Binding.Main (this, onLayoutItemNotFound);
      bool callBase = true;
      OnSetContentView (layoutResID, ref callBase);
      if (callBase) {
        base.SetContentView (layoutResID);
      }
    }

    partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn);
    partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn);
    partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn);

    public  Button                          myButton                         => __layout_binding?.myButton;
    public  CommonSampleLibrary.LogFragment fragmentWithExplicitManagedType  => __layout_binding?.fragmentWithExplicitManagedType;
    public  global::Android.App.Fragment    fragmentWithInferredType         => __layout_binding?.fragmentWithInferredType;
  }
}
```


## Layout XML Attributes

Many new Layout XML attributes control Binding and Code-Behind behavior, which
are within the `xamarin` XML namespace
(`xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools"`).
These include:

  * [`xamarin:classes`](#attr-xamarin-classes)
  * [`xamarin:managedType`](#attr-xamarin-managedType)


<a name="attr-xamarin-classes">

### `xamarin:classes`

The `xamarin:classes` XML attribute is used as part of
[Code-Behind](#resource-codebehind) to specify which types should be generated.

The `xamarin:classes` XML attribute contains a `;`-separated list of
*full class names* that should be generated.


<a name="attr-xamarin-managedType"></a>

### `xamarin:managedType`

The `xamarin:managedType` layout attribute is used to explicitly specify the
managed type to expose the bound ID as. If not specified, the type will be
inferred from the declaring context, e.g. `<Button/>` will result in an
`Android.Widget.Button`, and `<fragment/>` will result in an
`Android.App.Fragment`.

The `xamarin:managedType` attribute allows for more explicit type declarations.


## Managed type mapping

It is quite common to use widget names based on the Java package they come
from and, equally as often, the managed .NET name of such type will have a
different (.NET style) name in the managed land. The code generator can perform
a number of very simple adjustments to try to match the code, such as:

  * Capitalize all the components of the type namespace and name. For instance
    `java.package.myButton` would become `Java.Package.MyButton`

  * Capitalize two-letter components of the type namespace. For instance
    `android.os.SomeType` would become `Android.OS.SomeType`

  * Look up a number of hard-coded namespaces which have known mappings.
    Currently the list includes the following mappings:

      * [`android.view`](https://developer.android.com/reference/android/view/package-summary) ->
        [`Android.Views`](/dotnet/api/android.views)
      * `com.actionbarsherlock` -> `ABSherlock`
      * `com.actionbarsherlock.widget` -> `ABSherlock.Widget`
      * `com.actionbarsherlock.view` -> `ABSherlock.View`
      * `com.actionbarsherlock.app` -> `ABSherlock.App`

  * Look up a number of hard-coded types in internal tables. Currently the list includes the following types:

      * `WebView` -> [`Android.Webkit.WebView`](/dotnet/api/android.webkit.webview)

  * Strip number of hard-coded namespace *prefixes*. Currently the list includes the following prefixes:

    * `com.google.`

If, however, the above attempts fail, you will need to modify the layout which
uses a widget with such an unmapped type to add both the `xamarin` XML
namespace declaration to the root element of the layout and the
`xamarin:managedType` to the element requiring the mapping. For instance:

```xml
<fragment
    android:id="@+id/log_fragment"
    android:name="commonsamplelibrary.LogFragment"
    xamarin:managedType="CommonSampleLibrary.LogFragment"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
/>
```

Will use the `CommonSampleLibrary.LogFragment` type for the native type `commonsamplelibrary.LogFragment`.

You can avoid adding the XML namespace declaration and the
`xamarin:managedType` attribute by simply naming the type using its managed
name, for instance the above fragment could be redeclared as follows:

```xml
<fragment
    android:name="CommonSampleLibrary.LogFragment"
    android:id="@+id/secondary_log_fragment"
    android:layout_width="match_parent"
    android:layout_height="match_parent"
/>
```

### Fragments: a special case

The Android ecosystem currently supports two distinct implementations of the `Fragment` widget:

  * [`Android.App.Fragment`](/dotnet/api/android.app.fragment)
    The "classic" Fragment shipped with the base Android system
  * `AndroidX.Fragment.App.Fragment`, in the
    [`Xamarin.AndroidX.Fragment`](https://www.nuget.org/packages/Xamarin.AndroidX.Fragment)
    NuGet package.

These classes are **not** compatible with each other and so special care must be
taken when generating binding code for `<fragment>` elements in the layout files. .NET for Android must
choose one `Fragment` implementation as the default one to be used if the `<fragment>` element does not
have any specific type (managed or otherwise) specified. Binding code generator uses the
[`$(AndroidFragmentType)`](../../building-apps/build-properties.md#androidfragmenttype)
MSBuild property for that purpose. The property can be overriden by the user to specify a type different
than the default one. The property is set to `Android.App.Fragment` by default, and is overridden by the
AndroidX NuGet packages.

If the generated code does not build, the layout file must be amended by specifying the manged type of the
fragment in question.


## Code-behind layout selection and processing

### Selection

By default code-behind generation is disabled. To enable processing for all
layouts in any of the `Resource\layout*` directories that contain at least a
single element with the `//*/@android:id` attribute, set the
`$(AndroidGenerateLayoutBindings)` MSBuild property to `True` either on the
msbuild command line:

```dotnetcli
dotnet build -p:AndroidGenerateLayoutBindings=true MyProject.csproj
```

or in your .csproj file:

```xml
<PropertyGroup>
  <AndroidGenerateLayoutBindings>true</AndroidGenerateLayoutBindings>
</PropertyGroup>
```

Alternatively, you can leave code-behind disabled globally and enable it only
for specific files. To enable Code-Behind for a particular `.axml` file, change
the file to have a **Build action** of
[`@(AndroidBoundLayout)`](../../building-apps/build-items.md#androidboundlayout)
by editing your `.csproj` file and replacing `AndroidResource` with `AndroidBoundLayout`:

```xml
<!-- This -->
<AndroidResource Include="Resources\layout\Main.axml" />
<!-- should become this -->
<AndroidBoundLayout Include="Resources\layout\Main.axml" />
```

### Processing

Layouts are grouped by name, with like-named templates from *different*
`Resource\layout*` directories comprising a single group. Such groups are
processed as if they were a single layout. It is possible that in such case
there will be a type clash between two widgets found in different layouts
belonging to the same group. In such case the generated property will not be
able to have the exact widget type, but rather a "decayed" one. Decaying
follows the algorithm below:

 1. If all of the conflicting widgets are `View` derivatives, the property
    type will be `Android.Views.View`

 2. If all of the conflicting types are `Fragment` derivatives, the property
    type will be `Android.App.Fragment`

 3. If the conflicting widgets contain both a `View` and a `Fragment`, the
    property type will be `global::System.Object`

## Generated code

If you are interested in how the generated code looks for your layouts, please
take a look in the `obj\$(Configuration)\generated` folder in your solution
directory.
