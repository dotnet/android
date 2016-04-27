using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.IO;
using System.Xml.Linq;
using Xamarin.Android.Build.Utilities;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{
	public class PrepareWearApplicationFiles : Task
	{
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

		public override bool Execute ()
		{
			Log.LogDebugMessage ("PrepareWearApplicationFiles task");
			Log.LogDebugTaskItems ("  WearAndroidManifestFile:", WearAndroidManifestFile);
			Log.LogDebugTaskItems ("  IntermediateOutputPath:", IntermediateOutputPath);
			Log.LogDebugTaskItems ("  WearApplicationApkPath:", WearApplicationApkPath);

			string rawapk = "wearable_app.apk";
			string intermediateApkPath = Path.Combine (IntermediateOutputPath, "res", "raw", rawapk);
			string intermediateXmlFile = Path.Combine (IntermediateOutputPath, "res", "xml", "wearable_app_desc.xml");

			var doc = XDocument.Load (WearAndroidManifestFile);
			var wearPackageName = AndroidAppManifest.CanonicalizePackageName (doc.Root.Attribute ("package").Value);

			if (PackageName != wearPackageName)
				Log.LogCodedError ("XA5211", "Embedded wear app package name differs from handheld app package name ({0} != {1}).", wearPackageName, PackageName);

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

			MonoAndroidHelper.CopyIfChanged (WearApplicationApkPath, intermediateApkPath);

			Directory.CreateDirectory (Path.GetDirectoryName (intermediateXmlFile));
			if (!File.Exists (intermediateXmlFile) || !XDocument.DeepEquals (XDocument.Load (intermediateXmlFile), XDocument.Parse (xml))) {
				File.WriteAllText (intermediateXmlFile, xml);
				Log.LogDebugMessage ("    Created additional resource as {0}", intermediateXmlFile);
			}
			WearableApplicationDescriptionFile = new TaskItem (intermediateXmlFile);
			WearableApplicationDescriptionFile.SetMetadata ("IsWearApplicationResource", "True");
			BundledWearApplicationApkResourceFile = new TaskItem (intermediateApkPath);
			BundledWearApplicationApkResourceFile.SetMetadata ("IsWearApplicationResource", "True");

			return true;
		}
	}
}
