using System;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

sealed class JavaMarshalGCBridgeJni
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
		unsafe {
			// SAFETY: complete UTF-8 literals provide trailing NULs for each lookup; getRuntime.()Ljava/lang/Runtime;
			// returns the local duplicated before finally, and cached JniTypes retain the classes for the recorded member IDs.
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
	}

	public bool AddReference (JniObjectReference source, JniObjectReference destination, bool knownGCUserPeer)
	{
		unsafe {
			// SAFETY: the bridge supplies live source/destination references; source is checked for IGCUserPeer
			// unless already known, and the one stack argument matches monodroidAddReference.(Ljava/lang/Object;)V.
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
	}

	public void TriggerJavaGC ()
	{
		unsafe {
			// SAFETY: runtimeInstance is the owned global Runtime and runtimeGc is its cached gc.()V
			// instance method; no argument buffer is required or read during this synchronous call.
			try {
				JniEnvironment.InstanceMethods.CallVoidMethod (runtimeInstance, runtimeGc);
			} catch (JavaException e) {
				Logger.Log (LogLevel.Error, "monodroid", $"Java GC failed: {e.Message}");
			}
		}
	}

	public void ClearReferences (JniObjectReference reference)
	{
		unsafe {
			// SAFETY: the bridge supplies a live global IGCUserPeer reference; clearReferencesMethod is
			// monodroidClearReferences.()V from the retained IGCUserPeer class, with no arguments.
			JniEnvironment.InstanceMethods.CallVoidMethod (reference, clearReferencesMethod);
		}
	}

	public JniObjectReference CreateTemporaryPeer ()
		{
		unsafe {
			// SAFETY: gcUserPeerType retains the class for its cached ()V constructor; the null argument
			// buffer is valid for zero parameters, and ownership of the new local transfers to TemporaryPeerMap.
			return gcUserPeerType.NewObject (gcUserPeerConstructor, null);
		}
	}
}
