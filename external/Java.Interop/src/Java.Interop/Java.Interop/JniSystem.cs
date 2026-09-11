#nullable enable

using System;

namespace Java.Interop
{
	static class JniSystem {
		internal static unsafe int IdentityHashCode (JniObjectReference value)
		{
			var args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);
			var info = JniEnvironment.CurrentInfo;
			var runtime = info.Runtime;
			var hashCode = JniNativeMethods.CallStaticIntMethodA (
				info.EnvironmentPointer,
				runtime.SystemClass,
				runtime.SystemIdentityHashCode,
				(IntPtr) args);
			// System.identityHashCode() accepts null and does not throw, so avoid the
			// generic method wrapper's second JNI call to ExceptionOccurred().
			return hashCode;
		}
	}
}
