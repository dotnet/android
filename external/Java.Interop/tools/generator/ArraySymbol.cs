using System;
using System.Collections.Generic;
using System.Xml;

using MonoDroid.Utils;

namespace MonoDroid.Generation {

	public class ArraySymbol : ISymbol {

		static ISymbol byte_sym = new SimpleSymbol ("0", "byte", "byte", "B");

		ISymbol sym;
		bool is_params;

		public ArraySymbol (ISymbol sym)
		{
			if (sym.FullName == "sbyte")
				this.sym = byte_sym;
			else
				this.sym = sym;
		}

		public string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		public string ElementType {
			get {
				return sym.FullName;
			}
		}

		public string FullName {
			get { return (is_params ? "params " : String.Empty) + ElementType + "[]"; }
		}

		public bool IsGeneric {
			get { return !string.IsNullOrEmpty (sym.GetGenericType (null)); }
		}

		public bool IsParams {
			get { return is_params; }
			set { is_params = value; }
		}

		public string JavaName {
			get { return is_params ? sym.JavaName + "..." : sym.JavaName + "[]"; }
		}

		public string JniName {
			get { return "[" + sym.JniName; }
		}

		public string NativeType {
			get { return "IntPtr"; }
		}

		public bool IsEnum {
			get { return false; }
		}

		public bool IsArray {
			get { return true; }
		}

		public string GetObjectHandleProperty (string variable)
		{
			return sym.GetObjectHandleProperty (variable);
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return String.Format ("({0}[]) JNIEnv.GetArray ({1}, {2}, typeof ({3}))", opt.GetOutputName (ElementType), var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer", opt.GetOutputName (sym.FullName));
		}

		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			return String.Format ("JNIEnv.NewArray ({0})", var_name);
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			return sym.Validate (opt, type_params);
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return opt.GetSafeIdentifier (SymbolTable.GetNativeName (var_name));
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			string[] result = new string [2];
			result [0] = String.Format ("if ({0} != null)", opt.GetSafeIdentifier (var_name));
			result [1] = String.Format ("\tJNIEnv.CopyArray ({0}, {1});", opt.GetSafeIdentifier (var_name), opt.GetSafeIdentifier (SymbolTable.GetNativeName (var_name)));
			return result;
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			string native_name = opt.GetSafeIdentifier (SymbolTable.GetNativeName (var_name));
			string[] result = new string [4];
			result [0] = String.Format ("if ({0} != null) {{", var_name);
			result [1] = String.Format ("\tJNIEnv.CopyArray ({0}, {1});", native_name, var_name);
			result [2] = String.Format ("\tJNIEnv.DeleteLocalRef ({0});", native_name);
			result [3] = "}";
			return result;
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return new string[] { String.Format ("{0}[] {1} = ({0}[]) JNIEnv.GetArray ({2}, JniHandleOwnership.DoNotTransfer, typeof ({3}));", opt.GetOutputName (ElementType), opt.GetSafeIdentifier (var_name), opt.GetSafeIdentifier (SymbolTable.GetNativeName (var_name)), opt.GetOutputName (sym.FullName)) };
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[] { String.Format ("IntPtr {0} = JNIEnv.NewArray ({1});", opt.GetSafeIdentifier (SymbolTable.GetNativeName (var_name)), opt.GetSafeIdentifier (var_name)) };
		}

		public bool NeedsPrep { get { return true; } }
	}
}

