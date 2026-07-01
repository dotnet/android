using System;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class IntDefinitionAttr : AttributeWriter
	{
		public string ManagedMember { get; set; }
		public string JniField { get; set; }

		public IntDefinitionAttr (string managedMember, string jniField)
		{
			ManagedMember = managedMember;
			JniField = jniField;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var member = ManagedMember is null ? "null" : "\"" + ManagedMember + "\"";
			writer.WriteLine ($"[global::Android.Runtime.IntDefinition ({member}, JniField = \"{JniField}\")]");
		}
	}
}
