#nullable enable

using System;

namespace Java.Interop
{
	static class JniSystem {
		static JniType? _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/System");}
		}

		static JniMethodInfo? _identityHashCode;
		internal static int IdentityHashCode (JniObjectReference value)
		{
			Span<JniArgumentValue> args = stackalloc JniArgumentValue [1];
			args [0] = new JniArgumentValue (value);
			TypeRef.GetCachedStaticMethod (ref _identityHashCode, "identityHashCode", "(Ljava/lang/Object;)I");
			unsafe {
				fixed (JniArgumentValue* argsPtr = args)
					return JniEnvironment.StaticMethods.CallStaticIntMethod (TypeRef.PeerReference, _identityHashCode, argsPtr);
			}
		}
	}
}
