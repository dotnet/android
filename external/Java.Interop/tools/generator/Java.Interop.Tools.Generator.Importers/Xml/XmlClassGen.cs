using Xamarin.Android.Tools;
using System.Xml.Linq;

namespace MonoDroid.Generation
{
	public class XmlClassGen : ClassGen {
		bool is_abstract;
		bool is_final;
		string base_type;

		public XmlClassGen (XElement pkg, XElement elem)
			: base (new XmlGenBaseSupport (pkg, elem))//FIXME: should not be xml specific
		{
			is_abstract = elem.XGetAttribute ("abstract") == "true";
			is_final = elem.XGetAttribute ("final") == "true";
			base_type = elem.XGetAttribute ("extends");
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
				case "constructor":
					Ctors.Add (new XmlCtor (this, child));
					break;
				case "field":
					AddField (new XmlField (child));
					break;
				case "typeParameters":
					break; // handled at GenBaseSupport
				default:
					Report.Warning (0, Report.WarningClassGen + 1, "unexpected class child {0}.", child.Name);
					break;
				}
			}
		}
		
		public override bool IsAbstract {
			get { return is_abstract; }
		}
		
		public override bool IsFinal {
			get { return is_final; }
		}
		
		public override string BaseType {
			get { return base_type; }
			set { base_type = value; }
		}
	}
}

