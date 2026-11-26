#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.valuetypecollectionconversion
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// JavaConvert converts Java collections to collections of value types from a runtime Type. NativeAOT uses
// SafeJavaCollectionFactory: a dictionary with one value-type and one reference-type argument gets its activation
// constructor from the JavaDictionary<IJavaPeerable, T> or JavaDictionary<T, IJavaPeerable> exemplar, mapped onto the
// requested type with GetMemberWithSameMetadataDefinitionAs (). CoreCLR closes the wrappers with MakeGenericType ().
// The app never names the wrappers. string/int is avoided because Mono.Android keeps JavaDictionary<string, int> anyway.
// See https://github.com/dotnet/android/pull/13723 and https://github.com/dotnet/android/pull/13724.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[Activity (Label = "ValueTypeCollectionConversion", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			CheckReferenceKeyDictionary ();
			CheckReferenceValueDictionary ();
			CheckList ();
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}

	static void CheckReferenceKeyDictionary ()
	{
		using var source = new Java.Util.HashMap ();
		source.Put (new Java.Lang.String ("pi"), Java.Lang.Double.ValueOf (3.5));

		var dictionary = ConvertSingle<IDictionary<string, double>> (source, "Android.Runtime.JavaDictionary`2");
		if (dictionary.Count != 1 || !dictionary.TryGetValue ("pi", out var value) || value != 3.5) {
			throw new InvalidOperationException ("Unexpected IDictionary<string, double> contents.");
		}
	}

	static void CheckReferenceValueDictionary ()
	{
		using var source = new Java.Util.HashMap ();
		source.Put (Java.Lang.Long.ValueOf (7L), new Java.Lang.String ("seven"));

		var dictionary = ConvertSingle<IDictionary<long, string>> (source, "Android.Runtime.JavaDictionary`2");
		if (dictionary.Count != 1 || !dictionary.TryGetValue (7L, out var value) || value != "seven") {
			throw new InvalidOperationException ("Unexpected IDictionary<long, string> contents.");
		}
	}

	static void CheckList ()
	{
		using var source = new Java.Util.ArrayList ();
		source.Add (Java.Lang.Integer.ValueOf (1));
		source.Add (Java.Lang.Integer.ValueOf (2));

		var list = ConvertSingle<IList<int>> (source, "Android.Runtime.JavaList`1");
		if (list.Count != 2 || list [0] != 1 || list [1] != 2) {
			throw new InvalidOperationException ($"Unexpected IList<int> contents: [{string.Join (", ", list)}].");
		}
	}

	// Names the expected wrapper by string so the check doesn't root it.
	static T ConvertSingle<
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		T> (Java.Lang.Object source, string expectedDefinition) where T : class
	{
		IntPtr array = JNIEnv.NewObjectArray (1, source.Class.Handle, source.Handle);
		try {
			var items = new T [1];
			JNIEnv.CopyObjectArray (array, items);
			if (items [0] is not { } item) {
				throw new InvalidOperationException ($"Unable to convert the Java array element to {typeof (T)}.");
			}
			var type = item.GetType ();
			if (!type.IsGenericType || type.GetGenericTypeDefinition ().FullName != expectedDefinition) {
				throw new InvalidOperationException ($"Expected {expectedDefinition} for {typeof (T)}, got {type}.");
			}
			return item;
		} finally {
			JNIEnv.DeleteLocalRef (array);
		}
	}
}
