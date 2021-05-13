using System;
using System.Collections.Generic;


namespace MonoDroid.Generation {

	public class GenericTypeParameter : ISymbol {
		
		string type;
		string java_type;
		string jni_type;
		GenericParameterDefinition parm;

		public GenericTypeParameter (GenericParameterDefinition parm)
		{
			if (parm == null)
				throw new ArgumentNullException ("parm");
			this.parm = parm;
#if false // FIXME: we want to enable generic type constraints to get valid JNI output. So far we "remove" any method that involves generic parameter constraints.
			type = parm.Constraints != null ? parm.Constraints [0].FullName : "Java.Lang.Object";
			java_type = parm != null && parm.Constraints != null && parm.Constraints [0] != null ? parm.Constraints [0].JavaName : "java.lang.Object";
#else
			type = "Java.Lang.Object";
			java_type = parm != null && parm.Constraints != null && parm.Constraints [0] != null ? parm.Constraints [0].JavaName : "java.lang.Object";
#endif
			// some unknown types could result in "null" constraints items. So far we treat this as JLO.
			jni_type = parm.Constraints != null && parm.Constraints [0] != null ? parm.Constraints [0].JniName : "Ljava/lang/Object;";
		}
		
		public GenericParameterDefinition Definition {
			get { return parm; }
		}
		
		public string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		public string FullName {
			get { return type; }
		}

		public string JavaName {
			get { return java_type; }
		}

		public string JniName {
			get { return jni_type; }
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
			return null;
		}

		public string FromNative (CodeGenerationOptions opt, string varname, bool owned)
		{
			return String.Format ("({0}{4}) global::Java.Lang.Object.GetObject<{3}> ({1}, {2})", opt.GetOutputName (type), varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer", opt.GetOutputName (FullName), opt.NullableOperator);
		}

		public string GetGenericType (Dictionary<string, string> mappings)
		{
			var mapped = mappings != null && mappings.ContainsKey (parm.Name) ? mappings [parm.Name] : null;
			return mapped ?? parm.Name;
		}

		public string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null)
		{
			var mapped = mappings != null && mappings.ContainsKey (parm.Name) ? mappings [parm.Name] : null;
			string targetType = opt.GetOutputName (mappings == null ? parm.Name : mapped);
			if (targetType == "string")
				return string.Format ("new global::Java.Lang.String ({0})", varname);
			return varname;
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			return true;
		}

		public string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return new string[]{
				string.Format ("var {1} = global::Java.Lang.Object.GetObject<{0}> ({2}, {3});",
						opt.GetOutputName (FullName),
						var_name,
						opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)),
						owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer"),
			};
		}

		public string[] PostCallback (CodeGenerationOptions opt, string var_name)
		{
			return new string[]{
			};
		}

		public string[] PreCall (CodeGenerationOptions opt, string var_name)
		{
			return new string[] {
				string.Format ("IntPtr {0} = JNIEnv.ToLocalJniHandle ({1});",
						opt.GetSafeIdentifier (TypeNameUtilities.GetNativeName (var_name)), opt.GetSafeIdentifier (var_name)),
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

		public bool NeedsPrep { get { return true; } }
	}
}

