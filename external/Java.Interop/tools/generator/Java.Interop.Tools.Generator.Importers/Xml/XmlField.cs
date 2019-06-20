using System;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	public class XmlField : Field {

		XElement elem;
		string java_name;
		string name;
		string enum_type;

		public XmlField (XElement elem)
		{
			this.elem = elem;
			java_name = elem.XGetAttribute ("name");
			if (elem.Attribute ("managedName") != null)
				name = elem.XGetAttribute ("managedName");
			else
				name = SymbolTable.StudlyCase (Char.IsLower (java_name [0]) || java_name.ToLower ().ToUpper () != java_name ? java_name : java_name.ToLower ());
			if (elem.Attribute ("enumType") != null)
				enum_type = elem.XGetAttribute ("enumType");
		}

		public override bool IsDeprecated {
			get { return elem.XGetAttribute ("deprecated") != "not deprecated"; }
		}

		public override string DeprecatedComment {
			get { return elem.XGetAttribute ("deprecated"); }
		}

		public override bool IsEnumified {
			get { return enum_type != null; }
		}

		public override bool IsFinal {
			get { return elem.XGetAttribute ("final") == "true"; }
		}

		public override bool IsStatic {
			get { return elem.XGetAttribute ("static") == "true"; }
		}

		public override string Name { 
			get { return name; }
			set { name = value; }
		}

		public override string JavaName { 
			get { return java_name; }
		}
		
		public override string TypeName {
			get { return enum_type ?? elem.XGetAttribute ("type"); }
		}

		public override string Value {
			get { 
				string val = elem.XGetAttribute ("value"); // do not trim
				if (!String.IsNullOrEmpty (val) && Symbol != null && Symbol.FullName == "char")
					val = "(char)" + val;
				return val;
			}
		}

		public override string Visibility {
			get { return elem.XGetAttribute ("visibility"); }
		}

		protected override Parameter SetterParameter {
			get {
				var p = Parameter.FromElement (elem);
				p.Name = "value";
				return p;
			}
		}
	}
}
