using System;
using System.Collections.Generic;
using System.Xml;

using MonoDroid.Utils;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace MonoDroid.Generation {

	public class ArraySymbol : ISymbol {

		static ISymbol byte_sym = new SimpleSymbol ("0", "byte", "byte", "B");

		ISymbol sym;
		bool is_params;

		[Obsolete ("The code generation target is no longer used. Use ArraySymbol (ISymbol) instead.")]
		public ArraySymbol (ISymbol sym, CodeGenerationTarget target)
			: this (sym)
		{
		}

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
			get {
				return (is_params ? "params " : String.Empty) + ElementType + "[]";
			}
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

		public string ReturnCast => string.Empty;

		public string GetObjectHandleProperty (CodeGenerationOptions opt, string variable)
		{
			return sym.GetObjectHandleProperty (opt, variable);
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return String.Format ("({0}[]{4}) JNIEnv.GetArray ({1}, {2}, typeof ({3}))", opt.GetOutputName (ElementType), var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer", opt.GetOutputName (sym.FullName), opt.NullableOperator);
		}

		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			return String.Format ("JNIEnv.NewArray ({0})", var_name);
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			return sym.Validate (opt, type_params, context);
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			string managed_name = opt.GetSafeIdentifier (var_name);
			string native_name  = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
			return new[]{
				$"if ({managed_name} != null)",
				$"\tJNIEnv.CopyArray ({managed_name}, {native_name});",
			};
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			string managed_name = opt.GetSafeIdentifier (var_name);
			string native_name  = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
			return new[]{
				$"if ({managed_name} != null) {{",
				$"\tJNIEnv.CopyArray ({native_name}, {managed_name});",
				$"\tJNIEnv.DeleteLocalRef ({native_name});",
				$"}}",
			};
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return new string[] { String.Format ("var {1} = ({0}[]{4}) JNIEnv.GetArray ({2}, JniHandleOwnership.DoNotTransfer, typeof ({3}));", opt.GetOutputName (ElementType), opt.GetSafeIdentifier (var_name), opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)), opt.GetOutputName (sym.FullName), opt.NullableOperator) };
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[] { String.Format ("IntPtr {0} = JNIEnv.NewArray ({1});", opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)), opt.GetSafeIdentifier (var_name)) };
		}

		public bool NeedsPrep { get { return true; } }

	}
}
