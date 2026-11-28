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
using Android.Runtime;
using Microsoft.Android.TrimmingTests.Expectations;

[assembly: KeptTypeInAssembly ("AGenericBinding", "A.GenericPeer`1")]
[assembly: KeptTypeInAssembly ("ZNonGenericBinding", "Z.NonGenericPeer")]

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
			var runtime = Java.Interop.JniEnvironment.Runtime;
			var mappedType = runtime.TypeManager.GetType (new Java.Interop.JniTypeSignature ("example/AliasPeer"));
			var reference = new Java.Interop.JniObjectReference (
				JNIEnv.CreateInstance ("example/AliasPeer", "()V"), Java.Interop.JniObjectReferenceType.Local);
			using var peer = runtime.ValueManager.CreatePeer (
				ref reference, Java.Interop.JniObjectReferenceOptions.CopyAndDispose, targetType: null);
			using var genericPeer = Java.Lang.Object.GetObject<A.GenericPeer<Java.Lang.Object>> (
				JNIEnv.CreateInstance ("example/AliasPeer", "()V"), JniHandleOwnership.TransferLocalRef);
			if (mappedType != typeof (Z.NonGenericPeer) || peer?.GetType () != typeof (Z.NonGenericPeer) ||
					genericPeer?.GetType () != typeof (A.GenericPeer<Java.Lang.Object>)) {
				throw new InvalidOperationException ($"Wrong peers: {mappedType}, {peer?.GetType ()}, {genericPeer?.GetType ()}");
			}
			Android.Util.Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
		} catch (Exception e) {
			Android.Util.Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
		}
	}
}
