#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.valuetypedictionaryinstantiations
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/12793: generic virtual dispatch in the value-type dictionary
// factories made ILC compile all 18×18 JavaDictionary<TKey,TValue> value-type instantiations in every NativeAOT app. Only
// the instantiation the app uses may be compiled. ILLink keeps or removes generic definitions, so this is NativeAOT only.

using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("Mono.Android", "Android.Runtime.JavaDictionary`2<System.Int32,System.Int64>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Runtime.JavaDictionary`2<System.Int64,System.Int32>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Runtime.JavaDictionary`2<System.Double,System.Boolean>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("Mono.Android", "Android.Runtime.JavaDictionary`2<System.Byte,System.Char>", By = Tool.NativeAot)]

[Activity (Label = "ValueTypeDictionaryInstantiations", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			using var dictionary = new JavaDictionary<int, long> { [1] = 2 };
			if (dictionary [1] != 2) {
				throw new InvalidOperationException ($"The dictionary returned {dictionary [1]}.");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
