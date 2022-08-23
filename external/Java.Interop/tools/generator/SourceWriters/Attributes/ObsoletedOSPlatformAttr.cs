using System;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class ObsoletedOSPlatformAttr : AttributeWriter
	{
		public string Message { get; set; }
		public int Version { get; }

		public ObsoletedOSPlatformAttr (string message, int version)
		{
			Message = message;
			Version = version;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			if (Message.HasValue ())
				writer.WriteLine ($"[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android{Version}.0\", @\"{Message.Replace ("\"", "\"\"")}\")]");
			else
				writer.WriteLine ($"[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android{Version}.0\")]");
		}
	}
}
