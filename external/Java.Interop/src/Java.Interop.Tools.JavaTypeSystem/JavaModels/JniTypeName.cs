using System;
using System.Collections.Generic;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JniSignature
	{
		public JniTypeName Return { get; }
		public List<JniTypeName> Parameters { get; } = new List<JniTypeName> ();

		public JniSignature (JniTypeName returnType, params JniTypeName[] parameterTypes)
		{
			Return = returnType;
			Parameters.AddRange (parameterTypes);
		}

		public static JniSignature Parse (string signature)
		{
			var idx = signature.LastIndexOf (')') + 1;
			var jni = new JniSignature (JniTypeName.Parse (signature.Substring (idx)));

			// Strip out return type
			if (signature.StartsWith ("(", StringComparison.Ordinal)) {
				var e = signature.IndexOf (')');
				signature = signature.Substring (1, e >= 0 ? e - 1 : signature.Length - 1);
			}

			// Parse parameters
			var i = 0;

			while (i < signature.Length) {
				var t = JniTypeName.Parse (signature.Substring (i));

				jni.Parameters.Add (t);
				i += t.Jni.Length;
			}

			return jni;
		}

		public override string ToString ()
		{
			return $"({string.Join ("", Parameters)}){Return}";
		}
	}

	public class JniTypeName
	{
		public string Type { get; }
		public string Jni { get; }
		public bool IsKeyword { get; }

		public JniTypeName (string jni, string type, bool isKeyword)
		{
			Jni = jni;
			Type = type;
			IsKeyword = isKeyword;
		}

		// This returns the first type found in the signature and ignores any extra signature data
		public static JniTypeName Parse (string signature)
		{
			var index = 0;

			switch (signature [index]) {
				case '[': {
						++index;

						if (index >= signature.Length)
							throw new InvalidOperationException ("Missing array type after '[' at index " + index + " in: " + signature);

						var r = Parse (signature.Substring (index));

						return new JniTypeName (signature.Substring (0, index) + r.Jni, r.Type + "[]", r.IsKeyword);
					}
				case 'B':
					return new JniTypeName ("B", "byte", true);
				case 'C':
					return new JniTypeName ("C", "char", true);
				case 'D':
					return new JniTypeName ("D", "double", true);
				case 'F':
					return new JniTypeName ("F", "float", true);
				case 'I':
					return new JniTypeName ("I", "int", true);
				case 'J':
					return new JniTypeName ("J", "long", true);
				case 'L': {
						var e = signature.IndexOf (';', index);

						if (e <= 0)
							throw new InvalidOperationException ("Missing reference type after 'L' at index " + index + "in: " + signature);

						//var s = index;
						//index = e + 1;

						return new JniTypeName (
							signature.Substring (0, e + 1),
							signature.Substring (index + 1, e - 1).Replace ("/", ".").Replace ("$", "."),
							false
						);
					}
				case 'S':
					return new JniTypeName ("S", "short", true);
				case 'V':
					return new JniTypeName ("V", "void", true);
				case 'Z':
					return new JniTypeName ("Z", "boolean", true);
				default:
					throw new InvalidOperationException ("Unknown JNI Type '" + signature [index] + "' within: " + signature);
			}
		}

		// This throws an exception if there is extra data in the signature
		public static JniTypeName ParseExact (string signature)
		{
			var jni = Parse (signature);

			if (jni.Jni.Length != signature.Length)
				throw new InvalidOperationException ("Extra JNI signature");

			return jni;
		}

		public override string ToString () => Jni;
	}
}


