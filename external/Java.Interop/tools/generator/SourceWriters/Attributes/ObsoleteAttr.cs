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
			Message = message;
			IsError = isError;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var parts = new List<string> ();

			if (Message != null)
				parts.Add ($"@\"{Message}\"");

			if (IsError)
				parts.Add ("error: true");

			var content = string.Join (", ", parts.ToArray ());

			if (content.HasValue ())
				writer.WriteLine ($"[global::System.Obsolete ({content})]");
			else
				writer.WriteLine ("[global::System.Obsolete]");
		}
	}
}
