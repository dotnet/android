using System.Runtime.InteropServices;

using Java.Interop;

namespace Hello_NativeAOTFromJNI;

static class App {

	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_hello_App_sayHello")]
	static IntPtr sayHello (IntPtr jnienv, IntPtr klass)
	{
		var envp = new JniTransition (jnienv);
		try {
			const string message = "Hello from .NET NativeAOT!";
			Console.WriteLine (message);
			var h = JniEnvironment.Strings.NewString (message);
			var r = JniEnvironment.References.NewReturnToJniRef (h);
			JniObjectReference.Dispose (ref h);
			return r;
		}
		catch (Exception e) {
			Console.Error.WriteLine ($"Error in App.sayHello(): {e}");
			envp.SetPendingException (e);
		}
		finally {
			envp.Dispose ();
		}
		return IntPtr.Zero;
	}
}
