#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	// Writes a `package.xml` next to an Android SDK component's `source.properties`,
	// matching the format produced by xaprepare's `Step_Android_SDK_NDK.WritePackageXmls()`.
	// Used for components (e.g. the emulator) where the upstream zip omits `package.xml` but
	// the Android SDK manager expects one to be present after install.
	public class GenerateAndroidPackageXml : Task
	{
		static readonly XNamespace AndroidRepositoryCommon  = "http://schemas.android.com/repository/android/common/01";
		static readonly XNamespace AndroidRepositoryGeneric = "http://schemas.android.com/repository/android/generic/01";

		[Required]
		public ITaskItem [] Directories { get; set; } = [];

		public override bool Execute ()
		{
			foreach (var dir in Directories) {
				string path = dir.ItemSpec;
				var properties = ReadSourceProperties (path);
				if (properties == null) {
					Log.LogMessage (MessageImportance.Low, $"Skipping '{path}', no source.properties file found.");
					continue;
				}
				string packageXml = Path.Combine (path, "package.xml");
				Log.LogMessage (MessageImportance.Low, $"Writing '{packageXml}'");
				var doc = new XDocument (
					new XElement (AndroidRepositoryCommon + "repository",
						new XAttribute (XNamespace.Xmlns + "ns2", AndroidRepositoryCommon.NamespaceName),
						new XAttribute (XNamespace.Xmlns + "ns3", AndroidRepositoryGeneric.NamespaceName),
						new XElement ("localPackage",
							new XAttribute ("path", properties ["Pkg.Path"]),
							new XAttribute ("obsolete", "false"),
							new XElement ("revision", GetRevision (properties ["Pkg.Revision"])),
							new XElement ("display-name", properties ["Pkg.Desc"]))));
				doc.Save (packageXml, SaveOptions.None);
			}

			return !Log.HasLoggedErrors;
		}

		static Dictionary<string, string>? ReadSourceProperties (string dir)
		{
			var path = Path.Combine (dir, "source.properties");
			if (!File.Exists (path))
				return null;
			var dict = new Dictionary<string, string> ();
			foreach (var line in File.ReadLines (path)) {
				if (line.Length == 0)
					continue;
				var entry = line.Split (new [] { '=' }, 2, StringSplitOptions.None);
				if (entry.Length != 2)
					continue;
				dict.Add (entry [0], entry [1]);
			}
			return dict;
		}

		static IEnumerable<XElement> GetRevision (string revision)
		{
			var parts = revision.Split ('.');
			if (parts.Length > 0)
				yield return new XElement ("major", parts [0]);
			if (parts.Length > 1)
				yield return new XElement ("minor", parts [1]);
			if (parts.Length > 2)
				yield return new XElement ("micro", parts [2]);
		}
	}
}
