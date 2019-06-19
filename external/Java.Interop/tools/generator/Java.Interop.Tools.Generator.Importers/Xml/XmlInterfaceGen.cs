using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	public class XmlInterfaceGen : InterfaceGen {

		string args_type;

		public XmlInterfaceGen (XElement pkg, XElement elem) 
			: base (new InterfaceXmlGenBaseSupport (pkg, elem))
		{
			hasManagedName = elem.Attribute ("managedName") != null;
			args_type = elem.XGetAttribute ("argsType");
			foreach (var child in elem.Elements ()) {
				switch (child.Name.LocalName) {
				case "implements":
					string iname = child.XGetAttribute ("name-generic-aware");
					iname = iname.Length > 0 ? iname : child.XGetAttribute ("name");
					AddInterface (iname);
					break;
				case "method":
					AddMethod (new XmlMethod (this, child));
					break;
				case "field":
					AddField (new XmlField (child));
					break;
				case "typeParameters":
					break; // handled at GenBaseSupport
				default:
					Report.Warning (0, Report.WarningInterfaceGen + 0, "unexpected interface child {0}.", child);
					break;
				}
			}
		}
		
		public override string ArgsType {
			get { return args_type; }
		}
	}
}

