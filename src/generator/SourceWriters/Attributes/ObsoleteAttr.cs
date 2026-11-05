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

		public ObsoleteAttr (string message = null, bool isError = false)
		{
			Message = message?.Replace ("\"", "\"\"").Trim ();
			IsError = isError;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var content = string.Empty;

			if (Message != null || IsError)
				content += $"@\"{Message}\"";

			if (IsError)
				content += ", error: true";

			if (content.HasValue ())
				writer.WriteLine ($"[global::System.Obsolete ({content})]");
			else
				writer.WriteLine ("[global::System.Obsolete]");
		}
	}
}
