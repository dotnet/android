#nullable enable
using System.Xml;
using Microsoft.Build.Framework;
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
			using var reader = XmlReader.Create (ManifestFile);
			while (reader.Read ()) {
				if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "instrumentation") {
					InstrumentationName = reader.GetAttribute ("name", ManifestDocument.AndroidXmlNamespace.ToString ());
					if (InstrumentationName.IsNullOrEmpty ()) {
						Log.LogError ("The <instrumentation> element is missing the android:name attribute.");
						return false;
					}
					return !Log.HasLoggedErrors;
				}
			}

			Log.LogError ("No <instrumentation> element found in AndroidManifest.xml.");
			return false;
		}
	}
}
