using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class UnsupportedOSPlatformAttr : AttributeWriter
	{
		public int Version { get; }

		public UnsupportedOSPlatformAttr (int version) => Version = version;

		public override void WriteAttribute (CodeWriter writer)
		{
			writer.WriteLine ($"[global::System.Runtime.Versioning.UnsupportedOSPlatformAttribute (\"android{Version}.0\")]");
		}
	}
}
