using System;
using Xamarin.SourceWriter;

using Java.Interop.Tools.Generator;

namespace generator.SourceWriters
{
	public class ObsoletedOSPlatformAttr : AttributeWriter
	{
		public string Message { get; set; }
		public AndroidSdkVersion Version { get; }

		public ObsoletedOSPlatformAttr (string message, AndroidSdkVersion version)
		{
			Message = message;
			Version = version;
		}

		public override void WriteAttribute (CodeWriter writer)
		{
			var apiLevel = Version.MinorRelease == 0
				? $"{Version.ApiLevel}.0"
				: Version.ToString ();

			if (Message.HasValue ())
				writer.WriteLine ($"[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android{apiLevel}\", @\"{Message.Replace ("\"", "\"\"")}\")]");
			else
				writer.WriteLine ($"[global::System.Runtime.Versioning.ObsoletedOSPlatform (\"android{apiLevel}\")]");
		}
	}
}
