using System;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class NDKInfo : Task
	{
		[Required]
		public string NDKDirectory { get; set; }

		[Output]
		public string NDKRevision { get; set; }

		[Output]
		public string NDKVersionMajor { get; set; }

		[Output]
		public string NDKVersionMinor { get; set; }

		[Output]
		public string NDKVersionMicro { get; set; }

		[Output]
		public string NDKMinimumApiAvailable { get; set; }

		public override bool Execute ()
		{
			string props = Path.Combine (NDKDirectory, "source.properties");

			if (!File.Exists (props))
				Log.LogError ($"NDK version file not found at {props}");
			else
				GatherInfo (props);

			return !Log.HasLoggedErrors;
		}

		void GatherInfo (string props)
		{
			string[] lines = File.ReadAllLines (props);

			foreach (string l in lines) {
				string line = l.Trim ();
				string[] parts = line.Split (new char[] {'='}, 2);
				if (parts.Length != 2)
					continue;

				if (String.Compare ("Pkg.Revision", parts [0].Trim (), StringComparison.Ordinal) != 0)
					continue;

				string rev = parts [1].Trim ();
				NDKRevision = rev;

				Version ver;
				if (!Version.TryParse (rev, out ver)) {
					Log.LogError ($"Unable to parse NDK revision '{rev}' as a valid version string");
					return;
				}

				NDKVersionMajor = ver.Major.ToString ();
				NDKVersionMinor = ver.Minor.ToString ();
				NDKVersionMicro = ver.Build.ToString ();
				break;
			}

			int minimumApi = Int32.MaxValue;
			string platforms = Path.Combine (NDKDirectory, "platforms");
			foreach (string p in Directory.EnumerateDirectories (platforms, "android-*", SearchOption.TopDirectoryOnly)) {
				string pdir = Path.GetFileName (p);
				string[] parts = pdir.Split (new char[] { '-' }, 2);
				if (parts.Length != 2)
					continue;

				int api;
				if (!Int32.TryParse (parts [1].Trim (), out api))
					continue;

				if (api >= minimumApi)
					continue;

				minimumApi = api;
			}

			NDKMinimumApiAvailable = minimumApi.ToString ();
		}
	}
}
