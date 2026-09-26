using System;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

sealed unsafe class JavaMarshalGCBridgeJni
{
	readonly JniObjectReference runtimeInstance;
	readonly JniMethodInfo runtimeGc;
	readonly JniType gcUserPeerType;
	readonly JniMethodInfo gcUserPeerConstructor;
	readonly JniType igcUserPeerType;
	readonly JniMethodInfo addReferenceMethod;
	readonly JniMethodInfo clearReferencesMethod;

	public JavaMarshalGCBridgeJni ()
	{
		using var runtimeType = new JniType ("java/lang/Runtime"u8);
		var getRuntime = runtimeType.GetStaticMethod ("getRuntime"u8, "()Ljava/lang/Runtime;"u8);
		var localRuntime = JniEnvironment.StaticMethods.CallStaticObjectMethod (runtimeType.PeerReference, getRuntime);
		try {
			runtimeInstance = localRuntime.NewGlobalRef ();
		} finally {
			JniObjectReference.Dispose (ref localRuntime);
		}
		if (!runtimeInstance.IsValid)
			throw new InvalidOperationException ("Failed to obtain the Java Runtime instance.");
		runtimeGc = runtimeType.GetInstanceMethod ("gc"u8, "()V"u8);

		gcUserPeerType = new JniType ("mono/android/GCUserPeer"u8);
		gcUserPeerConstructor = gcUserPeerType.GetConstructor ("()V"u8);

		igcUserPeerType = new JniType ("mono/android/IGCUserPeer"u8);
		addReferenceMethod = igcUserPeerType.GetInstanceMethod ("monodroidAddReference"u8, "(Ljava/lang/Object;)V"u8);
		clearReferencesMethod = igcUserPeerType.GetInstanceMethod ("monodroidClearReferences"u8, "()V"u8);
	}

	public bool AddReference (JniObjectReference source, JniObjectReference destination, bool knownGCUserPeer)
	{
		if (!source.IsValid)
			throw new InvalidOperationException ("A GC bridge reference source is invalid.");
		if (!destination.IsValid)
			throw new InvalidOperationException ("A GC bridge reference destination is invalid.");

		if (!knownGCUserPeer && !JNIEnv.IsInstanceOf (source.Handle, igcUserPeerType.PeerReference.Handle)) {
			Logger.Log (LogLevel.Error, "monodroid", "Failed to find monodroidAddReference method.");
			if (RuntimeFeature.GCBridgeLogging && Logger.LogGC) {
				string? className = JniEnvironment.Types.GetJniTypeNameFromInstance (source);
				Logger.Log (LogLevel.Error, "monodroid-gc", $"Missing monodroidAddReference method for object of class {className ?? "[unknown]"}.");
			}
			return false;
		}

		JniArgumentValue* parameters = stackalloc JniArgumentValue [1];
		parameters [0] = new JniArgumentValue (destination);
		JniEnvironment.InstanceMethods.CallVoidMethod (source, addReferenceMethod, parameters);
		return true;
	}

	public void TriggerJavaGC ()
	{
		try {
			JniEnvironment.InstanceMethods.CallVoidMethod (runtimeInstance, runtimeGc);
		} catch (JavaException e) {
			Logger.Log (LogLevel.Error, "monodroid", $"Java GC failed: {e.Message}");
		}
	}

	public void ClearReferences (JniObjectReference reference)
	{
		JniEnvironment.InstanceMethods.CallVoidMethod (reference, clearReferencesMethod);
	}

	public JniObjectReference CreateTemporaryPeer ()
		=> gcUserPeerType.NewObject (gcUserPeerConstructor, null);
}
