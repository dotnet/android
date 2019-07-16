using System;
using System.Xml;
using System.Collections.Generic;


namespace MonoDroid.Generation {

	public class EnumSymbol : ISymbol {
		
		string type;

		public EnumSymbol (string type)
		{
			this.type = type;
		}
		
		public string DefaultValue {
			get { return "0"; }
		}

		public string FullName {
			get { return type; }
		}

		public string JavaName {
			get { return "int"; }
		}

		public string JniName {
			get { return "I"; }
		}

		public string NativeType {
			get { return "int"; }
		}

		public bool IsEnum {
			get { return true; }
		}

		public bool IsArray {
			get { return false; }
		}

		public string ElementType {
			get { return null; }
		}

		public string GetObjectHandleProperty (string variable)
		{
			return null;
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string varname, bool owned)
		{
			return String.Format ("({0}) {1}", opt.GetOutputName (type), varname);
		}

		public string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null)
		{
			return "(int) " + varname;
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			return true;
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			throw new NotSupportedException (string.Format ("{0} does not support PreCallback", this.GetType ().Name));
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			throw new NotSupportedException (string.Format ("{0} does not support PostCallback", this.GetType ().Name));
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			throw new NotSupportedException (string.Format ("{0} does not support PreCall", this.GetType ().Name));
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			throw new NotSupportedException (string.Format ("{0} does not support Call", this.GetType ().Name));
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			throw new NotSupportedException (string.Format ("{0} does not support PostCall", this.GetType ().Name));
		}

		public bool NeedsPrep { get { return false; } }
	}
}

