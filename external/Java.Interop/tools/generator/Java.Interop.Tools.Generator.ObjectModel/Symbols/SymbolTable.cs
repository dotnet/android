using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace MonoDroid.Generation {

	public class SymbolTable {

		// The symbols dictionary may contain shallow types (types that have not populated Ctors/Methods/Fields).
		// If you make any changes to the SymbolTable class that accesses the symbols you need to keep
		// that in mind.  Also if you add any new public methods that expose symbols from the table you must
		// EnsurePopulated them before letting them leave this class.
		ConcurrentDictionary<string, List<ISymbol>> symbols = new ConcurrentDictionary<string, List<ISymbol>> ();

		ISymbol char_seq;
		ISymbol fileinstream_sym;
		ISymbol fileoutstream_sym;
		ISymbol instream_sym;
		ISymbol outstream_sym;
		ISymbol xmlpullparser_sym;
		ISymbol xmlresourceparser_sym;
		ISymbol string_sym;

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
			"ubyte",
			"uint",
			"ulong",
			"ushort",
			"void",
		};

		public IEnumerable<ISymbol> AllRegisteredSymbols (CodeGenerationOptions options)
		{
			if (options.UseShallowReferencedTypes)
				throw new InvalidOperationException ("Not safe to retrieve all registered symbols when using shallow types.");

			return symbols.Values.SelectMany (v => v);
		}

		public SymbolTable ()
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
			AddType (new SimpleSymbol ("0", "uint", "uint", "I", returnCast: "(uint)"));
			AddType (new SimpleSymbol ("0", "ushort", "ushort", "S", returnCast: "(ushort)"));
			AddType (new SimpleSymbol ("0", "ulong", "ulong", "J", returnCast: "(ulong)"));
			AddType (new SimpleSymbol ("0", "ubyte", "byte", "B", returnCast: "(byte)"));
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
		public string GetSymbolInfo (string java_type, out string type_params, out int arrayRank, out bool has_ellipsis)
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

		public void AddType (ISymbol symbol)
		{
			string dummy;
			int ar;
			bool he;
			string key = symbol.IsEnum ? symbol.FullName : GetSymbolInfo (symbol.JavaName, out dummy, out ar, out he);

			AddType (key, symbol);
		}

		bool ShouldAddType (string key)
		{
			if (!symbols.ContainsKey (key))
				return true;
			if (Array.BinarySearch (InvariantSymbols, key, StringComparer.OrdinalIgnoreCase) >= 0)
				return false;
			return true;
		}

		public void AddType (string key, ISymbol symbol)
		{
			if (!ShouldAddType (key))
				return;

			symbols.AddOrUpdate (key,
				(value) => {
					// Key not found, add it to Dictionary
					lock (cache_population_lock)
						all_symbols_cache = null;

					return new List<ISymbol> { symbol };
				},
				(value, list) => {
					// Key already exists, add it to List
					if (!list.Any (v => object.ReferenceEquals (v, symbol)))
						list.Add (symbol);

					return list;
				});
		}

		public ISymbol Lookup (string java_type, GenericParameterDefinitionList in_params)
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
			key = TypeNameUtilities.FilterPrimitiveFullName (key) ?? key;

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

		ISymbol CreateArray (ISymbol symbol, int rank, bool has_ellipsis)
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

		ConcurrentDictionary<string, ISymbol> all_symbols_cache;
		static object cache_population_lock = new object ();

		public ISymbol Lookup (string java_type)
		{
			string type_params;
			int ar;
			bool he;
			string key = GetSymbolInfo (java_type, out type_params, out ar, out he);
			ISymbol sym;
			List<ISymbol> values;
			if (symbols.TryGetValue (key, out values)) {
				sym = values [values.Count-1];
			} else {
				// Note we're potentially searching shallow types, but this is only looking at the type name
				// Anything we find we will populate before returning to the user

				lock (cache_population_lock) {
					if (all_symbols_cache == null)
						all_symbols_cache = new ConcurrentDictionary<string, ISymbol> (symbols.Values.SelectMany (v => v).GroupBy (s => s.FullName).ToDictionary (s => s.Key, s => s.FirstOrDefault ()));

					if (!all_symbols_cache.TryGetValue (key, out sym)) {
						// We may be looking for a type like:
						// - System.Collections.Generic.IList<Java.Util.Locale.LanguageRange>
						// Our key is "System.Collections.Generic.IList", but it's stored in
						// the symbol table with the arity so we need to look for
						// "System.Collections.Generic.IList`1" to find a match
						key = AddArity (key, type_params);
						all_symbols_cache.TryGetValue (key, out sym);
					}
				}
			}
			ISymbol result;
			if (sym != null) {
				if (type_params.Length > 0) {
					GenBase gen = sym as GenBase;

					EnsurePopulated (gen);

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

			if (result is GenBase gen_base)
				EnsurePopulated (gen_base);

			return result;
		}

		private string AddArity (string key, string typeParams)
		{
			if (string.IsNullOrWhiteSpace (typeParams) || !typeParams.StartsWith ("<", StringComparison.Ordinal) || !typeParams.EndsWith (">", StringComparison.Ordinal))
				return key;

			var nested_count = 0;
			var arity = 1;

			// Remove the outer <>
			typeParams = typeParams.Substring (1, typeParams.Length - 2);

			foreach (var c in typeParams) {
				if (c == '>')
					nested_count--;

				if (c == '<')
					nested_count++;

				if (nested_count == 0 && c == ',')
					arity++;
			}

			return $"{key}`{arity}";
		}

		public void Dump ()
		{
			foreach (var p in symbols) {
				if (p.Key.StartsWith ("System", StringComparison.Ordinal))
					continue;
				foreach (var s in p.Value) {
					Console.Error.WriteLine ("[{0}]: {1} {2}", p.Key, s.GetType (), s.FullName);
				}
			}
		}

		static readonly object populate_lock = new object ();

		void EnsurePopulated (GenBase gen)
		{
			if (gen == null || !gen.IsShallow)
				return;

			foreach (var nested in gen.NestedTypes)
				EnsurePopulated (nested);

			// We need to fully populate this shallow type
			lock (populate_lock) {
				gen.PopulateAction ();
				gen.IsShallow = false;
				gen.PopulateAction = null;
			}
		}
	}
}
