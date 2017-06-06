using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation {
#if USE_CECIL
	public class ManagedCtor : Ctor {
		MethodDefinition m;
		string name;
		bool is_acw;

		public ManagedCtor (GenBase declaringType, MethodDefinition m)
			: this (declaringType, m, new ManagedMethodBaseSupport (m))
		{
		}
		
		ManagedCtor (GenBase declaringType, MethodDefinition m, ManagedMethodBaseSupport support)
			: base (declaringType, support)
		{
			this.m = m;
			name = m.Name;
			// If 'elem' is a constructor for a non-static nested type, then
			// the type of the containing class must be inserted as the first
			// argument
			if (IsNonStaticNestedType)
				Parameters.AddFirst (Parameter.FromManagedType (m.DeclaringType.DeclaringType, DeclaringType.JavaName));
			var regatt = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			foreach (var p in support.GetParameters (regatt))
				Parameters.Add (p);
		}

		public override bool IsAcw {
			get { return is_acw; }
		}
		
		public override bool IsNonStaticNestedType {
			// not a beautiful way to check static type, yes :|
			get { return m.DeclaringType.IsNested && !(m.DeclaringType.IsAbstract && m.DeclaringType.IsSealed); }
		}

		public override string Name {
			get { return name; }
			set { name = value; }
		}

		public override string CustomAttributes {
			get { return null; }
		}
	}
#endif

	public class XmlCtor : Ctor {
		string name;
		bool nonStaticNestedType;
		bool missing_enclosing_class;
		string custom_attributes;

		public XmlCtor (GenBase declaringType, XElement elem) : base (declaringType, new XmlMethodBaseSupport (elem))
		{
			name = elem.XGetAttribute ("name");
			int idx = name.LastIndexOf ('.');
			if (idx > 0)
				name = name.Substring (idx + 1);
			// If 'elem' is a constructor for a non-static nested type, then
			// the type of the containing class must be inserted as the first
			// argument
			nonStaticNestedType = idx > 0 && elem.Parent.Attribute ("static").Value == "false";
			if (nonStaticNestedType) {
				string     declName              = elem.Parent.XGetAttribute ("name");
				string     expectedEnclosingName = declName.Substring (0, idx);
				XElement enclosingType         = GetPreviousClass (elem.Parent.PreviousNode, expectedEnclosingName);
				if (enclosingType == null) {
					missing_enclosing_class = true;
					Report.Warning (0, Report.WarningCtor + 0, "For {0}, could not find enclosing type '{1}'.", name, expectedEnclosingName);
				}
				else
					Parameters.AddFirst (Parameter.FromClassElement (enclosingType));
			}
			
			foreach (var child in elem.Elements ()) {
				if (child.Name == "parameter")
					Parameters.Add (Parameter.FromElement (child));
			}

			if (elem.Attribute ("customAttributes") != null)
				custom_attributes = elem.XGetAttribute ("customAttributes");
		}

		static XElement GetPreviousClass (XNode n, string nameValue)
		{
			XElement e = null;
			while (n != null &&
			       ((e = n as XElement) == null ||
			        e.Name != "class" ||
			        !e.XGetAttribute ("name").StartsWith (nameValue, StringComparison.Ordinal) ||
			        // this complicated check (instead of simple name string equivalence match) is required for nested class inside a generic class e.g. android.content.Loader.ForceLoadContentObserver.
			        (e.XGetAttribute ("name") != nameValue && e.XGetAttribute ("name").IndexOf ('<') < 0))) {
				n = n.PreviousNode;
			}
			return (XElement) e;
		}

		public override bool IsNonStaticNestedType {
			get { return nonStaticNestedType; }
		}

		public override string Name {
			get { return name; }
			set { name = value; }
		}

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList tps)
		{
			if (missing_enclosing_class)
				return false;
			return base.OnValidate (opt, tps);
		}

		public override string CustomAttributes {
			get { return custom_attributes; }
		}
	}

	public abstract class Ctor : MethodBase {

		protected Ctor (GenBase declaringType, IMethodBaseSupport support)
			: base (declaringType, support)
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
