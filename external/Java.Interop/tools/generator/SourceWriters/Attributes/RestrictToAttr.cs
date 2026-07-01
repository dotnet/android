using System;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class RestrictToAttr : AttributeWriter
	{
		bool is_type;

		public RestrictToAttr (bool isType)
		{
			is_type = isType;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			writer.WriteLine ($"[global::System.Obsolete (\"While this {(is_type ? "type" : "member")} is 'public', Google considers it internal API and reserves the right to modify or delete it in the future. Use at your own risk.\", DiagnosticId = \"XAOBS001\")]");
		}
	}
}
