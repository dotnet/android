using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	internal class AssemblyIdentityMap
	{
		List<string> map = new List<string> ();

		public void Load (string mapFile)
		{
			map.Clear ();
			if (string.IsNullOrWhiteSpace (mapFile) || !File.Exists (mapFile))
				return;
			foreach (var s in File.ReadLines (mapFile)) {
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
				return map.IndexOf (assemblyIdentity).ToString ();
			}
			map.Add (assemblyIdentity);
			return map.IndexOf (assemblyIdentity).ToString ();
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

		public void Save (string mapFile)
		{
			if (map.Count == 0)
				return;
			var sb = new StringBuilder ();
			foreach (var item in map) {
				sb.AppendLine (item);
			}
			File.WriteAllText (mapFile, sb.ToString ());
		}
	}
}
