using System;
using System.Collections.Generic;
using System.Text;

namespace Java.Interop
{
	partial class JniEnvironment {
		static partial class Types {

			readonly    static  KeyValuePair<string, string>[]  BuiltinMappings = new KeyValuePair<string, string>[] {
				new KeyValuePair<string, string>("byte",       "B"),
				new KeyValuePair<string, string>("boolean",    "Z"),
				new KeyValuePair<string, string>("char",       "C"),
				new KeyValuePair<string, string>("double",     "D"),
				new KeyValuePair<string, string>("float",      "F"),
				new KeyValuePair<string, string>("int",        "I"),
				new KeyValuePair<string, string>("long",       "J"),
				new KeyValuePair<string, string>("short",      "S"),
				new KeyValuePair<string, string>("void",       "V"),
			};


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
					return GetJniTypeNameFromClass (c);
				}
			}

			public static string GetJniTypeNameFromClass (JniReferenceSafeHandle handle)
			{
				var s = JniEnvironment.Current.Class_getName.CallVirtualObjectMethod (handle);
				return JavaClassToJniType (Strings.ToString (s, JniHandleOwnership.Transfer));
			}

			static string JavaClassToJniType (string value)
			{
				for (int i = 0; i < BuiltinMappings.Length; ++i) {
					if (value == BuiltinMappings [i].Key)
						return BuiltinMappings [i].Value;
				}
				return value.Replace ('.', '/');
			}
		}
	}
}

