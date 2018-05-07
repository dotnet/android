---
id: 11763499-79e9-4868-83e6-41f3061745d1
title: "Layout CodeBehind"
dateupdated: 2018-04-23
---

# Overview

The Xamarin.Android build processes [Android resources][android-res], exposing
[Android IDs][android-id] via a generated `Resource.designer.cs` file.
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

Then during build-time a `Resource.designer.cs` file will be generated:

```csharp
partial class Resource {
  partial class Id {
    public const int myButton;
    public const int log_fragment;
    public const int secondary_log_fragment;
  }
  partial class Layout {
    public const int Main;
  }
}
```

*Traditionally*, interacting with Resources would be done in code, using the
constants from the `Resource` type and the `FindViewById<T>()` method:

```csharp
class MainActivity : Activity {

  // Code omitted for brevity

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

<a name="resource-bindings" />

# Bindings

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
      android:id="@+id/log_fragment"
      android:name="commonsamplelibrary.LogFragment"
      xamarin:managedType="CommonSampleLibrary.LogFragment"
  />
  <fragment
      android:id="@+id/secondary_log_fragment"
      android:name="CommonSampleLibrary.LogFragment"
  />
</LinearLayout>
```

then following type will be generated:

```csharp
// Generated code
namespace Binding {
  sealed class Main : global::Xamarin.Android.Design.LayoutBinding
  {
    public Main (global::Xamarin.Android.Design.ILayoutBindingClient client);

    public override int ResourceLayoutID => Resource.Layout.Main;
   
    public Button                          myButton                {get;}
    public CommonSampleLibrary.LogFragment log_fragment            {get;}
    public global::Android.App.Fragment    secondary_log_fragment  {get;}
  }
}
```

The generated binding type can be created around `Activity` instances, allowing
for strongly-typed access to IDs within the layout file:

```csharp
// User-written code
class MainActivity : Activity {

  // Code omitted for brevity

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

Alternatively, instead of using `SetContentView(int)`, which may result in a
"mismatch" between the Layout id and the Binding type, the new
`Activity.SetContentView<T>()` method may be used instead:

```csharp
// User-written code
class MainActivity : Activity {

  // Code omitted for brevity

  protected override void OnCreate (Bundle savedInstanceState)
  {
    base.OnCreate (savedInstanceState);

    var binding     = SetContentView<Binding.Main>();
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

## Missing Resource IDs

Properties on binding types still use `FindViewById<T>()` in their
implementation. If `FindViewById<T>()` returns `null`, then the default
behavior is for the property to throw an `InvalidOperationException`
instead of returning `null`.

This default behavior may be overridden by overriding one of the following
methods on the `Activity` type provided to the Binding class constructor:

```csharp
namespace Android.App {
  partial class Activity {
    protected virtual void OnLayoutViewNotFound<T> (int resourceId, ref T view)
        where T: View;
    protected virtual void OnLayoutFragmentNotFound<T> (int resourceId, ref T fragment)
        where T: Fragment;
  }
}
```

The `Activity.OnLayoutViewNotFound<T>()` method is invoked when a resource ID
for a `View` could not be found.

The `Activity.OnLayoutFragmentNotFound<T>()` method is invoked when a
resource ID for a `Fragment` could not be found.

`Android.Views.View` also provides these methods, for us by custom `View`
subclasses:

```csharp
namespace Android.Views {
  partial class View {
    protected virtual void OnLayoutViewNotFound<T> (int resourceId, ref T view)
        where T: View;
    protected virtual void OnLayoutFragmentNotFound<T> (int resourceId, ref T fragment)
        where T: Fragment;
  }
}
```

On both `Activity`s and `View`s, `OnLayoutViewNotFound<T>()` *must* set `view`
to a non-`null` value in order to prevent the `InvalidOperationException` from
being thrown.

Likewise, on both `Activity`s and `View`s, `OnLayoutFragmentNotFound<T>()`
*must* set `fragment` to a non-`null` value in order to prevent the
`InvalidOperationException` from being thrown.


<a name="resource-codebehind" />

# Code-Behind

Code-Behind involves build-time generation of a `partial` class which contains
strongly typed properties for all of the *ids* within the layout file.

Code-Behind builds atop the Binding mechanism, while requiring that layout
files "opt-in" to Code-Behind generation by using the new
[`xamarin:classes`](#xamarin:classes) XML attribute, which is a `;`-separated
list of full class names to be generated.

Given the Android Layout file `Resources\layout\Main.axml`:

```xml
<LinearLayout
    xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools"
    xamarin:classes="Example.MainActivity">
  <Button android:id="@+id/myButton" />
  <fragment
      android:id="@+id/log_fragment"
      android:name="commonsamplelibrary.LogFragment"
      xamarin:managedType="CommonSampleLibrary.LogFragment"
  />
  <fragment
      android:id="@+id/secondary_log_fragment"
      android:name="CommonSampleLibrary.LogFragment"
  />
</LinearLayout>
```

at build time the following type will be produced:

```csharp
// Generated code
namespace Example {
  partial class MainActivity {
    public Button                           myButton                {get;}
    public CommonSampleLibrary.LogFragment  log_fragment            {get;}
    public global::Android.App.Fragment     secondary_log_fragment  {get;}

    public override void SetContentView (global::Android.Views.View view);
    public override void SetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params);
    public override void SetContentView (int layoutResID);

    partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn);
    partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn);
    partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn);
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

As Code-Behind relies on partial classes, *all* declarations of a partial class
*must* use `partial class` in their declaration, otherwise a [CS0260][cs0260]
C# compiler error will be generated at build time.

[cs0260]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs0260

## Customization

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

## Example Generated Code

```csharp
// Generated code
namespace Example
{
  partial class MainActivity {

    Binding.Main __layout_binding;

    public override void SetContentView (global::Android.Views.View view)
    {
      __layout_binding = new global::Binding.Main (view);
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
        base.SetContentView (view);
      }
    }

    public override void SetContentView (int layoutResID)
    {
      __layout_binding = new global::Binding.Main (this);
      bool callBase = true;
      OnSetContentView (layoutResID, ref callBase);
      if (callBase) {
        base.SetContentView (view);
      }
    }

    partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn);
    partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn);
    partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn);

    public  Button                          myButton                => __layout_binding?.myButton;
    public  CommonSampleLibrary.LogFragment log_fragment            => __layout_binding?.log_fragment;
    public  global::Android.App.Fragment    secondary_log_fragment  => __layout_binding?.secondary_log_fragment;
  }
}
```


# Layout XML Attributes

Many new Layout XML attributes control Binding and Code-Behind behavior, which
are within the `xamarin` XML namespace
(`xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools"`).
These include:

  * [`xamarin:classes`](#attr-xamarin-classes)
  * [`xamarin:managedType`](#attr-xamarin-managedType)


<a name="attr-xamarin-classes">

## `xamarin:classes`

The `xamarin:classes` XML attribute is used as part of
[Code-Behind](#resource-codebehind) to specify which types should be generated.

The `xamarin:classes` XML attribute contains a `;`-separated list of
*full class names* that should be generated.


<a name="attr-xamarin-managedType" />

## `xamarin:managedType`

The `xamarin:managedType` layout attribute is used to explicitly specify the
managed type to expose the bound ID as. If not specified, the type will be
inferred from the declaring context, e.g. `<Button/>` will result in an
`Android.Widget.Button`, and `<fragment/>` will result in an
`Android.App.Fragment`.

The `xamarin:managedType` attribute allows for more explicit type declarations.


# Managed type mapping

It is quite common to use widget names based on the Java package they come
from and, equally as often, the managed .NET name of such type will have a
different (.NET style) name in the managed land. The code generator can perform
a number of very simple adjustments to try to match the code, such as:

  * Capitalize all the components of the type namespace and name. For instance
    `java.package.myButton` would become `Java.Package.MyButton`

  * Capitalize two-letter components of the type namespace. For instance
    `android.os.SomeType` would become `Android.OS.SomeType`

  * Look up a number of hard-coded namespaces which have known mappings.
    Currently the list includes the following namespaces:

      * `android.view` -> `Android.Views`
      * `android.support.wearable.view` -> `Android.Support.Wearable.Views`
      * `com.actionbarsherlock` -> `ABSherlock`
      * `com.actionbarsherlock.widget` -> `ABSherlock.Widget`
      * `com.actionbarsherlock.view` -> `ABSherlock.View`
      * `com.actionbarsherlock.app` -> `ABSherlock.App`

  * Look up a number of hard-coded types in internal tables. Currently the list includes the following types:

      * `WebView` -> `Android.Webkit.WebView`
	
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

# Code-behind layout selection and processing

## Selection

By default all the layouts in any of the `Resource\layout*` directories are
considered for code-behind generation as long as they contain at least a
single element with the `//*/@android:id` attribute. You can disable processing
of all the layouts by setting the `$(AndroidGenerateLayoutBindings)` MSBuild
property to `False` either on the msbuild command line:

```
msbuild /p:AndroidGenerateLayoutBindings=false MyProject.csproj
```

or in your .csproj file:

```xml
<PropertyGroup>
   <AndroidGenerateLayoutBindings>false</AndroidGenerateLayoutBindings>
</PropertyGroup
```

This disables any processing of layouts as far as code-behind is concerned.
If you want to enable Code-Behind for specific files, change the `.axml`
file to have a **Build action** of `@(AndroidBoundLayout)` by editing your
`.csproj` file and replacing `AndroidResource` with `AndroidBoundLayout`:

```xml
<!-- This -->
<AndroidResource Include="Resources\layout\Main.axml" />
<!-- should become this -->
<AndroidBoundLayout Include="Resources\layout\Main.axml" />
```

## Processing

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

# Generated code

If you are interested in how the generated code looks for your layouts, please
take a look in the `obj\$(Configuration)\generated` folder in your solution
directory.
