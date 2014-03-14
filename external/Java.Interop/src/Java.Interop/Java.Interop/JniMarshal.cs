using System;
using System.Collections;
using System.Diagnostics;

namespace Java.Interop {

	public static class JniMarshal {

		public static bool RecursiveEquals (object objA, object objB)
		{
			if (object.Equals (objA, objB))
				return true;
			var ae = objA as IEnumerable;
			var be = objB as IEnumerable;
			if (ae != null && be != null) {
				var ai = ae.GetEnumerator ();
				var bi = be.GetEnumerator ();
				try {
					bool am, bm;
					do {
						am = ai.MoveNext ();
						bm = bi.MoveNext ();
						if (!(am && bm))
							break;
						if (!RecursiveEquals (ai.Current, bi.Current))
							return false;
					} while (true);
					return (am == bm);
				} finally {
					var ad = ai as IDisposable;
					var bd = bi as IDisposable;
					if (ad != null)
						ad.Dispose ();
					if (bd != null)
						bd.Dispose ();
				}
			}
			return false;
		}

		internal static T GetValue<T> (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
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
			if (info.GetValueFromJni != null) {
				return (T) info.GetValueFromJni (handle, transfer, typeof (T));
			}

			return (T) jvm.GetObject (handle, transfer, typeof (T));
		}

		internal static JniLocalReference CreateLocalRef<T> (T value)
		{
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniMarshalInfoForType (typeof (T));
			if (info.CreateLocalRef != null) {
				return info.CreateLocalRef (value);
			}

			var o = (value as IJavaObject) ??
				JavaProxyObject.GetProxy (value);
			return jvm.GetJniMarshalInfoForType (typeof (IJavaObject)).CreateLocalRef (o);
		}
	}
}

