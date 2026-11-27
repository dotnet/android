#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.genericpeercollectioncopyto
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Non-generic JavaCollection/JavaList.CopyTo () activate closed generic peers using a runtime Type. The runtime
// reflects over the (IntPtr, JniHandleOwnership) constructor, which nothing else in this app keeps. The generated
// proxy of the open generic definition keeps that constructor with [DynamicDependency]; that must not keep generic
// peers the app doesn't use, nor make NativeAOT compile instantiations the app doesn't use.

using System;
using System.Collections.Generic;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

// ILLink keeps or removes generic definitions, so only NativeAOT can check instantiations.
[assembly: KeptTypeInAssembly ("GenericPeerCollectionCopyTo", "GenericCollectionElement`1<System.Int32>", By = Tool.NativeAot)]
[assembly: KeptTypeInAssembly ("GenericPeerCollectionCopyTo", "GenericCollectionElement`1<System.String>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("GenericPeerCollectionCopyTo", "GenericCollectionElement`1<System.Int64>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("GenericPeerCollectionCopyTo", "GenericCollectionElement`1<System.Object>", By = Tool.NativeAot)]
[assembly: RemovedTypeInAssembly ("GenericPeerCollectionCopyTo", "GenericCollectionElement`1<MainActivity>", By = Tool.NativeAot)]

[Activity (Label = "GenericPeerCollectionCopyTo", MainLauncher = true)]
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
		using var source = new Java.Util.ArrayList ();
		IntPtr elementHandle = IntPtr.Zero;
		try {
			using (var element = new GenericCollectionElement<T> { Value = value }) {
				source.Add (element);
				elementHandle = JNIEnv.NewGlobalRef (element.Handle);
			}

			var items = new GenericCollectionElement<T> [1];
			using (var collection = new JavaCollection (source.Handle, JniHandleOwnership.DoNotTransfer)) {
				collection.CopyTo (items, 0);
			}
			CheckElement ("JavaCollection.CopyTo", items, elementHandle);

			items = new GenericCollectionElement<T> [1];
			using (var list = new JavaList (source.Handle, JniHandleOwnership.DoNotTransfer)) {
				list.CopyTo (items, 0);
			}
			CheckElement ("JavaList.CopyTo", items, elementHandle);
		} finally {
			JNIEnv.DeleteGlobalRef (elementHandle);
		}
	}

	static void CheckElement<T> (string operation, GenericCollectionElement<T> []? items, IntPtr elementHandle)
	{
		if (items is not { Length: 1 } || items [0] is not { } item) {
			throw new InvalidOperationException ($"{operation} did not return a {typeof (GenericCollectionElement<T>)}.");
		}
		using (item) {
			if (item.GetType () != typeof (GenericCollectionElement<T>)) {
				throw new InvalidOperationException ($"{operation}: expected {typeof (GenericCollectionElement<T>)}, got {item.GetType ()}.");
			}
			if (!JNIEnv.IsSameObject (elementHandle, item.Handle)) {
				throw new InvalidOperationException ($"{operation} returned a peer for a different Java object.");
			}
			// The original peer was disposed, so this must be a new peer from the activation constructor, without its managed state.
			if (!EqualityComparer<T>.Default.Equals (item.Value, default)) {
				throw new InvalidOperationException ($"{operation}: expected a newly activated peer, but Value is '{item.Value}'.");
			}
		}
	}
}

public class GenericCollectionElement<T> : Java.Lang.Object
{
	public GenericCollectionElement ()
	{
	}

	[Kept]
	protected GenericCollectionElement (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	public T? Value { get; set; }
}

// A generic peer the app never uses, with the same shape: its proxy, and so its [DynamicDependency], must not keep it.
[Removed]
public class UnusedGenericCollectionElement<T> : Java.Lang.Object
{
	public UnusedGenericCollectionElement ()
	{
	}

	protected UnusedGenericCollectionElement (IntPtr handle, JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}

	public T? Value { get; set; }
}
