using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A replacement for DirectoryAssemblyResolver, using System.Reflection.Metadata
	/// </summary>
	public class MetadataResolver : IDisposable
	{
		readonly Dictionary<string, PEReader> cache = new Dictionary<string, PEReader> ();
		readonly List<string> searchDirectories = new List<string> ();

		public MetadataReader GetAssemblyReader (string assemblyName)
		{
			var key = Path.GetFileNameWithoutExtension (assemblyName);
			if (!cache.TryGetValue (key, out PEReader reader)) {
				var assemblyPath = Resolve (assemblyName);
				cache.Add (key, reader = new PEReader (File.OpenRead (assemblyPath)));
			}
			return reader.GetMetadataReader ();
		}

		public void AddSearchDirectory (string directory)
		{
			directory = Path.GetFullPath (directory);
			if (!searchDirectories.Contains (directory))
				searchDirectories.Add (directory);
		}

		public string Resolve (string assemblyName)
		{
			string assemblyPath = assemblyName;
			if (!assemblyPath.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
				assemblyPath += ".dll";
			}
			if (File.Exists (assemblyPath)) {
				return assemblyPath;
			}
			foreach (var dir in searchDirectories) {
				var path = Path.Combine (dir, assemblyPath);
				if (File.Exists (path))
					return path;
			}

			throw new FileNotFoundException ($"Could not load assembly '{assemblyName}'.", assemblyName);
		}

		public void Dispose ()
		{
			foreach (var provider in cache.Values) {
				provider.Dispose ();
			}
			cache.Clear ();
		}
	}
}
