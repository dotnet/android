using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class ObsoleteAttr : AttributeWriter
	{
		public string Message { get; set; }
		public bool IsError { get; set; }
		public bool NoAtSign { get; set; }		// TODO: Temporary to match unit tests
		public bool WriteEmptyString { get; set; }      // TODO: Temporary to match unit tests
		public bool WriteAttributeSuffix { get; set; }  // TODO: Temporary to match unit tests
		public bool WriteGlobal { get; set; }           // TODO: Temporary to match unit tests

		public ObsoleteAttr (string message = null, bool isError = false)
		{
			Message = message;
			IsError = isError;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var attr_name = WriteAttributeSuffix ? "ObsoleteAttribute" : "Obsolete";

			if (WriteGlobal)
				attr_name = "global::System." + attr_name;

			if (Message is null && !WriteEmptyString && !IsError) {
				writer.WriteLine ($"[{attr_name}]");
				return;
			}

			writer.Write ($"[{attr_name} ({(NoAtSign ? "" : "@")}\"{Message}\"");

			if (IsError)
				writer.Write (", error: true");

			writer.WriteLine (")]");
		}
	}
}
