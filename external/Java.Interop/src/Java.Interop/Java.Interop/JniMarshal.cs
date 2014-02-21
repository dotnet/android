using System;

namespace Java.Interop {

	static class JniMarshal {

		public static T GetValue<T> (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
		{
			var target  = JniEnvironment.Current.JavaVM.GetObject (handle, transfer, typeof (T));
			var proxy   = target as JavaProxyObject;
			if (proxy != null)
				return (T) proxy.Value;
			return (T) target;
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

