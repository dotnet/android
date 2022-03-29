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
		CodeGenerationTarget target;

		public ArraySymbol (ISymbol sym, CodeGenerationTarget target)
		{
			this.target = target;
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
				if (!is_params && target == CodeGenerationTarget.JavaInterop1) {
					return GetJavaInterop1MarshalType ();
				}
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
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				var transfer = "JniObjectReferenceOptions." + (owned ? "CopyAndDispose" : "Copy");
				return $"global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::{GetJavaInterop1MarshalType ()}>" +
					$"(ref {var_name}, {transfer})";
			}
			return String.Format ("({0}[]{4}) JNIEnv.GetArray ({1}, {2}, typeof ({3}))", opt.GetOutputName (ElementType), var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer", opt.GetOutputName (sym.FullName), opt.NullableOperator);
		}

		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return $"global::{GetJavaInterop1MarshalMethod ()} ({var_name})";
			}
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
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return new string[]{
					$"throw new NotSupportedException (\"ArraySymbol.PostCallback\");",
				};
			}
			return new[]{
				$"if ({managed_name} != null)",
				$"\tJNIEnv.CopyArray ({managed_name}, {native_name});",
			};
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			string managed_name = opt.GetSafeIdentifier (var_name);
			string native_name  = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				if (IsParams) {
					return new[]{
						$"if ({native_name} != null) {{",
						$"\t{native_name}.CopyTo ({managed_name}!, 0);",
						$"\t{native_name}.Dispose ();",
						$"}}",
					};
				}
				return new[]{
					$"if ({native_name} != null) {{",
					$"\t{native_name}.DisposeUnlessReferenced ();",
					$"}}",
				};
			}
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
			string managed_name = opt.GetSafeIdentifier (var_name);
			string native_name  = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return new[]{
					$"var {native_name} = global::{GetJavaInterop1MarshalMethod ()} ({managed_name});",
				};
			}
			return new string[] { String.Format ("IntPtr {0} = JNIEnv.NewArray ({1});", opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)), opt.GetSafeIdentifier (var_name)) };
		}

		public bool NeedsPrep { get { return true; } }

		string GetJavaInterop1MarshalMethod()
		{
			var typeParam = ElementType switch {
				"string" => "string",
				_ => $"global::{ElementType}",
			};
			return sym.JniName switch {
				"B" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalSByteArray",
				"C" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalCharArray",
				"D" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalDoubleArray",
				"F" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalSingleArray",
				"I" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalInt32Array",
				"J" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalInt64Array",
				"S" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalInt16Array",
				"V" => throw new InvalidOperationException ("`void` cannot be used as an array type."),
				"Z" => "Java.Interop.JniEnvironment.Arrays.CreateMarshalBooleanArray",
				_ => $"Java.Interop.JniEnvironment.Arrays.CreateMarshalObjectArray<{typeParam}>",
			};
		}

		string GetJavaInterop1MarshalType ()
		{
			return sym.JniName switch {
				"B" => "Java.Interop.JavaSByteArray",
				"C" => "Java.Interop.JavaCharArray",
				"D" => "Java.Interop.JavaDoubleArray",
				"F" => "Java.Interop.JavaSingleArray",
				"I" => "Java.Interop.JavaInt32Array",
				"J" => "Java.Interop.JavaInt64Array",
				"S" => "Java.Interop.JavaInt16Array",
				"V" => throw new InvalidOperationException ("`void` cannot be used as an array type."),
				"Z" => "Java.Interop.JavaBooleanArray",
				_   => $"Java.Interop.JavaObjectArray<{ElementType}>",
			};
		}
	}
}

