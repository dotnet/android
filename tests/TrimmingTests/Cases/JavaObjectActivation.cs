#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javaobjectactivation
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Java returns a java.lang.Integer typed as java.lang.Object, so the runtime has to activate the most derived binding,
// Java.Lang.Integer, through the type map. Unrelated numeric bindings and unused members are removed.

using System;
using Android.App;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Java.Lang.Integer")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Java.Lang.Integer",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "ValueOf(System.Int32)", "IntValue()")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Java.Util.ArrayList", "Get(System.Int32)")]

[assembly: RemovedMemberInAssembly ("Mono.Android", "Java.Lang.Integer", "ToHexString(System.Int32)", "Reverse(System.Int32)")]
[assembly: RemovedMemberInAssembly ("Mono.Android", "Java.Util.ArrayList", "EnsureCapacity(System.Int32)", "TrimToSize()")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Math.BigDecimal")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Java.Util.Concurrent.Atomic.AtomicInteger")]

[Activity (Label = "JavaObjectActivation", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			var list = new Java.Util.ArrayList ();
			list.Add (Java.Lang.Integer.ValueOf (42));
			var item = list.Get (0);
			if (item is not Java.Lang.Integer value || item.GetType () != typeof (Java.Lang.Integer) || value.IntValue () != 42) {
				throw new InvalidOperationException ($"ArrayList.get returned {item?.GetType ()}.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
