using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Mono.Cecil;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	// Represents a generic parameter definition in GenBase.
	public class GenericParameterDefinition
	{
		public GenericParameterDefinition (string name, string [] constraints)
		{
			Name = name;
			ConstraintExpressions = constraints;
		}
		
		public string Name { get; set; }
		public string [] ConstraintExpressions { get; private set; }
		
		// post-validation information.
		public ISymbol [] Constraints { get; private set; }

		bool validated, is_valid;

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			if (ConstraintExpressions == null || ConstraintExpressions.Length == 0)
				return true;
			if (validated)
				return is_valid;
			var syms = new List<ISymbol> ();
			foreach (var c in ConstraintExpressions) {
				var sym = opt.SymbolTable.Lookup (c, type_params);
				if (sym == null) {
					Report.Warning (0, Report.WarningGenericParameterDefinition + 0, "Unknown generic argument constraint type {0} {1}.", c, context.ContextString);
					validated = true;
					return false;
				}
				syms.Add (sym);
			}
			Constraints = syms.ToArray ();
			validated = is_valid = true;
			return true;
		}
	}
	
	public class GenericParameterDefinitionList : List<GenericParameterDefinition>
	{
		public static GenericParameterDefinitionList Merge (GenericParameterDefinitionList l1, GenericParameterDefinitionList l2)
		{
			if (l1 == null)
				return l2;
			if (l2 == null)
				return l1;
			var ret = new GenericParameterDefinitionList ();
			ret.AddRange (l1);
			ret.AddRange (l2);
			return ret;
		}
		
		public static GenericParameterDefinitionList FromMetadata (IEnumerable<GenericParameter> types)
		{
			GenericParameterDefinitionList ret = null;
			foreach (var p in types) {
				ret = ret ?? new GenericParameterDefinitionList ();
				ret.Add (new GenericParameterDefinition (
					p.FullNameCorrected (),
					(from a in p.Constraints
					 select a.ConstraintType.FullNameCorrected ()).ToArray ()));
			}
			return ret;
		}
		
		public static GenericParameterDefinitionList FromXml (XElement tps)
		{
			var ret = new GenericParameterDefinitionList ();
			var tpl = tps.Elements ("typeParameter");
			foreach (var n in tpl) {
				var csts = new List<string> ();
				foreach (var x in n.XPathSelectElements ("genericConstraints/genericConstraint"))
					csts.Add (x.XGetAttribute ("type"));
				ret.Add (new GenericParameterDefinition (n.XGetAttribute ("name"), csts.ToArray ()));
			}
			return ret;
		}

		public string ToGeneratedAttributeString ()
		{
			var typeArgList = this.Select (t => t.Name + (t.ConstraintExpressions.Any () ? " extends " + string.Join (" & ", t.ConstraintExpressions) : null));
			return "[global::Java.Interop.JavaTypeParameters (new string [] {\"" +
			                      string.Join ("\", \"", typeArgList) + "\"})]";
		}
		
		public int IndexOf (string name)
		{
			int i = 0;
			foreach (var e in this) {
				if (e.Name == name)
					return i;
				else
					i++;
			}
			return -1;
		}
		
		public string GetSignature ()
		{
			return Count == 0 ? null : '<' + String.Join (",", (from p in this select p.Name).ToArray ()) + '>';
		}
		
		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			foreach (var pd in this)
				if (!pd.Validate (opt, type_params, context))
					return false;
			return true;
		}
	}
}


