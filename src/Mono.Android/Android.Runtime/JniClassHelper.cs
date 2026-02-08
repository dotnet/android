using System;
using System.Runtime.InteropServices;

namespace Android.Runtime
{
	/// <summary>
	/// JNI utility methods shared across the runtime.
	/// Extracted from <c>TypeManager</c> to keep that class focused on backward compatibility.
	/// </summary>
	static class JniClassHelper
	{
		/// <summary>
		/// Get the Java class name for a JNI class pointer.
		/// Returns a JNI-style name (e.g. <c>"android/widget/Button"</c>).
		/// </summary>
		internal static string GetClassName (IntPtr class_ptr)
		{
			IntPtr ptr = RuntimeNativeMethods.monodroid_TypeManager_get_java_class_name (class_ptr);
			string ret = Marshal.PtrToStringAnsi (ptr)!;
			RuntimeNativeMethods.monodroid_free (ptr);

			return ret;
		}
	}
}
