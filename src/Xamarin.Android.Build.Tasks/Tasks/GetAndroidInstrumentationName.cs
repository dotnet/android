#nullable enable
using System;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Finds the first &lt;instrumentation&gt; element in an AndroidManifest.xml
	/// and returns its android:name attribute value.
	/// </summary>
	public class GetAndroidInstrumentationName : AndroidTask
	{
		public override string TaskPrefix => "GAIN";

		[Required]
		public string ManifestFile { get; set; } = "";

		[Output]
		public string? InstrumentationName { get; set; }

		public override bool RunTask ()
		{
			var manifest = AndroidAppManifest.Load (ManifestFile, MonoAndroidHelper.SupportedVersions);
			var androidNs = AndroidAppManifest.AndroidXNamespace;
			var doc = manifest.Document;

			var instrumentation = doc?.Root?.Element ("instrumentation")
				?? throw new InvalidOperationException ("No <instrumentation> element found in AndroidManifest.xml.");
			InstrumentationName = instrumentation.Attribute (androidNs + "name")?.Value;
			if (string.IsNullOrEmpty (InstrumentationName))
				throw new InvalidOperationException ("The <instrumentation> element in AndroidManifest.xml is missing the android:name attribute.");

			return !Log.HasLoggedErrors;
		}
	}
}
