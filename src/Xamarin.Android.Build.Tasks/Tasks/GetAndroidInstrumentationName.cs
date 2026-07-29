#nullable enable
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

			var instrumentation = manifest.Document?.Root?.Element ("instrumentation");
			if (instrumentation == null) {
				Log.LogCodedError ("XA1043", Properties.Resources.XA1043, ManifestFile);
				return !Log.HasLoggedErrors;
			}

			InstrumentationName = instrumentation.Attribute (androidNs + "name")?.Value;
			if (InstrumentationName.IsNullOrEmpty ())
				Log.LogCodedError ("XA1042", Properties.Resources.XA1042, ManifestFile);

			return !Log.HasLoggedErrors;
		}
	}
}
