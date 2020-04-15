using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Xml.Linq;

using Xamarin.Android.Tools;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tasks
{
	public class ParseAndroidWearProjectAndManifest : AndroidTask
	{
		public override string TaskPrefix => "PAW";

		static readonly XNamespace msbuildNS = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

		public ITaskItem [] ProjectFiles { get; set; }
		[Output]
		public string ApplicationManifestFile { get; set; }
		[Output]
		public string ApplicationPackageName { get; set; }

		public override bool RunTask ()
		{
			if (ProjectFiles.Length != 1)
				Log.LogCodedError ("XA1015", Properties.Resources.XA1015);
			
			var wearProj = ProjectFiles.First ();
			var manifestXml = XDocument.Load (wearProj.ItemSpec)
				.Root.Elements (msbuildNS + "PropertyGroup").Elements (msbuildNS + "AndroidManifest").Select (e => e.Value).FirstOrDefault ();
			if (string.IsNullOrEmpty (manifestXml))
				Log.LogCodedError ("XA1016", Properties.Resources.XA1016, wearProj);
			manifestXml = Path.Combine (Path.GetDirectoryName (wearProj.ItemSpec), manifestXml.Replace ('\\', Path.DirectorySeparatorChar));

			ApplicationManifestFile = manifestXml;

			Log.LogDebugMessage ("  [Output] ApplicationManifestFile: " + ApplicationManifestFile);

			ApplicationPackageName = AndroidAppManifest.CanonicalizePackageName (XDocument.Load (manifestXml).Root.Attributes ("package").Select (a => a.Value).FirstOrDefault ());
			if (string.IsNullOrEmpty (ApplicationPackageName))
				Log.LogCodedError ("XA1017", Properties.Resources.XA1017);
			
			Log.LogDebugMessage ("  [Output] ApplicationPackageName: " + ApplicationPackageName);
			return true;
		}
	}
}

