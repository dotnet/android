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

			return (T) jvm.GetObject (handle, transfer, typeof (T));
		}

		public static JniLocalReference CreateLocalRef<T> (T value)
		{
			var o = (value as IJavaObject) ??
				JavaProxyObject.GetProxy (value);
			if (o == null || o.SafeHandle.IsInvalid)
				return new JniLocalReference ();
			return o.SafeHandle.NewLocalRef ();
		}
	}
}

