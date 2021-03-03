using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GetManifestVersions : AndroidTask
	{
		public override string TaskPrefix => "GAP";

		public ITaskItem ManifestFile { get; set; }

		[Output]
		public string VersionCode { get; set; }

		[Output]
		public string VersionName { get; set; }

		public override bool RunTask ()
		{
			if (!string.IsNullOrEmpty (ManifestFile.ItemSpec) && File.Exists (ManifestFile.ItemSpec)) {
				using var stream = File.OpenRead (ManifestFile.ItemSpec);
				using var reader = XmlReader.Create (stream);
				if (reader.MoveToContent () == XmlNodeType.Element) {
					var versionCode = reader.GetAttribute ("versionCode");
					if (!string.IsNullOrEmpty (versionCode)) {
						VersionCode = versionCode;
					}
					var versionName = reader.GetAttribute ("versionName");
					if (!string.IsNullOrEmpty (versionName)) {
						VersionName = versionCode;
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
