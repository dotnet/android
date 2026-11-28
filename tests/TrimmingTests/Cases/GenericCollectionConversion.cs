#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.genericcollectionconversion
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// JNIEnv.CopyObjectArray<T> () converts each element through JavaConvert with only a runtime Type. For IList<T>,
// ICollection<T>, and IDictionary<K,V>, CoreCLR closes JavaList<T>, JavaCollection<T>, or JavaDictionary<K,V> with
// MakeGenericType () and maps FromJniHandle from the open definition onto it; NativeAOT uses SafeJavaCollectionFactory.
// The app never names the wrappers or calls their FromJniHandle (), so only JavaConvert's mapping can keep them.
// See https://github.com/dotnet/android/pull/13656.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.App;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Runtime.JavaList`1", "FromJniHandle(System.IntPtr,Android.Runtime.JniHandleOwnership)", By = Tool.Trimmer)]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Runtime.JavaCollection`1", "FromJniHandle(System.IntPtr,Android.Runtime.JniHandleOwnership)", By = Tool.Trimmer)]
[assembly: KeptMemberInAssembly ("Mono.Android", "Android.Runtime.JavaDictionary`2", "FromJniHandle(System.IntPtr,Android.Runtime.JniHandleOwnership)", By = Tool.Trimmer)]

[Instrumentation (Name = "net.dot.trimmingtests.CaseInstrumentation")]
public class CaseInstrumentation : Instrumentation
{
	// Android creates instrumentations before the runtime starts, so their peer is activated later through this constructor.
	public CaseInstrumentation (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	public override void CallApplicationOnCreate (Application? app)
	{
		base.CallApplicationOnCreate (app);
		try {
			CheckList ();
			CheckCollection ();
			CheckDictionary ();
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}

	static void CheckList ()
	{
		using var source = new Java.Util.ArrayList ();
		source.Add (new Java.Lang.String ("a"));
		source.Add (new Java.Lang.String ("b"));

		var list = ConvertSingle<IList<string>> (source, "Android.Runtime.JavaList`1");
		if (list.Count != 2 || list [0] != "a" || list [1] != "b") {
			throw new InvalidOperationException ($"Unexpected IList<string> contents: [{string.Join (", ", list)}].");
		}
	}

	static void CheckCollection ()
	{
		using var source = new Java.Util.HashSet ();
		source.Add (new Java.Lang.String ("c"));

		var collection = ConvertSingle<ICollection<string>> (source, "Android.Runtime.JavaCollection`1");
		var items = new List<string> (collection);
		if (collection.Count != 1 || items is not ["c"]) {
			throw new InvalidOperationException ($"Unexpected ICollection<string> contents: [{string.Join (", ", items)}].");
		}
	}

	static void CheckDictionary ()
	{
		using var source = new Java.Util.HashMap ();
		source.Put (new Java.Lang.String ("key"), new Java.Lang.String ("value"));

		var dictionary = ConvertSingle<IDictionary<string, string>> (source, "Android.Runtime.JavaDictionary`2");
		if (dictionary.Count != 1 || !dictionary.TryGetValue ("key", out var value) || value != "value") {
			throw new InvalidOperationException ("Unexpected IDictionary<string, string> contents.");
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
