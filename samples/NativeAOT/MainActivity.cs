using Android.Content.Res;
using Android.Runtime;
using Android.Util;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NativeAOT;

// Name required for typemap in NativeAotTypeManager
[Activity (Label = "@string/app_name", MainLauncher = true, Name = "my.MainActivity")]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Log.Debug ("NativeAOT", "MainActivity.OnCreate()");

        base.OnCreate(savedInstanceState);

        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);

        // An example of an Android API that uses a Java array
        var list = new ColorStateList (new int[][] { [ 0, 1 ]}, [0, 1]);
        Log.Debug ("NativeAOT", "MainActivity.OnCreate() ColorStateList: " + list);

        var t = Intent?.Extras?.GetBoolean ("throw") ?? false;
        if (t) {
            throw new InvalidOperationException ("What happened?");
        }

        // This tests loading and initializing JNI shared libraries
        var hashData = new byte[32];
        SHA256.HashData(Encoding.ASCII.GetBytes("Hello World"), hashData.AsSpan());
    }
}