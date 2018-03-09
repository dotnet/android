---
id: 11763499-79e9-4868-83e6-41f3061745d1
title: "Layout CodeBehind"
dateupdated: 2018-01-29
---

<a name="Overview" class="injected"></a>

# Overview

Xamarin.Android supports the auto generation of "Code Behind" classes. These classes
can reduce the amount code a developer writes. You can end up replacing code like

```csharp
SetContentView (Resource.Layout.Main);
var button = FindViewById<Button> (Resource.Id.myButton);
   button.Click += delegate {
};
```

with

```csharp
InitializeContentView ();
myButton.Click += delegate {
};
```

or, with nested layouts:

```csharp
InitializeContentView ();
myParentLayout.myButton.Widget.Click += delegate {
};
```

<a name="" class="injected"/></a>

# Preparing to use Code Behind

In order to make use of this new feature there are a few changes which are required. 
An `axml/xml` file that you want to associate with an activity needs to be modified to 
include a few extra xml attributes on the root layout element. 

Additionally, **only** elements which have the `android:id` attribute will be accessible via 
the generated code.


```xml
xmlns:tools="http://schemas.xamarin.com/android/tools"
tools:class="$(Namespace).$(ClassName)"
```

The `class` attribute defines the Namespace and ClassName of the code which will be
generated. For example if you have a layout for your `MainActivity` you would set
the `tools:class` to `MyAppNamespace.MainActivity`. Note it should be the fully
qualified name, not just the class name on its own.

The next thing we need to do is to make the `MainActivity` a `partial` class. This
allows the genereted code to extend the current class which you have written.
So 

```csharp
public class MainActivity : Activity {
}
```

will become 

```csharp
public partial class MainActivity : Activity {
}
```

You then need to make sure you initialize the layout properties by calling
`InitializeContentView ()` in the `OnCreate()` method of your activity.

```csharp
protected override void OnCreate (Bundle bundle)
{
   base.OnCreate (bundle);
   InitializeContentView ();
}
```

For those of you familiar with System.Windows.Forms this is akin
to `InitializeComponent`. Once this has been done you can now access
your layout items via the properties.

```csharp
myButton.Click += delegate {
};
```

There is a partial method available which can be implemented to handle
situations where the View is not found. The method is

```csharp
void OnLayoutViewNotFound<T> (int resourceId, ref T type)
   where T : global::Android.Views.View;
```

If `FindViewById` returns `null` then the `OnLayoutViewNotFound` method
will be called (if it is implemented). This is done BEFORE we throw the
`InvalidOperationException`. This allows the deveoper to handle the 
situation in a manner which fits the app they are writing. For example
you might want to switch to a backup view, or just log some additional
diagnostic information.

Another partial method exists to handle fragments:

```csharp
void OnLayoutFragmentNotFound<T> (int resourceId, ref T type)
  where T : global::Android.App.Fragment;
```

It works in exactly the same way as `OnLayoutViewNotFound` above, just for fragments.

## Generated code structure

The generated code-behind is laid out in a hierarchical fashion, reflecting the parent-child
relationship found in the layout file. The way it is done is that each element which has any
child elements **with** the `android:id` attribute (that is, ones which will also have code
generated for them) will have a nested class generated for it which will have a property for
each child element as well as the `Widget` property which refers to this element's actual 
Android widget/view. Each element which does **not** have any child elements with the 
`android:id` attribute, however, will become a *leaf node* and will have an associated property
in its parent widget's class directly typed to the actual Android type (e.g. `TextView`) instead
of the class described before. For instance, given this layout:

```xml
<LinearLayout xmlns:android="http://schemas.android.com/apk/res/android" xmlns:tools="http://schemas.xamarin.com/android/tools"
   tools:class="MyActivity">
   <ScrollView android:id="@+id/myScrollView">
      <TextView android:id="@+id/myTextView"/>
   </ScrollView>
</LinearLayout>
```

The code-behind will have this rough structure (class names are different to keep the documentation clear):

```csharp
myScrollView_Class myScrollView { 
   get { return new myScrollView_Class (this); }
}

class myScrollView_Class 
{
   public ScrollView Widget { get; }
   public TextView myTextView { get; }
	
   public myScrollView_Class (MyActivity parent) {}
}
```

So in order to access the widgets you'd use code similar to:

```csharp
InitializeContentView ();
myScrollView.Widget.Fling (100);
myScrollView.myTextView.AutoSizeMaxTextSize = 40;
```

### Code structure rationale

It may seem that it would be simpler to generate code which would put properties returning the layout elements directly in the
Activity partial class instead of outputing a seemingly complex nested class structure. This approach would work if it wasn't
for the following:

  1. Android allows duplicate `android:id` values for **sibling** elements
  2. Android allows duplicate `android:id` values anywhere within the layout tree
  3. Many layouts reuse XML in the form of fragments
  4. Many layouts reuse XML in the form of includes (using the `<include>` element)

**(1)** means that there's direct access (via `FindViewById` or with code-behind) to the **first** element with that `id` **only**. 
The rest of elements can be accessed only by enumerating the child collection. This is how it works in Android and we do not deviate 
from the Android approach currently.

**(2)** works in Android by walking down the element hierarchy (using `FindViewById`) until we find the direct parent of the element we seek and,
despite being tedious, this approach creates no conflicts and issues with accessing the elements with the same `id`

If we "flattened" the hierarchy, however, we would create the issue ourselves as suddenly we'd have `id` conflicts where there would have been
none before. Additionally, we wouldn't be able to generate code to directly access the farther elements, similar to `1.`. Or we could but we would
have to come up with a scheme to generate unique names for our properties for instance by appending a monotonously increasing integer to the base name,
e.g. given the base `id` of `myTextView` we would have properties named `myTextView`, `myTextView1`, `myTextView2` and so on.

It may not seem to be a big problem, after all there's a clearly defined naming convention that is predictable. But, is it? What happens if one element
with the shared `id` in the middle is removed? The elements following it are renumbered and suddenly our code works subtly differently - where `myTextView2`
was used to refer to the 3rd control, now it not only does not exist (causing a build error for the **third** instance of the element) but it is now silently
referred to by `myTextView1` which might again introduce subtle issues to the way the code works.

What happens when the layouts containing the "duplicate" `id`s are reordered? We have no compilation error as in the scenario above, it's much worse - suddenly
and quietly the code works differently, because the properties refer to **different** widgets (and thus layouts) but with the same `id`s!

**(3)** and **(4)** make the situation worse as they can introduce a number of "duplicate" `id` values all over the place and cause the **(1)** and **(2)** issues.

The hierarchical approach generates code that's inherently object-oriented, reflects the structure of the layout and in case of removing of elements will 
generate a compile-time error, while in case of reordering of elements it will keep working correctly as long as the `id` "path" doesn't change (i.e. the 
involved elements keep their `id` values from the root all the way to the leaf child). The only slightly awkward aspect is the necessity to introduce the
`Widget` property in each wrapper class in order to enable referring to the element itself and not just its children. However, since the usage and naming is
consistent, this is simply a matter of getting used to the convention.

## Managed types

By default each element for which we generate code-behind has its managed type set to
its local name, for instance 

```xml
<TextView android:id=""@+id/textView" />
```

Will generate a property named `textView` of type `TextView`. It works fine in most cases
but sometimes you might find code which either refers to a custom widget using the package
name or a fragment which uses the case-insensitive `android:name` attribute syntax, for
instance:

```xml
<fragment
   android:name="commonsamplelibrary.LogFragment"
   android:id="@+id/log_fragment" />
```

In this case the generated property would have the managed type `commonsamplelibrary.LogFragment`, 
however the actual managed type fully qualified name is `CommonSampleLibrary.LogFragment` and thus
the generated code would fail to compile. The solution is to add the `tools:managedType` attribute
which specifies the element's (all elements support this attribute) managed type.

One may wonder why we didn't reuse the `tools:class` attribute to specify the managed type? It is
because that attribute is used to specify the code-behind partial class name on the root element of
the layout and should the element had the `android:id` attribute present we'd end up with generated 
code that would use the **activity** type for the element's associated property instead of its
actual type and the code wouldn't build.

# How it works

There are a couple of new MSBuild Tasks which generate the code behind.
`<CalculateLayoutCodeBehind/>` and `<GenerateCodeBehindForLayout/>`. 

`<CalculateLayoutCodeBehind/>`  scans through the `@(AndroidResources)` of the 
project looking fo the `tools:class` attributes. Any layout file which does
not have this on the very first element will be ignored. 

`<GenerateCodeBehindForLayout/>` will then process the discovered files and 
geneerate the code behind files in  `$(IntermediateOutputDir)generated`. 
These files are named by combining the name of the layout file along with
the Namespace and ClassName from the `tools:class` attribute. So if we are
creating code behind for `MyAppNamespace.MainActivity` for  the `Main.axml`
you will see an intermediate file named `Main-MyAppNamespace.MainActivity.g.cs`.
Thes files will automatically be included in the `Compile` MSBuild ItemGroup if it
exists.
