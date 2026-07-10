using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDroid.Generation {

	public class GenericSymbol : ISymbol {

		bool is_concrete;
		GenBase gen;
		string [] java_params;
		string tps;
		ISymbol [] type_params;

		public GenericSymbol (GenBase gen, string type_params)
		{
			this.gen = gen;
			java_params = GenericParameterList.Parse (type_params);
		}
		
		public string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		public string FullName {
			get { return gen.FullName; }
		}

		public GenBase Gen {
			get { return gen; }
		}

		public bool IsConcrete {
			get { return is_concrete; }
		}

		public string JavaName {
			get { return gen.JavaName; }
		}

		public string JniName {
			get { return gen.JniName; }
		}

		public string NativeType {
			get { return "IntPtr"; }
		}

		public bool IsEnum {
			get { return false; }
		}

		public bool IsArray {
			get { return false; }
		}

		public string ElementType {
			get { return null; }
		}

		public ISymbol [] TypeParams {
			get { return type_params; }
		}

		public string ReturnCast => string.Empty;

		public string GetObjectHandleProperty (CodeGenerationOptions opt, string variable)
		{
			return gen.GetObjectHandleProperty (opt, variable);
		}

		string MapTypeParams (Dictionary<string, string> mappings)
		{
			StringBuilder sb = new StringBuilder ();
			sb.Append ("<");
			foreach (var tp in type_params) {
				if (sb.Length > 1)
					sb.Append (", ");
				if (mappings.ContainsKey (tp.FullName))
					sb.Append (mappings[tp.FullName]);
				else
					sb.Append (tp.FullName);
			}
			sb.Append (">");
			return sb.ToString ();
		}

		public string FromNative (CodeGenerationOptions opt, string varname, bool owned)
		{
			return gen.FromNative (opt, varname, owned);
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			var rgm = gen as IRequireGenericMarshal;
			return gen.FullName + (rgm != null && !rgm.MayHaveManagedGenericArguments ? null : mappings == null ? tps : MapTypeParams (mappings));
		}

		public string ToNative (CodeGenerationOptions opt, string varname)
		{
			return gen.ToNative (opt, varname);
		}

		public string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings)
		{
			return varname;
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList in_params, CodeGeneratorContext context)
		{
			if (!gen.Validate (opt, in_params, context))
				return false;

			is_concrete = true;
			type_params = new ISymbol [java_params.Length];
			for (int i = 0; i < java_params.Length; i++) {
				string tp = java_params [i];
				var gpd = in_params != null ? in_params.FirstOrDefault (t => t.Name == tp) : null;
				if (gpd != null) {
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
				type_params [i] = /*psym is IGeneric ? (psym as IGeneric).GetGenericType (null) : psym.FullName*/ psym;
			}
			tps = type_params == null ? null : "<" + String.Join (", ", (from tp in type_params select tp.FullName).ToArray ()) + ">";
			return true;
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return gen.PreCallback (opt, var_name, owned);
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			return gen.PostCallback (opt, var_name);
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return gen.PreCall (opt, var_name);
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return gen.Call (opt, var_name);
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			return gen.PostCall (opt, var_name);
		}

		public bool NeedsPrep { get { return true; } }
	}
}

