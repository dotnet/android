using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Xml.Linq;

using Xamarin.AndroidTools;
using Xamarin.Android.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ParseAndroidWearProjectAndManifest : Task
	{
		static readonly XNamespace msbuildNS = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

		public ITaskItem [] ProjectFiles { get; set; }
		[Output]
		public string ApplicationManifestFile { get; set; }
		[Output]
		public string ApplicationPackageName { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ParseAndroidWearProjectAndManifest task");
			Log.LogDebugTaskItems ("  ProjectFiles:", ProjectFiles);
			if (ProjectFiles.Length != 1)
				Log.LogError ("More than one Android Wear project is specified as the paired project. It can be at most one.");
			
			var wearProj = ProjectFiles.First ();
			var manifestXml = XDocument.Load (wearProj.ItemSpec)
				.Root.Elements (msbuildNS + "PropertyGroup").Elements (msbuildNS + "AndroidManifest").Select (e => e.Value).FirstOrDefault ();
			if (string.IsNullOrEmpty (manifestXml))
				Log.LogError ("Target Wear application's project '{0}' does not specify required 'AndroidManifest' project property.", wearProj);
			manifestXml = Path.Combine (Path.GetDirectoryName (wearProj.ItemSpec), manifestXml.Replace ('\\', Path.DirectorySeparatorChar));

			ApplicationManifestFile = manifestXml;

			Log.LogDebugMessage ("  [Output] ApplicationManifestFile: " + ApplicationManifestFile);

			ApplicationPackageName = AndroidAppManifest.CanonicalizePackageName (XDocument.Load (manifestXml).Root.Attributes ("package").Select (a => a.Value).FirstOrDefault ());
			if (string.IsNullOrEmpty (ApplicationPackageName))
				Log.LogError ("Target Wear application's AndroidManifest.xml does not specify required 'package' attribute.");
			
			Log.LogDebugMessage ("  [Output] ApplicationPackageName: " + ApplicationPackageName);
			return true;
		}
	}
}

