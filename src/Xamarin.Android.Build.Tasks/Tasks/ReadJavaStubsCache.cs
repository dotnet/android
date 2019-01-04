using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Xml.Linq;

namespace Xamarin.Android.Tasks
{
	public class ReadJavaStubsCache : Task
	{
		[Required]
		public string CacheFile { get; set; }

		[Output]
		public bool EmbeddedDSOsEnabled { get; set; }

		public override bool Execute ()
		{
			if (File.Exists (CacheFile)) {
				var doc = XDocument.Load (CacheFile);
				string text = doc.Element ("Properties")?.Element (nameof (EmbeddedDSOsEnabled))?.Value;
				if (bool.TryParse (text, out bool value))
					EmbeddedDSOsEnabled = value;
			}

			return !Log.HasLoggedErrors;
		}
	}
}
