using Android.Runtime;
using Android.Util;

/// <summary>
/// NOTE: This class is not required, but used for testing Android.App.Application subclasses.
/// Name required for typemap in NativeAotTypeManager
/// </summary>
[Application (Name = "my.MainApplication")]
public class MainApplication : Application
{
    public MainApplication (IntPtr handle, JniHandleOwnership transfer)
        : base (handle, transfer)
    {
        Log.Debug ("NativeAOT", $"Application..ctor({handle.ToString ("x2")}, {transfer})");
    }

    public override void OnCreate ()
    {
        Log.Debug ("NativeAOT", "Application.OnCreate()");

        base.OnCreate ();

        AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
            Console.WriteLine ("AppDomain.UnhandledException!");
            Console.WriteLine ($"  sender: {sender} [{sender != null} {sender?.GetType ()}]");
            Console.WriteLine ($"  e.IsTerminating: {e.IsTerminating}");
            Console.WriteLine ($"  e.ExceptionObject: {e.ExceptionObject}");
        };
    }
}
