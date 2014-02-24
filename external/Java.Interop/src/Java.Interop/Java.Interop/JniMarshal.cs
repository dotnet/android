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
			if (proxy != null)
				return (T) proxy.Value;

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
}

