using System.Collections.Generic;
using System.Linq;

namespace MonoDroid.Generation
{
	// Parses a type string into its type and optionally its generic type arguments
	// ex: Dictionary<string, List<string>>
	// -> Type: Dictionary<{0}>
	//    - GenericArguments:
	//      - Type: string
	//      - Type: List<{0}>
	//        - GenericArguments:
	//          - Type: string
	// A placeholder "{0}" is added because the type may extend past the generics:
	// ex: "List<string>.Enumerator[]" becomes "List<{0}>.Enumerator[]"
	public class ParsedType
	{
		public string Type { get; set; }
		public List<ParsedType> GenericArguments { get; } = new List<ParsedType> ();
		public bool HasGenerics => GenericArguments.Count > 0;

		ParsedType () { }

		public static ParsedType Parse (string type)
		{
			var less_than = type.IndexOf ('<');

			// No generics
			if (less_than < 0)
				return new ParsedType { Type = type };

			var greater_than = type.LastIndexOf ('>');
			var type_args = type.Substring (less_than + 1, greater_than - less_than - 1);
			var type_string = type.Substring (0, less_than) + "<{0}>" + (greater_than + 1 < type.Length ? type.Substring (greater_than + 1) : string.Empty);

			var parsed_args = ParseTypeList (type_args);

			var t = new ParsedType { Type = type_string };

			foreach (var p in parsed_args)
				t.GenericArguments.Add (Parse (p));

			return t;
		}

		public override string ToString ()
		{
			return ToString (false);
		}

		public string ToString (bool useGlobal = false)
		{
			var type = (useGlobal && Type.IndexOf ('.') >= 0 ? "global::" : string.Empty) + Type;

			if (!HasGenerics)
				return type;

			return type.Replace ("{0}", string.Join (", ", GenericArguments.Select (p => p.ToString (useGlobal))));
		}

		static List<string> ParseTypeList (string type)
		{
			var list = new List<string> ();

			// Only one type
			if (type.IndexOf (',') < 0) {
				list.Add (type);
				return list;
			}

			// Remove any whitespace
			type = type.Replace (" ", "");

			var start = 0;
			var counter = -1;
			var depth = 0;

			while (++counter < type.Length) {
				if (type [counter] == '<') {
					depth++;
					continue;
				}

				if (type [counter] == '>') {
					depth--;
					continue;
				}

				// This is a list separator, add the previous type
				if (depth == 0 && type [counter] == ',') {
					list.Add (type.Substring (start, counter - start));
					start = counter + 1;
					continue;
				}
			}

			// Add the final type
			list.Add (type.Substring (start, counter - start));

			return list;
		}
	}
}
