using System;
using System.Text;

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

			public static string GetJniTypeNameFromInstance (JniReferenceSafeHandle handle)
			{
				using (var c = GetObjectClass (handle)) {
					var s = JniEnvironment.Current.Class_getName.CallVirtualObjectMethod (c);
					return JavaClassToJniType (Strings.ToString (s, JniHandleOwnership.Transfer));
				}
			}

			static string JavaClassToJniType (string value)
			{
				return value.Replace ('.', '/');
			}
		}
	}
}

