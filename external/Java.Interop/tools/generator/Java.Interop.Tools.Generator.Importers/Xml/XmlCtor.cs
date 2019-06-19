using System;
using System.Xml.Linq;

using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	public class XmlCtor : Ctor {
		XElement elem;
		string name;
		bool nonStaticNestedType;
		bool missing_enclosing_class;
		string custom_attributes;

		public XmlCtor (GenBase declaringType, XElement elem) : base (declaringType)
		{
			this.elem = elem;
			GenericArguments = elem.GenericArguments ();
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

		public override string Deprecated => elem.Deprecated ();

		public override string Visibility => elem.Visibility ();
	}
}
