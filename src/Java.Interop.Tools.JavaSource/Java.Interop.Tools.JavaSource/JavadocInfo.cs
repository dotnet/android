using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Irony.Ast;
using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource {

	sealed class JavadocInfo {
		public  readonly    ICollection<XNode>  Exceptions  = new Collection<XNode> ();
		public  readonly    ICollection<XNode>  Extra       = new Collection<XNode> ();
		public  readonly    ICollection<XNode>  Remarks     = new Collection<XNode> ();
		public  readonly    ICollection<XNode>  Parameters  = new Collection<XNode> ();
		public  readonly    ICollection<XNode>  Returns     = new Collection<XNode> ();

		public override string ToString ()
		{
			return new XElement ("Javadoc",
					new XElement (nameof (Parameters),  Parameters),
					new XElement (nameof (Remarks),     Remarks),
					new XElement (nameof (Returns),     Returns),
					new XElement (nameof (Exceptions),  Exceptions),
					new XElement (nameof (Extra),       Extra))
			.ToString ();
		}
	}
}
