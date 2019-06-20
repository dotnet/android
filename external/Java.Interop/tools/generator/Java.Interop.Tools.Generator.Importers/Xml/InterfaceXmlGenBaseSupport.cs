using System.Xml.Linq;

namespace MonoDroid.Generation
{
	public class InterfaceXmlGenBaseSupport : XmlGenBaseSupport
	{
		public InterfaceXmlGenBaseSupport (XElement pkg, XElement elem)
			: base (pkg, elem)
		{
		}
		
		public override string TypeNamePrefix {
			get { return (IsPrefixableName (RawName) ? "I" : string.Empty); }
		}
	}
}


