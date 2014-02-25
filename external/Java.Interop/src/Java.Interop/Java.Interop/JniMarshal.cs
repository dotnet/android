using System;

namespace Java.Interop {

	static class JniMarshal {

		public static T GetValue<T> (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
		{
			if (handle == null || handle.IsInvalid)
				return default (T);

			var jvm     = JniEnvironment.Current.JavaVM;
			var target  = jvm.PeekObject (handle);
			var proxy   = target as JavaProxyObject;
			if (proxy != null) {
				JniEnvironment.Handles.Dispose (handle, transfer);
				return (T) proxy.Value;
			}

			var info = jvm.GetJniTypeInfoForType (typeof (T));
			if (info.MarshalFromJni != null) {
				return (T) info.MarshalFromJni (handle, transfer, typeof (T));
			}

			return (T) jvm.GetObject (handle, transfer, typeof (T));
		}

		public static JniLocalReference CreateLocalRef<T> (T value)
		{
			var o = (value as IJavaObject) ??
				JavaProxyObject.GetProxy (value);
			if (o == null || o.SafeHandle.IsInvalid)
				return new JniLocalReference ();

			var info = JniEnvironment.Current.JavaVM.GetJniTypeInfoForType (typeof (T));
			if (info.MarshalToJni != null) {
				return info.MarshalToJni (value);
			}

			return o.SafeHandle.NewLocalRef ();
		}
	}

	static class JniInteger {
		internal    const   string  JniTypeName = "java/lang/Integer";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		static JniInstanceMethodID init;
		public static JniLocalReference NewValue (int value)
		{
			TypeRef.GetCachedConstructor (ref init, "(I)V");
			return TypeRef.NewObject (init, new JValue (value));
		}

		static JniInstanceMethodID intValue;
		public static int GetValue (JniReferenceSafeHandle self, JniHandleOwnership transfer)
		{
			TypeRef.GetCachedInstanceMethod (ref intValue, "intValue", "()I");
			try {
				return intValue.CallVirtualInt32Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}
}

