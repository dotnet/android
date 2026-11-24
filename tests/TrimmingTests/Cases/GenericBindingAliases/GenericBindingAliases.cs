#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property ApplicationId=net.dot.android.trimmingtests.genericbindingaliases
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj
#:project AGenericBinding/AGenericBinding.csproj
#:project ZNonGenericBinding/ZNonGenericBinding.csproj

// Regression test for https://github.com/dotnet/android/issues/13674: an open generic binding can sort first for an
// untyped Java-to-managed lookup when two libraries bind the same Java type.

using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("AGenericBinding", "A.GenericPeer`1")]
[assembly: KeptTypeInAssembly ("ZNonGenericBinding", "Z.NonGenericPeer")]

[Activity (Label = "GenericBindingAliases", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);
		try {
			GC.KeepAlive (typeof (A.GenericPeer<Java.Lang.Object>));
			GC.KeepAlive (typeof (Z.NonGenericPeer));

			var runtime = Java.Interop.JniEnvironment.Runtime;
			var mappedType = runtime.TypeManager.GetType (new Java.Interop.JniTypeSignature ("example/AliasPeer"));
			if (mappedType != typeof (Z.NonGenericPeer)) {
				throw new InvalidOperationException ($"Untyped lookup selected {mappedType}.");
			}

			var javaClass = JNIEnv.FindClass ("example/AliasPeer");
			try {
				var constructor = JNIEnv.GetMethodID (javaClass, "<init>", "()V");
				var handle = JNIEnv.NewObject (javaClass, constructor);
				try {
					var reference = new Java.Interop.JniObjectReference (handle, Java.Interop.JniObjectReferenceType.Local);
					using var peer = runtime.ValueManager.CreatePeer (
						ref reference, Java.Interop.JniObjectReferenceOptions.Copy, targetType: null);
					if (peer?.GetType () != typeof (Z.NonGenericPeer)) {
						throw new InvalidOperationException ($"Untyped activation created {peer?.GetType ()}.");
					}
				} finally {
					JNIEnv.DeleteLocalRef (handle);
				}

				handle = JNIEnv.NewObject (javaClass, constructor);
				using var genericPeer = Java.Lang.Object.GetObject<A.GenericPeer<Java.Lang.Object>> (handle, JniHandleOwnership.TransferLocalRef);
				if (genericPeer?.GetType () != typeof (A.GenericPeer<Java.Lang.Object>)) {
					throw new InvalidOperationException ("The closed-generic binding was lost.");
				}
				Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
			} finally {
				JNIEnv.DeleteGlobalRef (javaClass);
			}
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
