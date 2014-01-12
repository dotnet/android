using System;

namespace Java.Interop
{
	static class JniRuntime {
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/Runtime");}
		}

		static JniStaticMethodID _getRuntime;
		internal static JniLocalReference GetRuntime ()
		{
			return TypeRef.GetCachedStaticMethod (ref _getRuntime, "getRuntime", "()Ljava/lang/Runtime;")
				.CallObjectMethod (TypeRef.SafeHandle);
		}

		static JniInstanceMethodID _gc;
		internal static void GC (JniLocalReference runtime)
		{
			TypeRef.GetCachedInstanceMethod (ref _gc, "gc", "()V")
				.CallVirtualVoidMethod (runtime);
		}
	}
}
