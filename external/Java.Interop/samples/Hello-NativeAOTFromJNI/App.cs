using System.Runtime.InteropServices;

using Java.Interop;

namespace Hello_NativeAOTFromJNI;

static class App {

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-x64/h-classes/net_dot_jni_hello_App.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_hello_App_sayHello")]
	static IntPtr sayHello (IntPtr jnienv, IntPtr klass)
	{
		var envp = new JniTransition (jnienv);
		try {
			var s = $"Hello from .NET NativeAOT!";
			Console.WriteLine (s);
			var h = JniEnvironment.Strings.NewString (s);
			var r = JniEnvironment.References.NewReturnToJniRef (h);
			JniObjectReference.Dispose (ref h);
			return r;
		}
		catch (Exception e) {
			Console.Error.WriteLine ($"Error in App.sayHello(): {e.ToString ()}");
			envp.SetPendingException (e);
		}
		finally {
			envp.Dispose ();
		}
		return nint.Zero;
	}
}
