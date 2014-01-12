using System;

namespace Java.Interop
{
	static class JniSystem {
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/System");}
		}

		static JniStaticMethodID _identityHashCode;
		internal static int IdentityHashCode (JniReferenceSafeHandle value)
		{
			return TypeRef.GetCachedStaticMethod (ref _identityHashCode, "identityHashCode", "(Ljava/lang/Object;)I")
				.CallInt32Method (TypeRef.SafeHandle, new JValue (value));
		}
	}
}

