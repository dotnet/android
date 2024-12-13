using Android.Runtime;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Java.Interop.Samples.NativeAotFromAndroid;

[Register("my/MainActivity")] // Required for typemap in NativeAotTypeManager
[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);
    }

    [UnmanagedCallersOnly(EntryPoint = "Java_my_MainActivity_n_1onCreate")]
    static void n_OnCreate(IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
    {
        try {
            var method = typeof (Activity).GetMethod ("GetOnCreate_Landroid_os_Bundle_Handler", BindingFlags.NonPublic | BindingFlags.Static);
            ArgumentNullException.ThrowIfNull (method);

            var handler = method.Invoke (null, null) as Delegate;
            ArgumentNullException.ThrowIfNull (handler);

            handler.DynamicInvoke ([ jnienv, native__this, native_savedInstanceState ]);
        } catch (Exception exc) {
            AndroidLog.Print (AndroidLogLevel.Error, "MainActivity", $"n_OnCreate() failed: {exc}");
        }
    }
}