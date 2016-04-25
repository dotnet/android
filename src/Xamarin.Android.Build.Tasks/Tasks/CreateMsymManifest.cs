using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Xml.Linq;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public class CreateMsymManifest : Task
	{
		[Required]
		public string BuildId { get; set; }

		[Required]
		public string PackageName { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		public override bool Execute ()
		{
			XDocument doc = new XDocument (
				new XElement ("mono-debug", new XAttribute("version", "1"),
					new XElement ("app-id", PackageName),
					new XElement ("build-date", DateTime.UtcNow.ToString ("O")),
					new XElement ("build-id", BuildId))
			);
			doc.Save (Path.Combine (OutputDirectory, "manifest.xml"));
			return !Log.HasLoggedErrors;
		}
	}
}

