using System;
using System.Collections.Generic;

namespace MonoDroid.Generation {

	public class StreamSymbol : ISymbol {

		string base_name;
		string java_name;
		string jni_name;

		public StreamSymbol (string name) : this (name, name) {}

		public StreamSymbol (string name, string base_name)
		{
			this.base_name = base_name;
			java_name = "java.io." + name;
			jni_name = "Ljava/io/" + name + ";";
		}
			
		public string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		public string FullName {
			get { return "System.IO.Stream"; }
		}

		public bool IsGeneric {
			get { return false; }
		}

		public string JavaName {
			get { return java_name; }
		}

		public string JniName {
			get { return jni_name; }
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

		public string ReturnCast => string.Empty;

		public string GetObjectHandleProperty (string variable)
		{
			return $"((global::Java.Lang.Object) {variable}).Handle";
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return String.Format (opt.GetOutputName ("Android.Runtime.{0}Invoker") + ".FromJniHandle ({1}, {2})", base_name, var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
		}

		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			return String.Format (opt.GetOutputName ("Android.Runtime.{0}Adapter") + ".ToLocalJniHandle ({1})", base_name, var_name);
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			return true;
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[] {
				string.Format ("IntPtr {0} = global::Android.Runtime.{1}Adapter.ToLocalJniHandle ({2});",
						opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)),
						base_name,
						opt.GetSafeIdentifier (var_name)),
			};
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[]{
				string.Format ("JNIEnv.DeleteLocalRef ({0});",
						opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name))),
			};
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return new string[]{
				string.Format ("var {0} = global::Android.Runtime.{1}Invoker.FromJniHandle ({2}, {3});",
						var_name,
						base_name,
						opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)),
						owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer"),
			};
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			return new string [0];
		}

		public bool NeedsPrep { get { return true; } }
	}
}
