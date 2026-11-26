#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.genericpeerarrayelement
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// JNIEnv.GetArray<T> () reaches TrimmableTypeMap.CreateInstance () with only a runtime Type. For a closed generic
// peer the runtime needs the (IntPtr, JniHandleOwnership) constructor, which nothing else in this app keeps.
// The generated proxy of the open generic definition returns it from GetGenericTargetTypeDefinition (), annotated to keep
// its constructors, and the runtime maps that constructor onto the closed type (#13721). That must not keep generic
// peers the app doesn't use, nor make NativeAOT compile instantiations the app doesn't use.

using System;
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

// ILLink keeps or removes generic definitions, so only NativeAOT can check instantiations.
[assembly: KeptTypeInAssembly ("GenericPeerArrayElement", "GenericArrayElement`1<System.Int32>", By = Tool.NativeAot)]
[assembly: KeptTypeInAssembly ("GenericPeerArrayElement", "GenericArrayElement`1<System.String>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("GenericPeerArrayElement", "GenericArrayElement`1<System.Int64>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("GenericPeerArrayElement", "GenericArrayElement`1<System.Object>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("GenericPeerArrayElement", "GenericArrayElement`1<MainActivity>", By = Tool.NativeAot)]

[Activity (Label = "GenericPeerArrayElement", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			Check ("managed state");
			Check (42);
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}

	static void Check<T> (T value)
	{
		IntPtr array;
		using (var element = new GenericArrayElement<T> { Value = value }) {
			array = JNIEnv.NewObjectArray (1, element.Class.Handle, element.Handle);
		}
		try {
			var items = JNIEnv.GetArray<GenericArrayElement<T>> (array);
			if (items is not { Length: 1 } || items [0] is not { } item) {
				throw new InvalidOperationException ($"Unable to convert the Java array element to {typeof (GenericArrayElement<T>)}.");
			}
			if (item.GetType () != typeof (GenericArrayElement<T>)) {
				throw new InvalidOperationException ($"Expected {typeof (GenericArrayElement<T>)}, got {item.GetType ()}.");
			}
			// The original peer was disposed, so this must be a new peer from the activation constructor, without its managed state.
			if (!EqualityComparer<T>.Default.Equals (item.Value, default)) {
				throw new InvalidOperationException ($"Expected a newly activated peer, but Value is '{item.Value}'.");
			}
			item.Dispose ();
		} finally {
			JNIEnv.DeleteLocalRef (array);
		}
	}
}

public class GenericArrayElement<T> : Java.Lang.Object
{
	public GenericArrayElement ()
	{
	}

	[Kept]
	protected GenericArrayElement (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	public T? Value { get; set; }
}

// A generic peer the app never uses, with the same shape: its proxy, and so its constructor annotation, must not keep it.
[Removed]
public class UnusedGenericElement<T> : Java.Lang.Object
{
	public UnusedGenericElement ()
	{
	}

	protected UnusedGenericElement (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	public T? Value { get; set; }
}
