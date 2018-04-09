using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class GetNugetPackageBasePath : Task
	{
		[Required]
		public ITaskItem [] PackageConfigFiles { get; set; }

		[Required]
		public string PackageName { get; set; }

		[Output]
		public ITaskItem BasePath { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, "Task GetNugetPackageBasePath");
			Log.LogMessage (MessageImportance.Low, "\tPackageName : {0}", PackageName);
			Log.LogMessage (MessageImportance.Low, "\tPackageConfigFiles : ");
			foreach (ITaskItem file in PackageConfigFiles) {
				Log.LogMessage (MessageImportance.Low, "\t\t{0}", file.ItemSpec);
			}

			Version latest = null;
			foreach (string file in PackageConfigFiles.Select (x => Path.GetFullPath (x.ItemSpec)).Distinct ().OrderBy (x => x)) {
				if (!File.Exists (file)) {
					Log.LogWarning ("\tPackages config file {0} not found", file);
					continue;
				}

				Version tmp = GetPackageVersion (file);
				if (latest != null && latest >= tmp)
					continue;
				latest = tmp;
			}

			if (latest == null)
				Log.LogError ("NuGet Package '{0}' not found", PackageName);
			else
				BasePath = new TaskItem (Path.Combine ("packages", $"{PackageName}.{latest}"));
			Log.LogMessage (MessageImportance.Low, $"BasePath == {BasePath}");
			return !Log.HasLoggedErrors;
		}

		Version GetPackageVersion (string packageConfigFile)
		{
			Version ret = null;
			var doc = new XmlDocument ();
			doc.Load (packageConfigFile);

			XmlNodeList nodes = doc.DocumentElement.SelectNodes ($"./package[@id='{PackageName}']");
			foreach (XmlNode n in nodes) {
				var e = n as XmlElement;
				if (e == null)
					continue;

				Version tmp;
				if (!Version.TryParse (e.GetAttribute ("version"), out tmp))
					continue;
				if (ret != null && ret >= tmp)
					continue;
				ret = tmp;
			}

			return ret;
		}
	}
}
