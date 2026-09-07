using System;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;

using Java.InteropTests;

namespace ${ROOT_NAMESPACE};

[Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (Label = "${PROJECT_NAME}", MainLauncher = true)]
public class MainActivity : Activity
{
	const string ResultPrefix = "INTERFACE_COLLECTION_ROOTING_RESULT";
	const string ResultToken = "${RESULT_TOKEN}";
	const string Tag = "InterfaceCollections";

	protected override void OnCreate (Bundle savedInstanceState)
	{
		base.OnCreate (savedInstanceState);

		try {
			using var holder = new RawInterfaceCollectionHolder ();
			using var list = (IDisposable) holder.CreateList ();
			using var collection = (IDisposable) holder.CreateCollection ();
			using var dictionary = (IDisposable) holder.CreateInterfaceDictionary ();
			Log.Info (Tag, $"{ResultPrefix} PASS {ResultToken}");
		} catch (Exception e) {
			Log.Error (Tag, $"{ResultPrefix} FAIL {ResultToken}: {e}");
		} finally {
			Finish ();
		}
	}
}
