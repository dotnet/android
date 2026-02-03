using Android.App;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace HelloWorld;

[Activity (Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
	const string TAG = "HelloWorld";

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		try {
			Log.Info (TAG, "MainActivity.OnCreate START");
			base.OnCreate (savedInstanceState);
			Log.Info (TAG, "After base.OnCreate");
			SetContentView (Resource.Layout.Main);
			Log.Info (TAG, "After SetContentView");

			// Test array API - this should trigger the array TypeMap lookup
			TestArrayAPIs ();

			Log.Info (TAG, "MainActivity.OnCreate SUCCESS!");
		} catch (System.Exception ex) {
			Log.Error (TAG, $"Exception in OnCreate: {ex}");
		}
	}

	void TestArrayAPIs ()
	{
		Log.Info (TAG, "TestArrayAPIs START");

		// Test 1: String array (primitive array type) - skip ViewGroup test for now
		string[] strings = new string[] { "Hello", "World", "TypeMap", "V3" };
		Log.Info (TAG, $"String array: {string.Join (", ", strings)}");

		// Test 2: Create a Java array from managed and pass it back
		var javaStrings = new Java.Lang.String[] {
			new Java.Lang.String ("Java"),
			new Java.Lang.String ("String"),
			new Java.Lang.String ("Array")
		};
		Log.Info (TAG, $"Java.Lang.String[] length: {javaStrings.Length}");
		foreach (var s in javaStrings) {
			Log.Info (TAG, $"  Java.Lang.String: {s}");
		}

		// Test 3: Get threads from ThreadGroup - returns Thread[]
		try {
			var threadGroup = Java.Lang.Thread.CurrentThread ()?.ThreadGroup;
			if (threadGroup != null) {
				int activeCount = threadGroup.ActiveCount ();
				var threads = new Java.Lang.Thread[activeCount];
				int count = threadGroup.Enumerate (threads);
				Log.Info (TAG, $"ThreadGroup.Enumerate: {count} threads");
				for (int i = 0; i < count; i++) {
					Log.Info (TAG, $"  Thread[{i}]: {threads[i]?.Name}");
				}
			}
		} catch (System.Exception ex) {
			Log.Error (TAG, $"ThreadGroup test failed: {ex}");
		}

		// Test 4: TrustManagerFactory.GetTrustManagers() - returns ITrustManager[]
		// NOTE: This may fail because Android returns internal types like RootTrustManager
		// that don't have bindings, causing them to be wrapped as Java.Lang.Object
		// which can't be stored in an ITrustManager[] array.
		try {
			var tmf = Javax.Net.Ssl.TrustManagerFactory.GetInstance (Javax.Net.Ssl.TrustManagerFactory.DefaultAlgorithm);
			tmf?.Init ((Java.Security.KeyStore?) null);
			var trustManagers = tmf?.GetTrustManagers ();
			if (trustManagers != null) {
				Log.Info (TAG, $"TrustManagers: {trustManagers.Length} items");
				foreach (var tm in trustManagers) {
					Log.Info (TAG, $"  TrustManager: {tm?.GetType ().Name}");
				}
			} else {
				Log.Info (TAG, "TrustManagers: null");
			}
		} catch (System.Exception ex) {
			Log.Error (TAG, $"TrustManager test failed (expected for internal Android types): {ex.Message}");
		}

		Log.Info (TAG, "TestArrayAPIs SUCCESS!");
	}
}
