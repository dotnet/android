using System;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Triggers Java garbage collection directly through JNI.
/// </summary>
static class JavaGCTrigger
{
	static JniObjectReference s_RuntimeInstance;
	static JniMethodInfo? s_Runtime_gc;

	static JavaGCTrigger ()
	{
		using var runtimeClass = new JniType ("java/lang/Runtime");
		var getRuntimeMethod = runtimeClass.GetStaticMethod ("getRuntime", "()Ljava/lang/Runtime;");
		s_Runtime_gc = runtimeClass.GetInstanceMethod ("gc", "()V");
		var runtimeLocal = JniEnvironment.StaticMethods.CallStaticObjectMethod (runtimeClass.PeerReference, getRuntimeMethod, null);
		s_RuntimeInstance = runtimeLocal.NewGlobalRef ();
		JniObjectReference.Dispose (ref runtimeLocal);
	}

	public static void Trigger ()
	{
		if (s_Runtime_gc == null) {
			throw new InvalidOperationException ("JavaGCTrigger static constructor must run before Trigger");
		}

		try {
			JniEnvironment.InstanceMethods.CallVoidMethod (s_RuntimeInstance, s_Runtime_gc, null);
		} catch (Exception ex) {
			Logger.Log (LogLevel.Error, "monodroid-gc", $"Java GC failed: {ex.Message}");
		}
	}
}
