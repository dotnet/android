using System.Runtime.InteropServices;

using Java.Interop;

namespace Hello_NativeAOTFromJNI;

static class JavaInteropRuntime
{
	static JniRuntime? runtime;

	[UnmanagedCallersOnly (EntryPoint="JNI_OnLoad")]
	static int JNI_OnLoad (IntPtr vm, IntPtr reserved)
	{
		return (int) JniVersion.v1_6;
	}

	[UnmanagedCallersOnly (EntryPoint="JNI_OnUnload")]
	static void JNI_OnUnload (IntPtr vm, IntPtr reserved)
	{
		runtime?.Dispose ();
	}

	// symbol name from `$(IntermediateOutputPath)obj/Release/osx-arm64/h-classes/net_dot_jni_hello_JavaInteropRuntime.h`
	[UnmanagedCallersOnly (EntryPoint="Java_net_dot_jni_hello_JavaInteropRuntime_init")]
	static void init (IntPtr jnienv, IntPtr klass)
	{
		Console.WriteLine ($"C# init()");
		try {
			var options = new JreRuntimeOptions {
				EnvironmentPointer  = jnienv,
				TypeManager             = new NativeAotTypeManager (),
				UseMarshalMemberBuilder = false,
			};
			runtime = options.CreateJreVM ();
		}
		catch (Exception e) {
			Console.Error.WriteLine ($"JavaInteropRuntime.init: error: {e}");
		}
	}
}
