using System;
using System.IO;
using System.Xml;

namespace MonoDroid.Generation
{

	public abstract class Ctor : MethodBase {

		protected Ctor (GenBase declaringType)
			: base (declaringType)
		{
		}
		
		public abstract bool IsNonStaticNestedType { get; }
		public abstract string CustomAttributes { get; }

		string jni_sig;
		public string JniSignature {
			get { return jni_sig; }
		}

		public string ID {
			get { return "id_ctor" + IDSignature; }
		}

		void GenerateCustomAttributes (StreamWriter sw, string indent)
		{
			if (CustomAttributes != null)
				sw.WriteLine("{0}{1}", indent, CustomAttributes);
			if (Annotation != null)
				sw.WriteLine ("{0}{1}", indent, Annotation);
		}

		public void Generate (StreamWriter sw, string indent, CodeGenerationOptions opt, bool use_base, ClassGen type)
		{
			string jni_sig = JniSignature;
			bool gen_string_overload =  Parameters.HasCharSequence && !type.ContainsCtor (jni_sig.Replace ("java/lang/CharSequence", "java/lang/String"));
			System.Collections.Specialized.StringCollection call_cleanup = Parameters.GetCallCleanup (opt);
			opt.CodeGenerator.WriteConstructorIdField (this, sw, indent, opt);
			sw.WriteLine ("{0}// Metadata.xml XPath constructor reference: path=\"{1}/constructor[@name='{2}'{3}]\"", indent, type.MetadataXPathReference, type.JavaSimpleName, Parameters.GetMethodXPathPredicate ());
			sw.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, ".ctor", jni_sig, String.Empty, this.AdditionalAttributeString ());
			if (Deprecated != null)
				sw.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, Deprecated.Replace ("\"", "\"\""));
			GenerateCustomAttributes (sw, indent);
			sw.WriteLine ("{0}{1} unsafe {2} ({3})\n{0}\t: {4} (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)",
					indent, Visibility, Name, GenBase.GetSignature (this, opt), use_base ? "base" : "this");
			sw.WriteLine ("{0}{{", indent);
			opt.CodeGenerator.WriteConstructorBody (this, sw, indent + "\t", opt, call_cleanup);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			if (gen_string_overload) {
				sw.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, ".ctor", jni_sig, String.Empty, this.AdditionalAttributeString ());
				sw.WriteLine ("{0}{1} unsafe {2} ({3})\n{0}\t: {4} (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)",
						indent, Visibility, Name, GenBase.GetSignature (this, opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"), use_base ? "base" : "this");
				sw.WriteLine ("{0}{{", indent);
				opt.CodeGenerator.WriteConstructorBody (this, sw, indent + "\t", opt, call_cleanup);
				sw.WriteLine ("{0}}}", indent);
				sw.WriteLine ();
			}
		}

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList tps)
		{
			if (!base.OnValidate (opt, tps))
				return false;
			jni_sig = "(" + Parameters.JniSignature + ")V";
			return true;
		}
	}
}
