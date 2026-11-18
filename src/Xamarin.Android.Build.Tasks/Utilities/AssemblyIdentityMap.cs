#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	internal class AssemblyIdentityMap
	{
		List<string> map = new List<string> ();

		public void Load (string mapFile) => Load (mapFile, TaskEnvironment.Fallback);

		public void Load (string mapFile, TaskEnvironment taskEnvironment)
		{
			map.Clear ();
			if (mapFile.IsNullOrWhiteSpace () || !File.Exists (taskEnvironment.GetAbsolutePath (mapFile)))
				return;
			foreach (var s in File.ReadLines (taskEnvironment.GetAbsolutePath (mapFile))) {
				if (!map.Contains (s))
					map.Add (s);
			}
		}

		/// <summary>
		/// Returns the index of the file in $(_AndroidLibrayProjectAssemblyMapFile): map.cache
		/// Adds the file to the cache if it doesn't exist.
		/// </summary>
		/// <param name="assemblyIdentity">The file name including extension.</param>
		public string GetLibraryImportDirectoryNameForAssembly (string assemblyIdentity)
		{
			if (map.Contains (assemblyIdentity)) {
				return map.IndexOf (assemblyIdentity).ToString (CultureInfo.InvariantCulture);
			}
			map.Add (assemblyIdentity);
			return map.IndexOf (assemblyIdentity).ToString (CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Returns the name of the assembly for the provided `lp` directory.
		/// </summary>
		/// <param name="dir">The directory in the `lp` directory which we want to resolve.</param>
		public string GetAssemblyNameForImportDirectory (string dir)
		{
			if (!int.TryParse (dir, out int index))
				return string.Empty;
			if (index < 0 || index > map.Count)
				return string.Empty;
			return map [index];
		}

		public void Save (string mapFile) => Save (mapFile, TaskEnvironment.Fallback);

		public void Save (string mapFile, TaskEnvironment taskEnvironment)
		{
			if (map.Count == 0)
				return;
			var sb = new StringBuilder ();
			foreach (var item in map) {
				sb.AppendLine (item);
			}
			File.WriteAllText (taskEnvironment.GetAbsolutePath (mapFile), sb.ToString ());
		}
	}
}
