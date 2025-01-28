using Android.Runtime;
using Android.Util;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NativeAOT;

[Register("my/MainActivity")] // Required for typemap in NativeAotTypeManager
[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Log.Debug ("NativeAOT", "MainActivity.OnCreate()");

        base.OnCreate(savedInstanceState);

        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);
    }
}