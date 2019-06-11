using MonoDroid.Utils;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
#endif // HAVE_CECIL

	public class XmlMethod : Method {

		XElement elem;

		public XmlMethod (GenBase declaringType, XElement elem)
			: base (declaringType)
		{
			this.elem = elem;
			GenericArguments = elem.GenericArguments ();
			is_static = elem.XGetAttribute ("static") == "true";
			is_virtual = !is_static && elem.XGetAttribute ("final") == "false";
			if (elem.Attribute ("managedName") != null)
				name = elem.XGetAttribute ("managedName");
			else
				name = StringRocks.MemberToPascalCase (JavaName);

			is_abstract = elem.XGetAttribute ("abstract") == "true";
			if (declaringType is InterfaceGen)
				is_interface_default_method = !is_abstract && !is_static;

			GenerateDispatchingSetter = elem.Attribute ("generateDispatchingSetter") != null;

			foreach (var child in elem.Elements ()) {
				if (child.Name == "parameter")
					Parameters.Add (Parameter.FromElement (child));
			}
			FillReturnType ();
		}

		// core XML-based properties
		public override string Deprecated => elem.Deprecated ();

		public override string Visibility => elem.Visibility ();

		public override string ArgsType {
			get {
				var a = elem.Attribute ("argsType");
				if (a == null)
					return null;
				return a.Value;
			}
		}

		public override string EventName {
			get {
				var a = elem.Attribute ("eventName");
				if (a == null)
					return null;
				return a.Value;
			}
		}

		bool is_abstract;
		public override bool IsAbstract {
			get { return is_abstract; }
		}

		public override bool IsFinal {
			get { return elem.XGetAttribute ("final") == "true"; }
		}

		bool is_interface_default_method;
		public override bool IsInterfaceDefaultMethod {
			get { return is_interface_default_method; }
		}

		public override string JavaName {
			get { return elem.XGetAttribute ("name"); }
		}

		bool is_static;
		public override bool IsStatic {
			get { return is_static; }
		}

		bool is_virtual;
		public override bool IsVirtual {
			get { return is_virtual; }
			set { is_virtual = value; }
		}

		string name;
		public override string Name {
			get { return name; }
			set { name = value; }
		}
		
		// FIXME: this should not require enumReturn. Somewhere in generator uses this property improperly.
		public override string Return {
			get { return IsReturnEnumified ? elem.XGetAttribute ("enumReturn") : elem.XGetAttribute ("return"); }
		}
		
		public override string ManagedReturn {
			get { return IsReturnEnumified ? elem.XGetAttribute ("enumReturn") : elem.XGetAttribute ("managedReturn"); }
		}
		
		public override bool IsReturnEnumified {
			get { return elem.Attribute ("enumReturn") != null; }
		}

		protected override string PropertyNameOverride {
			get { return elem.XGetAttribute ("propertyName"); }
		}

		static readonly Regex ApiLevel = new Regex (@"api-(\d+).xml");
		public override int SourceApiLevel {
			get {
				string source = elem.XGetAttribute ("merge.SourceFile");
				if (source == null)
					return 0;
				Match m = ApiLevel.Match (source);
				if (!m.Success)
					return 0;
				int api;
				if (int.TryParse (m.Groups [1].Value, out api))
					return api;
				return 0;
			}
		}

		public override bool Asyncify {
			get {
				if (IsOverride)
					return false;

				return elem.Attribute ("generateAsyncWrapper") != null;
			}
		}

		public override string CustomAttributes {
			get { return elem.XGetAttribute ("customAttributes"); }
		}
	}
}

