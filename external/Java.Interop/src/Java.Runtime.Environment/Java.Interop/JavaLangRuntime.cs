using System;

namespace Java.Interop
{
	static class JavaLangRuntime {
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/Runtime");}
		}

		static JniStaticMethodInfo _getRuntime;
		internal static JniObjectReference GetRuntime ()
		{
			return TypeRef.GetCachedStaticMethod (ref _getRuntime, "getRuntime", "()Ljava/lang/Runtime;")
				.InvokeObjectMethod (TypeRef.PeerReference);
		}

		static JniInstanceMethodInfo _gc;
		internal static void GC (JniObjectReference runtime)
		{
			TypeRef.GetCachedInstanceMethod (ref _gc, "gc", "()V")
				.InvokeVirtualVoidMethod (runtime);
		}
	}
}
