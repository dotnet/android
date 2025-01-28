using Android.Runtime;
using Android.Util;

/// <summary>
/// NOTE: This class is not required, but used for testing Android.App.Application subclasses.
/// </summary>
[Register ("my/MainApplication")] // Required for typemap in NativeAotTypeManager
[Application]
public class MainApplication : Application
{
    public MainApplication (IntPtr handle, JniHandleOwnership transfer)
        : base (handle, transfer)
    {
    }

    public override void OnCreate ()
    {
        Log.Debug ("NativeAOT", "Application.OnCreate()");

        base.OnCreate ();
    }
}
