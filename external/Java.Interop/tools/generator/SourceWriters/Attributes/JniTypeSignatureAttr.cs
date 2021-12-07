using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class JniTypeSignatureAttr : AttributeWriter
	{
		public string SimpleReference { get; set; }
		public bool GenerateJavaPeer { get; set; }

		public JniTypeSignatureAttr (string simpleReference, bool generateJavaPeer)
		{
			SimpleReference     = simpleReference;
			GenerateJavaPeer    = generateJavaPeer;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var sb = new StringBuilder ()
				.Append ("[global::Java.Interop.JniTypeSignature (\"")
				.Append (SimpleReference)
				.Append ("\", ")
				.Append ("GenerateJavaPeer=")
				.Append (GenerateJavaPeer ? "true" : "false")
				.Append (")]");
			writer.WriteLine (sb.ToString ());
		}
	}
}
