using System;

namespace Java.Interop
{
	public static class JniObject {

		public static bool IsSameInstance (JniReferenceSafeHandle a, JniReferenceSafeHandle b)
		{
			return JniTypes.IsSameObject (a, b);
		}

		public static JniType GetTypeFromInstance (JniReferenceSafeHandle value)
		{
			var lref = JniTypes.GetObjectClass (value);
			if (!lref.IsInvalid)
				return new JniType (lref, JniHandleOwnership.Transfer);
			return null;
		}
	}
}

