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

		/// <summary>
		/// Set when the application was already found to have no launchable
		/// &lt;activity&gt;, in which case a missing &lt;instrumentation&gt; element
		/// means there is nothing at all to launch and XA1043 is reported.
		/// Otherwise a missing &lt;instrumentation&gt; is reported as XA1048.
		/// </summary>
		public bool NoLaunchableActivity { get; set; }

		[Output]
		public string? InstrumentationName { get; set; }

		public override bool RunTask ()
		{
			var manifest = AndroidAppManifest.Load (ManifestFile, MonoAndroidHelper.SupportedVersions);
			var androidNs = AndroidAppManifest.AndroidXNamespace;

			var instrumentation = manifest.Document?.Root?.Element ("instrumentation");
			if (instrumentation == null) {
				if (NoLaunchableActivity) {
					Log.LogCodedError ("XA1043", Properties.Resources.XA1043, ManifestFile);
				} else {
					Log.LogCodedError ("XA1048", Properties.Resources.XA1048, ManifestFile);
				}
				return !Log.HasLoggedErrors;
			}

			InstrumentationName = instrumentation.Attribute (androidNs + "name")?.Value;
			if (InstrumentationName.IsNullOrEmpty ())
				Log.LogCodedError ("XA1042", Properties.Resources.XA1042, ManifestFile);

			return !Log.HasLoggedErrors;
		}
	}
}
