using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

using Java.Interop.Tools.Generator;

namespace generator.SourceWriters
{
	public class UnsupportedOSPlatformAttr : AttributeWriter
	{
		public AndroidSdkVersion Version { get; }

		public UnsupportedOSPlatformAttr (AndroidSdkVersion version) => Version = version;

		public override void WriteAttribute (CodeWriter writer)
		{
			var apiLevel = Version.MinorRelease == 0
				? $"{Version.ApiLevel}.0"
				: Version.ToString ();
			writer.WriteLine ($"[global::System.Runtime.Versioning.UnsupportedOSPlatformAttribute (\"android{apiLevel}\")]");
		}
	}
}
