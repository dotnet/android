using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CopyGeneratedJavaResourceClasses : Task
	{
		[Required]
		public string SourceTopDirectory { get; set; }
		[Required]
		public string DestinationTopDirectory { get; set; }
		[Required]
		public string PrimaryPackageName { get; set; }

		public string ExtraPackages { get; set; }
		[Output]
		public string PrimaryJavaResgenFile { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("SourceTopDirectory: {0}", SourceTopDirectory);
			Log.LogDebugMessage ("DestinationTopDirectory: {0}", DestinationTopDirectory);
			Log.LogDebugMessage ("PrimaryPackageName: {0}", PrimaryPackageName);
			Log.LogDebugMessage ("ExtraPackages: {0}", ExtraPackages);

			var list = new List<string> ();
			foreach (var pkg in GetPackages ()) {
				string subpath = Path.Combine (pkg.Split ('.'));
				string src = Path.Combine (SourceTopDirectory, subpath, "R.java");
				string dst = Path.Combine (DestinationTopDirectory, subpath, "R.java");

				if (!File.Exists (src))
					continue;

				var date = File.GetLastWriteTimeUtc (src);
				if (MonoAndroidHelper.CopyIfChanged (src, dst)) {
					MonoAndroidHelper.SetWriteable (dst);
					MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (dst, date, Log);
				}
				list.Add (dst);
			}
			// so far we only need the package's R.java for GenerateResourceDesigner input.
			PrimaryJavaResgenFile = list.FirstOrDefault ();

			Log.LogDebugMessage ("Output PrimaryJavaResgenFile: {0}", PrimaryJavaResgenFile);

			return true;
		}

		IEnumerable<string> GetPackages ()
		{
			yield return PrimaryPackageName.ToLowerInvariant ();
			if (!string.IsNullOrEmpty (ExtraPackages))
				foreach (var pkg in ExtraPackages.Split (':'))
					yield return pkg;
		}
	}
}

