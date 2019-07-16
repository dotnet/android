using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDroid.Generation {

	public class GenericParameterList {

		public static string[] Parse (string type_params)
		{
			type_params = type_params.Replace ("? extends ", String.Empty);
			type_params = type_params.Replace ("? super ", String.Empty);
			int depth = 0;
			StringBuilder sb = new StringBuilder ();
			List<string> parms = new List<string> ();
			for (int i = 1; i < type_params.Length - 1; i++) {
				char c = type_params [i];
				if (c == ',' && depth == 0) {
					parms.Add (sb.ToString ().TrimStart ());
					sb.Remove (0, sb.Length);
					continue;
				}
				switch (c) {
				case '<':
					depth++;
					break;
				case '>':
					depth--;
					break;
				default:
					break;
				}
				sb.Append (c);
			}
			if (sb.Length > 0)
				parms.Add (sb.ToString ().TrimStart ());

			return parms.ToArray ();
		}

		bool is_concrete;
		bool is_valid;
		bool validated;
		string managed;
		string [] java_params;
		ISymbol [] type_params;

		public GenericParameterList (string type_params)
		{
			java_params = Parse (type_params);
		}

		public bool IsConcrete {
			get { return is_concrete; }
		}

		public ISymbol [] TypeParams {
			get { return type_params; }
		}

		public override string ToString ()
		{
			return managed;
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList in_params, CodeGeneratorContext context)
		{
			if (validated)
				return is_valid;

			validated = true;
			is_concrete = true;
			type_params = new  ISymbol [java_params.Length];
			for (int i = 0; i < java_params.Length; i++) {
				string tp = java_params [i].TrimStart ();
				var gpd = in_params != null ? in_params.FirstOrDefault (t => t.Name == tp) : null;
				if (in_params != null && gpd != null) {
					type_params [i] = new GenericTypeParameter (gpd);
					is_concrete = false;
					continue;
				} else if (tp == "?") {
					if (in_params != null && in_params.Count == 1) {
						type_params [i] = new GenericTypeParameter (in_params [0]);
						is_concrete = false;
					} else
						type_params [i] = new SimpleSymbol ("null", "java.lang.Object", "object", "Ljava/lang/Object;");
					continue;
				}

				ISymbol psym = opt.SymbolTable.Lookup (tp, in_params);
				if (psym == null || !psym.Validate (opt, in_params, context))
					return false;
				

				if (psym is GenericSymbol && !(psym as GenericSymbol).IsConcrete)
					is_concrete = false;
				type_params [i] = /*psym is IGeneric ? (psym as IGeneric).GetGenericType (null) :*/ psym;
			}
			managed = "<" + String.Join (", ", (from tp in type_params select tp.FullName).ToArray ()) + ">";
			is_valid = true;
			return true;
		}
	}
}

