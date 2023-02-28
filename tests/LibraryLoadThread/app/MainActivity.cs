using System.Runtime.InteropServices;

using Android.App;
using Android.OS;
using Java.Lang;

using BCLThread = global::System.Threading.Thread;
using BCLThreadStart = global::System.Threading.ThreadStart;

namespace LibraryLoadThread;

[Activity (Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
	BCLThread? thread;

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);

		// Set our view from the "main" layout resource
		SetContentView (Resource.Layout.activity_main);

		thread = new BCLThread (new BCLThreadStart (LoadLibraryInThread));
		thread.Start ();
	}

	[DllImport ("thread-load")]
	static extern void HelloWorld (string from);

	static void LoadLibraryInThread ()
	{
		JavaSystem.LoadLibrary ("thread-local");
		HelloWorld (nameof (LoadLibraryInThread));
	}
}
