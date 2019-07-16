using System;
using System.Collections.Generic;


namespace MonoDroid.Generation {

	public class FormatSymbol : ISymbol {
		
		string default_value;
		string from_fmt;
		string java_type;
		string jni_type;
		string native_type;
		string to_fmt;
		string type;

		public FormatSymbol (string default_value, string java_type, string jni_type, string native_type, string type, string from_fmt, string to_fmt)
		{
			this.default_value = default_value;
			this.java_type = java_type;
			this.jni_type = jni_type;
			this.native_type = native_type;
			this.type = type;
			this.from_fmt = from_fmt;
			this.to_fmt = to_fmt;
		}
		
		public string DefaultValue {
			get { return default_value; }
		}

		public string FullName {
			get { return type; }
		}

		public string FullOutputName {
			get { return FullName; }
		}

		public string JavaName {
			get { return java_type; }
		}

		public string JniName {
			get { return jni_type; }
		}

		public string NativeType {
			get { return native_type; }
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
			return String.Format (from_fmt, varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
		}

		public string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null)
		{
			return String.Format (to_fmt, varname);
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

