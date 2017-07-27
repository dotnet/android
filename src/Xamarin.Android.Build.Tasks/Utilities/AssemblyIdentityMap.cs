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
			if (!File.Exists (mapFile))
				return;
			foreach (var s in File.ReadLines (mapFile)) {
				if (!map.Contains (s))
					map.Add (s);
			}
		}

		public string GetLibraryImportDirectoryNameForAssembly (string assemblyIdentity)
		{
			if (map.Contains (assemblyIdentity)) {
				return map.IndexOf (assemblyIdentity).ToString ();
			}
			map.Add (assemblyIdentity);
			return map.IndexOf (assemblyIdentity).ToString ();
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
