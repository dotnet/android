using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace MonoDroid.Generation {

	public static class SymbolTable {

		static Dictionary<string, ISymbol> symbols = new Dictionary<string, ISymbol> ();
		static ISymbol char_seq;
		static ISymbol fileinstream_sym;
		static ISymbol fileoutstream_sym;
		static ISymbol instream_sym;
		static ISymbol outstream_sym;
		static ISymbol xmlpullparser_sym;
		static ISymbol xmlresourceparser_sym;
		static ISymbol string_sym;

		static readonly string[] InvariantSymbols = new string[]{
			"Android.Graphics.Color",
			"boolean",
			"byte",
			"char",
			"double",
			"float",
			"int",
			"long",
			"short",
			"void",
		};

		public static IEnumerable<ISymbol> AllRegisteredSymbols ()
		{
			return symbols.Values;
		}

		static SymbolTable ()
		{
			AddType (new SimpleSymbol ("IntPtr.Zero", "void", "void", "V"));
			AddType (new SimpleSymbol ("false", "boolean", "bool", "Z"));
			AddType (new SimpleSymbol ("0", "byte", "sbyte", "B"));
			AddType (new SimpleSymbol ("(char)0", "char", "char", "C"));
			AddType (new SimpleSymbol ("0.0", "double", "double", "D"));
			AddType (new SimpleSymbol ("0.0F", "float", "float", "F"));
			AddType (new SimpleSymbol ("0", "int", "int", "I"));
			AddType (new SimpleSymbol ("0L", "long", "long", "J"));
			AddType (new SimpleSymbol ("0", "short", "short", "S"));
			AddType ("Android.Graphics.Color", new ColorSymbol ());
			char_seq = new CharSequenceSymbol ();
			instream_sym = new StreamSymbol ("InputStream");
			outstream_sym = new StreamSymbol ("OutputStream");
			fileinstream_sym = new StreamSymbol ("FileInputStream", "InputStream");
			fileoutstream_sym = new StreamSymbol ("FileOutputStream", "OutputStream");
			xmlpullparser_sym = new XmlPullParserSymbol ();
			xmlresourceparser_sym = new XmlResourceParserSymbol ();
			string_sym = new StringSymbol ();
		}

		// Extract symbol information
		//     java_type: "foo.Bar<T1<T2>[]>[]..."
		//       returns: "foo.Bar"
		//   type_params: "<T1<T2>[]>"
		//     arrayRank: 2
		//  has_ellipsis: true
		public static string GetSymbolInfo (string java_type, out string type_params, out int arrayRank, out bool has_ellipsis)
		{
			type_params   = string.Empty;
			arrayRank     = 0;
			has_ellipsis  = false;

			if (string.IsNullOrEmpty (java_type))
				return java_type;

			var erased = new StringBuilder (java_type.Length);
			int ltCount = 0;
			for (int i = 0; i < java_type.Length; ++i) {
				char c = java_type [i];
				switch (c) {
				case '<':
					ltCount++;
					type_params += c;
					break;
				case '>':
					ltCount--;
					type_params += c;
					break;
				case '[':
					if (ltCount == 0)
						arrayRank++;
					goto case ']';
				case ']':
					if (ltCount > 0)
						type_params += c;
					break;
				default:
					if (ltCount == 0)
						erased.Append (c);
					else
						type_params += c;
					break;
				}
			}

			has_ellipsis = erased.Length > 3 &&
				erased [erased.Length-1] == '.' &&
				erased [erased.Length-2] == '.' &&
				erased [erased.Length-3] == '.';
			if (has_ellipsis) {
				arrayRank++;
				erased.Length -= 3;
			}

			return erased.ToString ();
		}

		public static void AddType (ISymbol symbol)
		{
			string dummy;
			int ar;
			bool he;
			string key = symbol.IsEnum ? symbol.FullName : GetSymbolInfo (symbol.JavaName, out dummy, out ar, out he);

			if (!ShouldAddType (key))
				return;

			symbols [key] = symbol;
		}

		static bool ShouldAddType (string key)
		{
			if (!symbols.ContainsKey (key))
				return true;
			if (Array.BinarySearch (InvariantSymbols, key, StringComparer.OrdinalIgnoreCase) >= 0)
				return false;
			return true;
		}

		public static void AddType (string key, ISymbol symbol)
		{
			if (!ShouldAddType (key))
				return;

			symbols [key] = symbol;
		}
		
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

		public static ISymbol Lookup (string java_type, GenericParameterDefinitionList in_params)
		{
			string type_params;
			int arrayRank;
			bool has_ellipsis;
			string key = GetSymbolInfo (java_type, out type_params, out arrayRank, out has_ellipsis);

			// FIXME: we should make sure to differentiate those ref types IF we use those modifiers in the future.
			switch (key [key.Length - 1]) {
			case '&': // managed ref type
			case '*': // managed (well, unmanaged...) pointer type
				key = key.Substring (0, key.Length - 1);
				break;
			}
			key = FilterPrimitiveFullName (key) ?? key;

			switch (key) {
			case "android.content.res.XmlResourceParser":
				return CreateArray (xmlresourceparser_sym, arrayRank, has_ellipsis);
			case "org.xmlpull.v1.XmlPullParser":
				return CreateArray (xmlpullparser_sym, arrayRank, has_ellipsis);
			case "java.io.FileInputStream":
				return CreateArray (fileinstream_sym, arrayRank, has_ellipsis);
			case "java.io.FileOutputStream":
				return CreateArray (fileoutstream_sym, arrayRank, has_ellipsis);
			case "java.io.InputStream":
				return CreateArray (instream_sym, arrayRank, has_ellipsis);
			case "java.io.OutputStream":
				return CreateArray (outstream_sym, arrayRank, has_ellipsis);
			case "java.lang.CharSequence":
				return CreateArray (char_seq, arrayRank, has_ellipsis);
			case "java.lang.String":
				return CreateArray (string_sym, arrayRank, has_ellipsis);
			case "java.util.List":
			case "java.util.ArrayList":
			case "System.Collections.IList":
				return CreateArray (new CollectionSymbol (key, "IList", "Android.Runtime.JavaList", type_params), arrayRank, has_ellipsis);
			case "java.util.Map":
			case "java.util.HashMap":
			case "java.util.SortedMap":
			case "System.Collections.IDictionary":
				return CreateArray (new CollectionSymbol (key, "IDictionary", "Android.Runtime.JavaDictionary", type_params), arrayRank, has_ellipsis);
			case "java.util.Set":
				return CreateArray (new CollectionSymbol (key, "ICollection", "Android.Runtime.JavaSet", type_params), arrayRank, has_ellipsis);
			case "java.util.Collection":
			case "System.Collections.ICollection":
				return CreateArray (new CollectionSymbol (key, "ICollection", "Android.Runtime.JavaCollection", type_params), arrayRank, has_ellipsis);
			default:
				break;
			}

			ISymbol result;
			var gpd = in_params != null ? in_params.FirstOrDefault (t => t.Name == key) : null;
			if (gpd != null) {
				result = new GenericTypeParameter (gpd);
			}
			else
				result = Lookup (key + type_params);

			return CreateArray (result, arrayRank, has_ellipsis);
		}

		static ISymbol CreateArray (ISymbol symbol, int rank, bool has_ellipsis)
		{
			if (symbol == null)
				return null;

			ArraySymbol r = null;
			while (rank-- > 0)
				symbol = r = new ArraySymbol (symbol);
			if (r != null)
				r.IsParams = has_ellipsis;
			return symbol;
		}

		public static ISymbol Lookup (string java_type)
		{
			string type_params;
			int ar;
			bool he;
			string key = GetSymbolInfo (java_type, out type_params, out ar, out he);
			ISymbol result;
			ISymbol sym = symbols.ContainsKey (key) ? symbols [key] : symbols.FirstOrDefault (s => s.Value.FullName == key).Value;
			if (sym != null) {
				if (type_params.Length > 0) {
					GenBase gen = sym as GenBase;
					if (gen != null && gen.IsGeneric)
						result = new GenericSymbol (gen, type_params);
					// In other case, it is still valid to derive from non-generic type.
					// Generics are likely removed but we should not invalidate such derived classes.
					else
						result = gen;
				}
				// disable this condition; consider that "java.lang.Class[]" can be specified as a parameter type
				//else if (sym is GenBase && (sym as GenBase).IsGeneric)
				//	return null;
				else
					result = sym;
			} else
				return null;

			return result;
		}
		
		public static void Dump ()
		{
			foreach (var p in symbols)
				if (!p.Key.StartsWith ("System"))
					Console.Error.WriteLine ("[{0}]: {1} {2}", p.Key, p.Value.GetType (), p.Value.FullName);
		}

		public static string GetNativeName (string name)
		{
			if (name.StartsWith ("@"))
				return "native__" + name.Substring (1);
			return "native_" + name;
		}

		public static string MangleName (string name)
		{
			switch (name) {
			case "event":
				return "e";
			case "base":
			case "bool":
			case "byte":
			case "callback":
			case "checked":
			case "decimal":
			case "delegate":
			case "fixed":
			case "foreach":
			case "in":
			case "int":
			case "interface":
			case "internal":
			case "is":
			case "lock":
			case "namespace":
			case "new":
			case "null":
			case "object":
			case "out":
			case "override":
			case "params":
			case "readonly":
			case "ref":
			case "remove":
			case "string":
			case "where":
				return "@" + name;
			default:
				return name;
			}
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
	}
}
