#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.interfaceelementcollections
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/11770: Java collections whose element type is a bound
// interface (JavaList<ICharSequence>) need a container factory from the interface's proxy; without one the runtime
// fell back to MakeGenericType, which NativeAOT doesn't support.

using System;
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Java.Lang;

[Activity (Label = "InterfaceElementCollections", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			var bundle = new Bundle ();
			bundle.PutCharSequenceArrayList ("key", new List<ICharSequence> { new Java.Lang.String ("value") });
			var list = bundle.GetCharSequenceArrayList ("key") ?? throw new InvalidOperationException ("The list is missing.");
			if (list.Count != 1 || list [0]?.ToString () != "value") {
				throw new InvalidOperationException ($"The list round-tripped as [{string.Join (", ", list)}].");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (System.Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
