using System;
using System.Diagnostics;

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

			var info = jvm.GetJniMarshalInfoForType (typeof (T));
			if (info.MarshalFromJni != null) {
				return (T) info.MarshalFromJni (handle, transfer, typeof (T));
			}

			return (T) jvm.GetObject (handle, transfer, typeof (T));
		}

		public static JniLocalReference CreateLocalRef<T> (T value)
		{
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniMarshalInfoForType (typeof (T));
			if (info.MarshalToJni != null) {
				return info.MarshalToJni (value);
			}

			var o = (value as IJavaObject) ??
				JavaProxyObject.GetProxy (value);
			return jvm.GetJniMarshalInfoForType (typeof (IJavaObject)).MarshalToJni (o);
		}
	}

	static class JniInteger {
		internal    const   string  JniTypeName = "java/lang/Integer";

		static JniType _TypeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _TypeRef, JniTypeName);}
		}

		static JniInstanceMethodID init;
		internal static JniLocalReference NewValue (object value)
		{
			Debug.Assert (value is int);
			TypeRef.GetCachedConstructor (ref init, "(I)V");
			return TypeRef.NewObject (init, new JValue ((int) value));
		}

		static JniInstanceMethodID intValue;
		internal static object GetValue (JniReferenceSafeHandle self, JniHandleOwnership transfer, Type targetType)
		{
			Debug.Assert (targetType == typeof (int));
			TypeRef.GetCachedInstanceMethod (ref intValue, "intValue", "()I");
			try {
				return intValue.CallVirtualInt32Method (self);
			} finally {
				JniEnvironment.Handles.Dispose (self, transfer);
			}
		}
	}
}

