using System;
using System.Text.RegularExpressions;
using System.IO;

namespace Xamarin.Android.Build.Utilities
{
	public class SatelliteAssembly {
		// culture match courtesy: http://stackoverflow.com/a/3962783/83444
		static readonly Regex SatelliteChecker = new Regex (
			Regex.Escape (Path.DirectorySeparatorChar.ToString ()) +
			"(?<culture>[a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*)" +
			Regex.Escape (Path.DirectorySeparatorChar.ToString ()) +
			string.Format ("(?<file>[^{0}]+.resources.dll)$", Regex.Escape (Path.DirectorySeparatorChar.ToString ())));

		public static bool TryGetSatelliteCultureAndFileName (string assemblyPath, out string culture, out string fileName)
		{
			culture = fileName = null;

			var m = SatelliteChecker.Match (assemblyPath);
			if (!m.Success)
				return false;

			culture   = m.Groups ["culture"].Value;
			fileName  = m.Groups ["file"].Value;
			return true;
		}
	}
}

