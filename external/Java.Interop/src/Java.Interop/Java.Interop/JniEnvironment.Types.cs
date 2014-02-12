using System;

namespace Java.Interop
{
	partial class JniEnvironment {
		static partial class Types {

			public static JniType GetTypeFromInstance (JniReferenceSafeHandle value)
			{
				var lref = JniEnvironment.Types.GetObjectClass (value);
				if (!lref.IsInvalid)
					return new JniType (lref, JniHandleOwnership.Transfer);
				return null;
			}
		}
	}
}

