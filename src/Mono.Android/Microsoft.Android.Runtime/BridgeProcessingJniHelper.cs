using System;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Java-side GC bridge helpers for NativeAOT.
/// Triggers Java garbage collection and handles GCUserPeerable interface calls.
/// </summary>
unsafe static class BridgeProcessingJniHelper
{
	static JniObjectReference s_RuntimeInstance;
	static JniMethodInfo? s_Runtime_gc;

	static JniType? s_GCUserPeerableClass;
	static JniMethodInfo? s_GCUserPeerable_jiAddManagedReference;
	static JniMethodInfo? s_GCUserPeerable_jiClearManagedReferences;

	/// <summary>
	/// Must be called after JNI runtime is created but before any GC can trigger the bridge callback.
	/// Performs JNI lookups that would deadlock if called during GC.
	/// </summary>
	public static void Initialize ()
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

	public static void TriggerJavaGC ()
	{
		try {
			JniEnvironment.InstanceMethods.CallVoidMethod (s_RuntimeInstance, s_Runtime_gc!, null);
		} catch (Exception ex) {
			Logger.Log (LogLevel.Error, "monodroid-gc", $"Java GC failed: {ex.Message}");
		}
	}

	public static bool AddReference (JniObjectReference from, JniObjectReference to, BridgeProcessingLogger? logger)
	{
		if (!from.IsValid || !to.IsValid) {
			return false;
		}

		// Try the optimized path for GCUserPeerable (NativeAOT)
		if (!RuntimeFeature.IsCoreClrRuntime) {
			if (TryAddManagedReference (from, to)) {
				return true;
			}
		}

		// Fall back to reflection-based approach
		var fromClassRef = JniEnvironment.Types.GetObjectClass (from);
		using var fromClass = new JniType (ref fromClassRef, JniObjectReferenceOptions.CopyAndDispose);

		JniMethodInfo addMethod;
		try {
			addMethod = fromClass.GetInstanceMethod ("monodroidAddReference", "(Ljava/lang/Object;)V");
		} catch (Java.Lang.NoSuchMethodError) {
			logger?.LogMissingAddReferencesMethod (fromClass);
			return false;
		}

		JniArgumentValue* args = stackalloc JniArgumentValue[1];
		args[0] = new JniArgumentValue (to);
		JniEnvironment.InstanceMethods.CallVoidMethod (from, addMethod, args);
		return true;
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
