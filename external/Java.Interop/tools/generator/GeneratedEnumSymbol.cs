using System;
using System.Collections.Generic;

namespace MonoDroid.Generation
{
	// This could be array and non-array, depending on metadata fixup.
	public class GeneratedEnumSymbol : ISymbol
	{
		string enum_type;
		bool is_array;
		
		public GeneratedEnumSymbol (string enumType)
		{
			if (enumType.EndsWith ("[]")) {
				enum_type = enumType.Substring (0, enumType.Length - 2);
				is_array = true;
			}
			else
				this.enum_type = enumType;
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			if (IsArray)
				return String.Format ("({0}[]) JNIEnv.GetArray ({1}, {2}, typeof ({0}))", opt.GetOutputName (enum_type), var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			else
				return String.Format ("({0}) {1}", opt.GetOutputName (enum_type), var_name);
		}
	
		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			if (IsArray)
				return String.Format ("JNIEnv.NewArray ({0})", var_name);
			else
				return String.Format ("({0}) {1}", JavaName, var_name);
		}
	
		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			return true;
		}
	
		public string DefaultValue {
			get { return IsArray ? "null" : "0"; }
		}
	
		public string FullName {
			get { return enum_type + (IsArray ? "[]" : String.Empty); }
		}
	
		public string JavaName {
			get { return IsArray ? "int[]" : "int"; }
		}
	
		public string JniName {
			get { return IsArray ? "[I" : "I"; }
		}
	
		public string NativeType {
			get { return IsArray ? "int[]" : "int"; }
		}

		public bool IsEnum {
			get { return true; }
		}

		public bool IsArray {
			get { return is_array; }
		}

		public string ElementType {
			get { return enum_type; }
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

