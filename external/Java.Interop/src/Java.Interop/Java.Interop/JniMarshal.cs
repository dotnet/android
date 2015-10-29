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

		internal static T GetValue<T> (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
		{
			if (!reference.IsValid)
				return default (T);

			var jvm     = JniEnvironment.Runtime;
			var target  = jvm.PeekObject (reference);
			var proxy   = target as JavaProxyObject;
			if (proxy != null) {
				JniEnvironment.References.Dispose (ref reference, transfer);
				return (T) proxy.Value;
			}

			if (target is T) {
				JniEnvironment.References.Dispose (ref reference, transfer);
				return (T) target;
			}

			var info = jvm.GetJniMarshalInfoForType (typeof (T));
			if (info.GetValueFromJni != null) {
				return (T) info.GetValueFromJni (ref reference, transfer, typeof (T));
			}

			var signature   = jvm.TypeManager.GetTypeSignature (JniEnvironment.Types.GetJniTypeNameFromInstance (reference));
			var targetType  = jvm.TypeManager.GetType (signature);
			if (targetType != null &&
					typeof (T).IsAssignableFrom (targetType) &&
					(info = jvm.GetJniMarshalInfoForType (targetType)).GetValueFromJni != null) {
				return (T) info.GetValueFromJni (ref reference, transfer, targetType);
			}

			return (T) jvm.GetObject (ref reference, transfer, typeof (T));
		}

		public  static JniObjectReference CreateLocalRef<T> (T value)
		{
			var jvm     = JniEnvironment.Runtime;

			var info    = new JniMarshalInfo ();
			if (value != null)
				info = jvm.GetJniMarshalInfoForType (value.GetType ());
			if (info.CreateLocalRef == null)
				info = jvm.GetJniMarshalInfoForType (typeof (T));

			if (info.CreateLocalRef != null) {
				return info.CreateLocalRef (value);
			}

			var o = (value as IJavaPeerable) ??
				JavaProxyObject.GetProxy (value);
			return jvm.GetJniMarshalInfoForType (typeof (IJavaPeerable)).CreateLocalRef (o);
		}
	}
}

