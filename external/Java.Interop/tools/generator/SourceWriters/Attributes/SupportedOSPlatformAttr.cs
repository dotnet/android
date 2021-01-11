using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class SupportedOSPlatformAttr : AttributeWriter
	{
		public int Version { get; }

		public SupportedOSPlatformAttr (int version) => Version = version;

		public override void WriteAttribute (CodeWriter writer)
		{
			writer.WriteLine ($"[global::System.Runtime.Versioning.SupportedOSPlatformAttribute (\"android{Version}.0\")]");
		}
	}
}
