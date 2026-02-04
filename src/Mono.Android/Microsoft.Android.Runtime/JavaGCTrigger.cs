using System;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Java-side GC bridge helpers for NativeAOT.
/// Triggers Java garbage collection and handles GCUserPeerable interface calls.
/// </summary>
unsafe static class JavaGCTrigger
{
	static JniObjectReference s_RuntimeInstance;
	static JniMethodInfo? s_Runtime_gc;

	// For NativeAOT: GCUserPeerable interface
	static JniType? s_GCUserPeerableClass;
	static JniMethodInfo? s_GCUserPeerable_jiAddManagedReference;
	static JniMethodInfo? s_GCUserPeerable_jiClearManagedReferences;

	static JavaGCTrigger ()
	{
		using var runtimeClass = new JniType ("java/lang/Runtime");
		var getRuntimeMethod = runtimeClass.GetStaticMethod ("getRuntime", "()Ljava/lang/Runtime;");
		s_Runtime_gc = runtimeClass.GetInstanceMethod ("gc", "()V");
		var runtimeLocal = JniEnvironment.StaticMethods.CallStaticObjectMethod (runtimeClass.PeerReference, getRuntimeMethod, null);
		s_RuntimeInstance = runtimeLocal.NewGlobalRef ();
		JniObjectReference.Dispose (ref runtimeLocal);

		if (!RuntimeFeature.IsCoreClrRuntime) {
			s_GCUserPeerableClass = new JniType ("net/dot/jni/GCUserPeerable");
			s_GCUserPeerable_jiAddManagedReference = s_GCUserPeerableClass.GetInstanceMethod ("jiAddManagedReference", "(Ljava/lang/Object;)V");
			s_GCUserPeerable_jiClearManagedReferences = s_GCUserPeerableClass.GetInstanceMethod ("jiClearManagedReferences", "()V");
		}
	}

	public static void Trigger ()
	{
		try {
			JniEnvironment.InstanceMethods.CallVoidMethod (s_RuntimeInstance, s_Runtime_gc!, null);
		} catch (Exception ex) {
			Logger.Log (LogLevel.Error, "monodroid-gc", $"Java GC failed: {ex.Message}");
		}
	}

	public static bool TryAddManagedReference (JniObjectReference from, JniObjectReference to)
	{
		if (s_GCUserPeerableClass == null || s_GCUserPeerable_jiAddManagedReference == null) {
			return false;
		}

		if (!JniEnvironment.Types.IsInstanceOf (from, s_GCUserPeerableClass.PeerReference)) {
			return false;
		}

		JniArgumentValue* args = stackalloc JniArgumentValue[1];
		args[0] = new JniArgumentValue (to);
		JniEnvironment.InstanceMethods.CallVoidMethod (from, s_GCUserPeerable_jiAddManagedReference, args);
		return true;
	}

	public static bool TryClearManagedReferences (JniObjectReference handle)
	{
		if (s_GCUserPeerableClass == null || s_GCUserPeerable_jiClearManagedReferences == null) {
			return false;
		}

		if (!JniEnvironment.Types.IsInstanceOf (handle, s_GCUserPeerableClass.PeerReference)) {
			return false;
		}

		JniEnvironment.InstanceMethods.CallVoidMethod (handle, s_GCUserPeerable_jiClearManagedReferences, null);
		return true;
	}
}
