---
id: 11763499-79e9-4868-83e6-41f3061745d1
title: "Layout CodeBehind"
dateupdated: 2018-01-29
---

<a name="Overview" class="injected"></a>

# Overview

Xamarin.Android supports the auto generation of "Code Behind" classes. These classes
can reduce the amount code a developer writes. You can end up replacing code like

    SetContentView (Resource.Layout.Main);
    var button = FindViewById<Button> (Resource.Id.myButton);
        button.Click += delegate {
    };

with

    InitializeContentView ();
    myButton.Click += delegate {
    };


<a name="" class="injected"/></a>

# Preparing to use Code Behind

In order to make use of this new feature there are a few changes which are required. 
An axml/xml file that you want to associate with an activity needs to be modified to 
include a few extra xml attributes on the root layout element.

    xmlns:tools="http://schemas.xamarin.com/android/tools"
    tools:class="$(Namespace).$(ClassName)"

The `class` attribute defines the Namespace and ClassName of the code which will be
generated. For example if you have a layout for your `MainActivity` you would set
the `tools:class` to `MyAppNamespace.MainActivity`. Note it should be the fully
qualified name, not just the class name on its own.

The next thing we need to do is to make the `MainActivity` a `partial` class. This
allows the genereted code to extend the current class which you have written.
So 
    public class MainActivity : Activity {
    }

will become 

    public partial class MainActivity : Activity {
    }

You then need to make sure you initialize the layout properties by calling
`InitializeContentView ()` in the `onCreate` method of your activity.

    protected override void OnCreate (Bundle bundle)
    {
        base.OnCreate (bundle);
        InitializeContentView ();
    }

For those of you familiar with System.Windows.Forms this is akin
to `InitializeComponent`. Once this has been done you can now access
your layout items via the properties.

    myButton.Click += delegate {
    };

There is a partial method available which can be implemented to handle
situations where the View is not found. The method is

    void OnLayoutViewNotFound<T> (int resourceId, ref T type) where T : global::Android.Views.View;

If `FindViewById` returns `null` then the `OnLayoutViewNotFound` method
will be called (if it is implemented). This is done BEFORE we throw the
`InvalidOperationException`. This allows the deveoper to handle the 
situation in a manner which fits the app they are writing. For example
you might want to switch to a backup view, or just log some additional
diagnostic information.

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