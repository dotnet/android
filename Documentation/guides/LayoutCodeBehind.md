---
id: 11763499-79e9-4868-83e6-41f3061745d1
title: "Layout CodeBehind"
dateupdated: 2018-04-23
---

<a name="Overview" class="injected"></a>

# Overview

Xamarin.Android supports auto generation of "Code Behind" classes for Android layouts which
can then be used from within any Activity or any custom View derivative to access the views/widgets
without having to use the `FindViewById` method. All the views/widgets with the `android:id` attribute
will have a corresponding property to find and return the widget to the caller.

Code-behind generator has two modes of operation, one designed to be independent on the context in
which it is used (that is not tied to a particular Activity or View) and another, which builds upon the
former, which adds properties directly into the indicated Activity's instance.

First approach requires minimal changes to your Activity or View code but is slightly less "integraed"
with the Activity/View class, while the other requires small modifications to both the layout being
bound and the Activity that is to take advantage of the generated properties (the second approach doesn't
work with Views). 

Currently only **C#** code generator is implemented but provisions exist for future implementation of
support for other languages (such as **F#** or **VisualBasic**)

<a name="First approach" class="injected"></a>

# First approach (Standalone binding class)

Generates "binding" code, that is a stand-alone class with properties to access the layout widgets. 
All the generated classes are placed in the `Binding` namespace and their names follow the base name 
of the layout being bound. Therefore, a class for layout named `Main.axml` will be named `Binding.Main` 
and a class for layout `my_other_layout.xml` will be named `Binding.my_other_layout`. The main advantage 
of this approach is that no modifications layout files are necessary.
    
The class can be instantiated in a number of ways:
    
   * By passing instance of an activity *after* setting the activity layout in the usual way:
    
         SetContentView (Resource.Id.Main);
         var items = new Binding.Main (this)
    
   * By passing instance of a *View* to the constructor after loading the correct layout into the view:
    
         var items = new Binding.my_other_layout (some_view);
  
   * By changing the Activity layout setting code to call a new overload of the ``SetContentView`` method:
    
         var items = SetContentView<Binding.Main> ();
    
After the binding is instantiated, one can find the widgets by simply accessing the correct property:
    
    Button btn = items.MyButton;
    
Given the following layout:

```xml
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android">
   <Button android:id="@+id/myButton" />
   <fragment xamarin:managedType="CommonSampleLibrary.LogFragment"
             android:name="commonsamplelibrary.LogFragment"
             android:id="@+id/log_fragment" />
   <fragment android:name="CommonSampleLibrary.LogFragment"
             android:id="@+id/secondary_log_fragment" />
</LinearLayout>
```

Code similar to one below will be generated (some elements are removed for brevity: whitespace, line pragmas, comments 
and using statements):
    
```csharp
namespace Binding
{
  sealed class Main : global::Xamarin.Android.Design.LayoutBinding
  {
     public override int ResourceLayoutID => global::Xamarin.Android.Tests.CodeBehindBuildTests.Resource.Layout.Main;
   
     public Main (global::Xamarin.Android.Design.ILayoutBindingClient client) : base (client) {}
    
     Button __myButton;
     public Button myButton => FindView (global::Xamarin.Android.Tests.CodeBehindBuildTests.Resource.Id.myButton, ref __myButton);
    
     CommonSampleLibrary.LogFragment __log_fragment;
     public CommonSampleLibrary.LogFragment log_fragment => FindFragment (global::Xamarin.Android.Tests.CodeBehindBuildTests.Resource.Id.log_fragment, ref __log_fragment);
    
     global::Android.App.Fragment __secondary_log_fragment;
     public global::Android.App.Fragment secondary_log_fragment => FindFragment (global::Xamarin.Android.Tests.CodeBehindBuildTests.Resource.Id.secondary_log_fragment, ref __secondary_log_fragment);
    
     CommonSampleLibrary.LogFragment __tertiary_log_fragment;
     public CommonSampleLibrary.LogFragment tertiary_log_fragment => FindFragment (global::Xamarin.Android.Tests.CodeBehindBuildTests.Resource.Id.tertiary_log_fragment, ref __tertiary_log_fragment);
  }
}
```
    
The `Xamarin.Android.Design.LayoutBinding` is a new XA class added to make it possible to instantiate the binding for both an Activity and a View,
there is no use for this class in your application code it needs to be treated as part of Xamarin.Android "infrastructure". This class, however,
requires that the "client" (that is a class which will use the binding) implements the new `Xamarin.Android.Design.ILayoutBindingClient` interface
which allows to abstract out access to the `Android.App.Context` context class (required to find and access widgets in the layout) as well as
the `FindViewById<T>` generic method used to perform the search.

The interface contains a couple of methods the developer might be interested in. Both the `Activity` and the `View` classes implement the interface,
so you can override the two methods as needed:

```csharp
protected virtual void OnLayoutViewNotFound <T> (int resourceId, ref T view) where T: View;
protected virtual void OnLayoutFragmentNotFound <T> (int resourceId, ref T fragment) where T: Fragment;
```

The methods are called on your Activity/View whenever a widget/fragment with the specified ID cannot be found. Your implementation can instantiate the required 
widget/fragment or find it using any other method than `FindViewById` and then set the `view` parameter to the widget instance. If your code cannot find/instantiate 
the required widget/fragment it should not modify the `view` parameter, in which case Xamarin.Android will throw an exception.

# Second approach (Partial Activity class)

Builds on the first one and requires slightly more changes to the code. It is similar to the old approach in that it generates a partial
activity class which defines a number of properties, right in the Activity, to access the layout widgets. In order for this to work, it 
is first necessary to modify the associated layout by adding two attributes to the root element of the layout:
    
   * XML namespace declaration:

         xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools"
    
   * Specification of *full* type names for activities which will use the generated code (a semicolon-separated list, at least one type is required):
    
         xamarin:classes="Xamarin.Android.Tests.CodeBehindBuildTests.MainActivity;Xamarin.Android.Tests.CodeBehindBuildTests.AnotherMainActivity"

Second, all the activities mentioned in the `xamarin:classes` attribute have to be modified by adding the `partial` modifier to the Activity
class declaration. So 

```csharp
namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
  public class MainActivity : Activity 
  {}
}

namespace Xamarin.Android.Tests.CodeBehindBuildTests 
{
  public class AnotherMainActivity : Activity
  { }
}
```

will become 


```csharp
namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
  public partial class MainActivity : Activity 
  {}
}

namespace Xamarin.Android.Tests.CodeBehindBuildTests 
{
  public partial class AnotherMainActivity : Activity
  { }
}
```

No other changes are necessary as the code-behind overrides all the `SetContentView` methods to instantiate the correct `Binding` class and
uses it to seamlessly access the layout widgets. Widgets are accessed via standard properties, e.g.:

```csharp
Button btn = MyButton;
```

Given the same layout as in `First approach`, with the two XML attributes mentioned above added:
    
```xml
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android"
              xmlns:xamarin="http://schemas.xamarin.com/android/xamarin/tools"
              xamarin:classes="Xamarin.Android.Tests.CodeBehindBuildTests.MainActivity">
  <Button android:id="@+id/myButton" />
  <fragment xamarin:managedType="CommonSampleLibrary.LogFragment"
            android:name="commonsamplelibrary.LogFragment"
            android:id="@+id/log_fragment" />
  <fragment android:name="CommonSampleLibrary.LogFragment"
            android:id="@+id/secondary_log_fragment" />
</LinearLayout>
```

We now get the following code generated (the `Binding.Main` class remains exactly the same and still can be used independently):


```csharp
namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
   partial class MainActivity
   {
      Binding.Main __layout_binding;
    
      public override void SetContentView (global::Android.Views.View view) 
	  {
        __layout_binding = new global::Binding.Main (view);
        bool callBase = true;

        OnSetContentView (view, ref callBase);
        if (callBase)
          base.SetContentView (view);
      }
    
      public override void SetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params) 
	  {
          // Code similar to above
      }
    
      public override void SetContentView (int layoutResID) 
	  {
          // Code similar to above
      }
    
      partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn);
      partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn);
      partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn);
    
      public Button myButton => __layout_binding?.myButton;
      public CommonSampleLibrary.LogFragment log_fragment => __layout_binding?.log_fragment;
      public global::Android.App.Fragment secondary_log_fragment => __layout_binding?.secondary_log_fragment;
      public CommonSampleLibrary.LogFragment tertiary_log_fragment => __layout_binding?.tertiary_log_fragment;
  }
}
```

The `OnSetContentView` partial methods may be implemented in your code to customize `SetContentView` behavior since it's no longer possible to override
the latter in your "main" activity code. If your `OnSetContentView` requires that the base class implementation of `SetContentView` is **not** called,
it needs to set the `bool callBase` parameter to `true`, otherwise the base implementation will be called after your `OnSetContentView` method returns.

# Managed type mapping

It is quite common to use widget names based on the Java package they come from and, equally as often, the managed .NET name of such type will have
a different (.NET style) name in the managed land. The code generator can peform a number of very simple adjustments to try to match the code, such
as:

  * Capitalize all the components of the type namespace and name. For instance `java.package.myButton` would become `Java.Package.MyButton`

  * Capitalize two-letter components of the type namespace. For instance `android.os.SomeType` would become `Android.OS.SomeType`

  * Look up a number of hard-coded namespaces which have known mappings. Currently the list includes the following namespaces:
  
    * `android.view` -> `Android.Views`
    * `android.support.wearable.view` -> `Android.Support.Wearable.Views`
	* `com.actionbarsherlock` -> `ABSherlock`
    * `com.actionbarsherlock.widget` -> `ABSherlock.Widget`
    * `com.actionbarsherlock.view` -> `ABSherlock.View`
    * `com.actionbarsherlock.app` -> `ABSherlock.App`
 
  * Look up a number of hard-coded types in internal tables. Currently the list includes the following types:
  
    * `WebView` -> `Android.Webkit.WebView`
	
  * Strip number of hard-coded namespace **prefixes**. Currently the list includes the following prefixes:
  
    * `com.google.`

If, however, the above attempts fail, you will need to modify the layout which uses a widget with such an unmapped type to add both the `xamarin`
XML namespace declaration to the root element of the layout and the `xamarin:managedType` to the element requiring the mapping. For instance:

```xml
<fragment
    xamarin:managedType="CommonSampleLibrary.LogFragment"
    android:name="commonsamplelibrary.LogFragment"
    android:id="@+id/log_fragment"
    android:layout_width="match_parent"
    android:layout_height="match_parent" />
```

Will use the `CommonSampleLibrary.LogFragment` type for the native type `commonsamplelibrary.LogFragment`. 

You can avoid adding the XML namespace declaration and the `xamarin:managedType` attribute by simply naming the type using its managed name, 
for instance the above fragment could be redeclared as follows:

```xml
<fragment
    android:name="CommonSampleLibrary.LogFragment"
    android:id="@+id/secondary_log_fragment"
    android:layout_width="match_parent"
    android:layout_height="match_parent" />
```

# Code-behind layout selection and processing

## Selection

By default all the layouts in any of the `Resource/layout*` directories are considered for code-behind generation as long as they contain at least a
single element with the `<android:id/>` attribute. You can disable processing of all the layouts by setting the `AndroidGenerateLayoutBindings` MSBuild
property to `false` either on the msbuild command line:

```
msbuild /p:AndroidGenerateLayoutBindings=false MyProject.csproj
```

or in your .csproj file:

```xml
<PropertyGroup>
   <AndroidGenerateLayoutBindings>false</AndroidGenerateLayoutBindings
</PropertyGroup
```

This disables any processing of layouts as far as code-behind is concerned, so in order to get the code generated for a particular layout (or layouts)
you need to edit your .csproj file, find the line which includes the layout:

```xml
<AndroidResource Include="Resources\layout\Main.axml" />
```

and change its type to `AndroidBoundLayout`:

```xml
<AndroidBoundLayout Include="Resources\layout\Main.axml" />
```

## Processing

Layouts are grouped by name, with like-named templates from **different** `Resource/layout*` directories
comprising a single group. Such groups are processed as if they were a single layout. It is possible that in such case there will be a type clash between
two widgets found in different layotus belonging to the same group. In such case the generated property will not be able to have the exact widget type, but
rather a "decayed" one. Decaying follows the algorithm below:

   1. If all of the conflicting widgets are `View` derivatives, the property type will be `Android.Views.View`
   2. If all of the conflicting types are `Fragment` derivatives, the property type will be `Android.App.Fragment`
   3. If the conflicting widgets contain both a `View` and a `Fragment`, the property type will be `global::System.Object`

# Generated code

If you are interested in how the generated code looks for your layouts, please take a look in the `obj/$(Configuration)/generated` folder in your
solution directory.
