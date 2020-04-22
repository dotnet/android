using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using MonoDroid.Utils;

namespace MonoDroid.Generation {

	public class ReturnValue {

		ISymbol sym;
		string java_type;
		string managed_type;
		string raw_type;
		bool is_enumified;

		public ReturnValue (Method owner, string java_type, string managed_type, bool isEnumified, bool notNull)
		{
			this.raw_type = this.java_type = java_type;
			this.managed_type = managed_type;
			this.is_enumified = isEnumified;
			NotNull = notNull;
		}

		public string CallMethodPrefix => TypeNameUtilities.GetCallPrefix (sym);

		public string DefaultValue {
			get { return sym.DefaultValue; }
		}

		public string FullName {
			get {
				if (!String.IsNullOrEmpty (managed_type))
					return managed_type;
				return sym.FullName;
				//return sym is GenBase && !String.IsNullOrEmpty ((sym as GenBase).Marshaler) ? (sym as GenBase).Marshaler : sym.FullName;
			}
		}
		
		public void SetGeneratedEnumType (string enumType)
		{
			sym = new GeneratedEnumSymbol (enumType);
			is_enumified = true;
			managed_type = null;
			java_type = sym.JavaName;
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			return sym.GetGenericType (mappings) ?? FullName;
		}

		public bool IsVoid {
			get { return java_type == "void"; }
		}

		public bool IsArray {
			get { return sym.IsArray; }
		}

		public bool IsEnumified {
			get { return is_enumified; }
		}

		public bool IsGeneric {
			get { return sym is GenericSymbol ? !(sym as GenericSymbol).IsConcrete : !string.IsNullOrEmpty (sym.GetGenericType (null)); }
		}

		public string JavaName {
			get { return sym.JavaName; }
		}

		public string JniName {
			get { return sym.JniName; }
		}

		public string NativeType {
			get { return sym.NativeType; }
		}

		public bool NotNull { get; set; }

		public string RawJavaType {
			get { return raw_type; }
		}

		public string ReturnCast => sym?.ReturnCast ?? string.Empty;

		public ISymbol Symbol => sym;

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			if (!string.IsNullOrEmpty (managed_type) && (sym is ClassGen || sym is InterfaceGen)) {
				return string.Format ("global::Java.Lang.Object.GetObject<{0}> ({1}, {2})", 
				                      opt.GetOutputName (managed_type), var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
 			}
			return sym.FromNative (opt, var_name, owned);
		}

		public string ToNative (CodeGenerationOptions opt, string var_name)
		{
			return ((sym is GenericTypeParameter) || (sym is GenericSymbol)) ? String.Format ("JNIEnv.ToLocalJniHandle ({0})", var_name) : sym.ToNative (opt, var_name);
		}

		public string GetGenericReturn (CodeGenerationOptions opt, string name, Dictionary<string, string> mappings)
		{
			string targetType = sym.GetGenericType (mappings);
			if (string.IsNullOrEmpty (targetType))
				return name;
			if (targetType == "string")
				return string.Format ("{0}?.ToString ()", name);
			var rgm = opt.SymbolTable.Lookup (targetType) as IRequireGenericMarshal;
			return string.Format ("global::Java.Interop.JavaObjectExtensions.JavaCast<{0}>({1}){2}",
			                      rgm != null ? (rgm.GetGenericJavaObjectTypeOverride () ?? sym.FullName) : sym.FullName,
			                      opt.GetSafeIdentifier (rgm != null ? rgm.ToInteroperableJavaObject (name) : name),
			                      opt.NullForgivingOperator); 
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			sym = (IsEnumified ? opt.SymbolTable.Lookup (managed_type, type_params) : null) ?? opt.SymbolTable.Lookup (java_type, type_params);
			if (sym == null) {
				Report.Warning (0, Report.WarningReturnValue + 0, "Unknown return type {0} {1}.", java_type, context.ContextString);
				return false;
			}
			if (!sym.Validate (opt, type_params, context)) {
				Report.Warning (0, Report.WarningReturnValue + 1, "Invalid return type {0} {1}.", java_type, context.ContextString);
				return false;
			}
			return true;
		}
	}
}

