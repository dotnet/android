#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javascriptinterfaceannotation
#:property SupportedOSPlatformVersion=26
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/12542 (fixed by https://github.com/dotnet/android/pull/12549):
// the trimmable type map's Java callable wrappers dropped C# attributes that map to Java annotations, so WebView no longer
// saw @JavascriptInterface and silently stopped calling the bridge.

using System;
using Android.App;
using Android.OS;
using Android.Webkit;
using Java.Interop;

[Activity (Label = "JavascriptInterfaceAnnotation", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			using var bridge = new Bridge ();
			var method = bridge.Class.GetMethod ("postMessage", Java.Lang.Class.ForName ("java.lang.String"))
				?? throw new InvalidOperationException ("The Java callable wrapper has no postMessage (String) method.");
			if (!method.IsAnnotationPresent (Java.Lang.Class.ForName ("android.webkit.JavascriptInterface"))) {
				throw new InvalidOperationException ("postMessage (String) is not annotated with @JavascriptInterface.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}

class Bridge : Java.Lang.Object
{
	[JavascriptInterface]
	[Export ("postMessage")]
	public void PostMessage (string? message) => Console.WriteLine (message);
}
