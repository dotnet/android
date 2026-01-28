using Android.App;
using Android.OS;
using Android.Util;

namespace HelloWorld;

[Activity (Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
	const string TAG = "HelloWorld";

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		SetContentView (Resource.Layout.Main);

		// Test object array callback - Java calls our DoInBackground with Object[] params
		TestObjectArrayCallback ();
	}

	void TestObjectArrayCallback ()
	{
		Log.Info (TAG, "=== Testing Object[] callback from Java to C# ===");

		// Create custom AsyncTask that receives Object[] from Java
		var task = new MyAsyncTask ();

		// Execute with string parameters - Java will call DoInBackground with these as Object[]
		task.Execute ("Hello", "World", "From", "Java");

		Log.Info (TAG, "AsyncTask executed, check logs for callback results");
	}
}

// Custom AsyncTask that overrides DoInBackground - Java calls this with Object[] params
#pragma warning disable CS0618 // AsyncTask is obsolete
[Android.Runtime.Register ("crc649c7c8361742aa92e/MyAsyncTask")]
public class MyAsyncTask : AsyncTask
{
	const string TAG = "MyAsyncTask";

	protected override Java.Lang.Object? DoInBackground (params Java.Lang.Object[]? @params)
	{
		Log.Info (TAG, $"DoInBackground called with {@params?.Length ?? 0} parameters:");

		if (@params != null) {
			for (int i = 0; i < @params.Length; i++) {
				var p = @params[i];
				Log.Info (TAG, $"  param[{i}]: {p} (type: {p?.GetType ().Name ?? "null"})");
			}
		}

		// Return a result
		return new Java.Lang.String ($"Processed {@params?.Length ?? 0} items");
	}

	protected override void OnPostExecute (Java.Lang.Object? result)
	{
		Log.Info (TAG, $"OnPostExecute: result = {result}");
		Log.Info (TAG, "=== Object[] callback test completed ===");
	}
}
#pragma warning restore CS0618
