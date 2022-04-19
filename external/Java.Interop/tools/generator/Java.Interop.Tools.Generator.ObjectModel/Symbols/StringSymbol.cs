using System;
using System.Collections.Generic;
using System.Xml;

using MonoDroid.Utils;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace MonoDroid.Generation {

	public class StringSymbol : ISymbol {

		public string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		public string FullName {
			get { return "string"; }
		}

		public bool IsGeneric {
			get { return false; }
		}

		public string JavaName {
			get { return "java.lang.String"; }
		}

		public string JniName {
			get { return "Ljava/lang/String;"; }
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

		public string GetObjectHandleProperty (CodeGenerationOptions opt, string variable)
		{
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return $"{variable}.PeerReference";
			}
			return $"((global::Java.Lang.Object) {variable}).Handle";
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return String.Format (
						"global::Java.Interop.JniEnvironment.Strings.ToString (ref {0}, JniObjectReferenceOptions.{1})",
						var_name,
						owned ? "CopyAndDispose" : "Copy");
			}
			return String.Format ("JNIEnv.GetString ({0}, {1})", var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
		}

		public string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null)
		{
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return String.Format ("global::Java.Interop.JniEnvironment.Strings.NewString ({0})", var_name);
			}
			return String.Format ("JNIEnv.NewString ({0})", var_name);
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			return true;
		}

		public string Call (CodeGenerationOptions opt, string var_name)
		{
			return opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			return new string [0];
		}

		public string[] PostCall (CodeGenerationOptions opt, string var_name)
		{
			string native_name  = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return new[]{
					$"global::Java.Interop.JniObjectReference.Dispose (ref {native_name});",
				};
			}
			return new string[]{
				string.Format ("JNIEnv.DeleteLocalRef ({0});", opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name))),
			};
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return new string[] { String.Format ("var {0} = JNIEnv.GetString ({1}, JniHandleOwnership.DoNotTransfer);", opt.GetSafeIdentifier (var_name), opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name))) };
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			string managed_name = opt.GetSafeIdentifier (var_name);
			string native_name  = opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name));
			if (opt.CodeGenerationTarget == CodeGenerationTarget.JavaInterop1) {
				return new[]{
					$"var {native_name} = global::Java.Interop.JniEnvironment.Strings.NewString ({managed_name});",
				};
			}
			return new[]{
				$"IntPtr {native_name} = JNIEnv.NewString ((string{opt.NullableOperator}){managed_name});",
			};
		}

		public bool NeedsPrep { get { return true; } }
	}
}

