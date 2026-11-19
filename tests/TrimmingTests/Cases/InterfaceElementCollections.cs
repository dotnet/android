#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.interfaceelementcollections
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/issues/11770: converting a Java list to a list of a bound interface
// type (IList<ICharSequence>) needs a container factory for that element type; without one the runtime fell back to
// MakeGenericType, which NativeAOT doesn't support. Bindings such as Bundle.GetCharSequenceArrayList () construct
// JavaList<ICharSequence> themselves, so this case lets JavaConvert find the converter: the elements of a
// JavaList<IList<ICharSequence>> are converted from a runtime Type.

using System;
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Android.Runtime;
using Java.Lang;

[Activity (Label = "InterfaceElementCollections", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			using var inner = new Java.Util.ArrayList ();
			inner.Add (new Java.Lang.String ("value"));
			using var outer = new Java.Util.ArrayList ();
			outer.Add (inner);
			using var lists = new JavaList<IList<ICharSequence>> (outer.Handle, JniHandleOwnership.DoNotTransfer);
			var list = lists [0] ?? throw new InvalidOperationException ("The inner list is missing.");
			if (list.Count != 1 || list [0]?.ToString () != "value") {
				throw new InvalidOperationException ($"The list was converted as [{string.Join (", ", list)}].");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (System.Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
