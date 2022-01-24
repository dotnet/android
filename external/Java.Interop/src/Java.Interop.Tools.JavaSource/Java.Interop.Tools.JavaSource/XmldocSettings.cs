using System;
using System.Xml.Linq;

namespace Java.Interop.Tools.JavaSource
{
	public class XmldocSettings
	{
		public string DocRootValue { get; set; } = string.Empty;
		public XElement []? ExtraRemarks { get; set; }
		public XmldocStyle Style { get; set; } = XmldocStyle.Full;
	}
}
