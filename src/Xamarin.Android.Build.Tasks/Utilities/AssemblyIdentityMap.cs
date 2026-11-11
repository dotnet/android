#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Xamarin.Android.Tasks
{
	internal class AssemblyIdentityMap
	{
		// Written next to map.cache by the _AndroidExtractAarLibrariesInputs target, one `id=path` line per .aar
		internal const string AarLibraryMapFileName = "aar.map";

		List<string> map = new List<string> ();
		Dictionary<string, string> aarMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		public void Load (string mapFile)
		{
			map.Clear ();
			aarMap.Clear ();
			if (mapFile.IsNullOrWhiteSpace ())
				return;
			LoadAarMap (Path.Combine (Path.GetDirectoryName (mapFile) ?? "", AarLibraryMapFileName));
			if (!File.Exists (mapFile))
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
				return map.IndexOf (assemblyIdentity).ToString (CultureInfo.InvariantCulture);
			}
			map.Add (assemblyIdentity);
			return map.IndexOf (assemblyIdentity).ToString (CultureInfo.InvariantCulture);
		}

		void LoadAarMap (string aarMapFile)
		{
			if (!File.Exists (aarMapFile))
				return;
			foreach (var line in File.ReadLines (aarMapFile)) {
				int separator = line.IndexOf ('=');
				if (separator > 0)
					aarMap [line.Substring (0, separator)] = line.Substring (separator + 1);
			}
		}

		/// <summary>
		/// Returns the name of the assembly or the path of the .aar for the provided `lp` directory.
		/// </summary>
		/// <param name="dir">The directory in the `lp` directory which we want to resolve.</param>
		public string GetAssemblyNameForImportDirectory (string dir)
		{
			if (aarMap.TryGetValue (dir, out var aarPath))
				return aarPath;
			if (!int.TryParse (dir, out int index))
				return string.Empty;
			if (index < 0 || index >= map.Count)
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
