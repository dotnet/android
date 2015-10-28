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

			static  readonly    JniInstanceMethodInfo   Class_getName;

			static Types ()
			{
				using (var t = new JniType ("java/lang/Class")) {
					Class_getName   = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
				}
			}


			public static JniType GetTypeFromInstance (JniObjectReference reference)
			{
				var lref = JniEnvironment.Types.GetObjectClass (reference);
				if (lref.IsValid)
					return new JniType (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
				return null;
			}

			public static string GetJniTypeNameFromInstance (JniObjectReference reference)
			{
				var lref = GetObjectClass (reference);
				try {
					return GetJniTypeNameFromClass (lref);
				}
				finally {
					JniEnvironment.References.Dispose (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
				}
			}

			public static string GetJniTypeNameFromClass (JniObjectReference reference)
			{
				var s = Class_getName.InvokeVirtualObjectMethod (reference);
				return JavaClassToJniType (Strings.ToString (ref s, JniObjectReferenceOptions.DisposeSourceReference));
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

