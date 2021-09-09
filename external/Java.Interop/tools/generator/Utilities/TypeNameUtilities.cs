using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Utils;

namespace MonoDroid.Generation
{
	public static class TypeNameUtilities
	{
		// These must be sorted for BinarySearch to work
		// Missing "this" because it's handled elsewhere as "this_"
		internal static string [] reserved_keywords = new [] {
			"abstract", "as", "base", "bool", "break", "byte", "callback", "case", "catch", "char", "checked", "class", "const",
			"continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false",
			"finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal",
			"is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private",
			"protected", "public", "readonly", "ref", "remove", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
			"struct", "switch", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
			"using", "virtual", "void", "volatile", "where", "while",
		};

		public static string FilterPrimitiveFullName (string s)
		{
			switch (s) {
			case "System.Boolean":
				return "boolean";
			case "System.Char":
				return "char";
			case "System.Byte":
				return "byte";
			case "System.SByte":
				return "byte";
			case "System.Int16":
				return "short";
			case "System.Int32":
				return "int";
			case "System.Int64":
				return "long";
			case "System.Single":
				return "float";
			case "System.Double":
				return "double";
			case "System.Void":
				return "void";
			case "System.String":
				return "java.lang.String";
			}
			return null;
		}

		public static string GetGenericJavaObjectTypeOverride (string managed_name, string parms)
		{
			switch (managed_name) {
			case "System.Collections.ICollection":
				return "JavaCollection";
			case "System.Collections.IDictionary":
				return "JavaDictionary";
			case "System.Collections.IList":
				return "JavaList";
			case "System.Collections.Generic.ICollection":
				return "JavaCollection" + parms;
			case "System.Collections.Generic.IList":
				return "JavaList" + parms;
			case "System.Collections.Generic.IDictionary":
				return "JavaDictionary" + parms;
			}
			return null;
		}

		public static string GetNativeName (string name)
		{
			if (name.StartsWith ("@", StringComparison.Ordinal))
				return "native__" + name.Substring (1);
			return "native_" + name;
		}

		public static string MangleName (string name)
		{
			if (name == "event")
				return "e";
			
			if (Array.BinarySearch (reserved_keywords, name) >= 0)
				return "@" + name;

			return name;
		}

		public static string StudlyCase (string name)
		{
			StringBuilder builder = new StringBuilder ();
			bool raise = true;
			foreach (char c in name) {
				if (c == '_' || c == '-')
					raise = true;
				else if (raise) {
					builder.Append (Char.ToUpper (c));
					raise = false;
				} else
					builder.Append (c);
			}
			return builder.ToString ();
		}

		public static string GetCallPrefix (ISymbol symbol)
		{
			if (symbol is SimpleSymbol || symbol.IsEnum) {
				var ret = StringRocks.MemberToPascalCase (symbol.JavaName);

				// We do not have unsigned versions of GetIntValue, etc.
				// We use the signed versions and cast the result
				if (ret == "Uint")
					return "Int";
				if (ret == "Ushort")
					return "Short";
				if (ret == "Ulong")
					return "Long";
				if (ret == "Ubyte")
					return "Byte";

				return ret;

			} else
				return "Object";
		}
	}
}
