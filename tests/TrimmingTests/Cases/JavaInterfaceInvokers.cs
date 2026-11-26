#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.javainterfaceinvokers
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Java returns objects the app only knows by a bound interface, so the runtime activates the interface's invoker.
// The invokers and the members the app calls are kept; the interface's other members and unrelated invokers are removed.

using System;
using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesInvoker")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesInvoker",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "Edit()", "GetString(System.String,System.String)")]
[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesEditorInvoker")]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesEditorInvoker",
	".ctor(System.IntPtr,Android.Runtime.JniHandleOwnership)", "PutString(System.String,System.String)", "Commit()")]

[assembly: RemovedMemberInAssembly ("Mono.Android", "Android.Content.ISharedPreferences",
	"GetInt(System.String,System.Int32)", "Contains(System.String)")]
[assembly: RemovedMemberInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesInvoker",
	"GetInt(System.String,System.Int32)", "Contains(System.String)")]
[assembly: RemovedMemberInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesEditorInvoker",
	"PutInt(System.String,System.Int32)", "Clear()")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Content.ISharedPreferencesOnSharedPreferenceChangeListenerInvoker")]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Text.ITextWatcherInvoker")]

[Activity (Label = "JavaInterfaceInvokers", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			var preferences = GetSharedPreferences ("trimming-tests", FileCreationMode.Private)
				?? throw new InvalidOperationException ("GetSharedPreferences returned null.");
			var editor = preferences.Edit ()?.PutString ("key", "value") ?? throw new InvalidOperationException ("Edit returned null.");
			if (!editor.Commit ()) {
				throw new InvalidOperationException ("Commit failed.");
			}
			var value = preferences.GetString ("key", null);
			if (value != "value") {
				throw new InvalidOperationException ($"GetString returned '{value}'.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
