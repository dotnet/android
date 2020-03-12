using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Xml.Linq;
using Xamarin.Android.Tools;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	public class PrepareWearApplicationFiles : AndroidTask
	{
		public override string TaskPrefix => "PWA";

		static readonly XNamespace androidNs = XNamespace.Get ("http://schemas.android.com/apk/res/android");

		[Required]
		public string PackageName { get; set; }
		public string WearAndroidManifestFile { get; set; }
		public string IntermediateOutputPath { get; set; }
		public string WearApplicationApkPath { get; set; }
		[Output]
		public ITaskItem WearableApplicationDescriptionFile { get; set; }
		[Output]
		public ITaskItem BundledWearApplicationApkResourceFile { get; set; }
		[Output]
		public string [] ModifiedFiles { get; set; }

		public override bool RunTask ()
		{
			string rawapk = "wearable_app.apk";
			string intermediateApkPath = Path.Combine (IntermediateOutputPath, "res", "raw", rawapk);
			string intermediateXmlFile = Path.Combine (IntermediateOutputPath, "res", "xml", "wearable_app_desc.xml");

			var doc = XDocument.Load (WearAndroidManifestFile);
			var wearPackageName = AndroidAppManifest.CanonicalizePackageName (doc.Root.Attribute ("package").Value);
			var modified = new List<string> ();

			if (PackageName != wearPackageName)
				Log.LogCodedError ("XA5211", Properties.Resources.XA5211, wearPackageName, PackageName);

			if (!File.Exists (WearApplicationApkPath)) {
				Log.LogWarning ("This application won't contain the paired Wear package because the Wear application package .apk is not created yet. If you are using MSBuild or XBuild, you have to invoke \"SignAndroidPackage\" target.");
				return true;
			}

			var xml = string.Format (@"<wearableApp package=""{0}"">
  <versionCode>{1}</versionCode>
  <versionName>{2}</versionName>
  <rawPathResId>{3}</rawPathResId>
</wearableApp>
", wearPackageName, doc.Root.Attribute (androidNs + "versionCode").Value, doc.Root.Attribute (androidNs + "versionName").Value, Path.GetFileNameWithoutExtension (rawapk));

			if (MonoAndroidHelper.CopyIfChanged (WearApplicationApkPath, intermediateApkPath)) {
				Log.LogDebugMessage ("    Copied APK to {0}", intermediateApkPath);
				modified.Add (intermediateApkPath);
			}

			Directory.CreateDirectory (Path.GetDirectoryName (intermediateXmlFile));
			if (!File.Exists (intermediateXmlFile) || !XDocument.DeepEquals (XDocument.Load (intermediateXmlFile), XDocument.Parse (xml))) {
				File.WriteAllText (intermediateXmlFile, xml);
				Log.LogDebugMessage ("    Created additional resource as {0}", intermediateXmlFile);
				modified.Add (intermediateXmlFile);
			}
			WearableApplicationDescriptionFile = new TaskItem (intermediateXmlFile);
			WearableApplicationDescriptionFile.SetMetadata ("IsWearApplicationResource", "True");
			BundledWearApplicationApkResourceFile = new TaskItem (intermediateApkPath);
			BundledWearApplicationApkResourceFile.SetMetadata ("IsWearApplicationResource", "True");
			ModifiedFiles = modified.ToArray ();

			return true;
		}
	}
}
