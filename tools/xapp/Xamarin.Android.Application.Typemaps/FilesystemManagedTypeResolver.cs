using System.Collections.Generic;
using System.IO;

using Mono.Cecil;

namespace tmt
{
	class FilesystemManagedTypeResolver : ManagedTypeResolver
	{
		List<string> searchPaths;
		bool havePaths;

		public FilesystemManagedTypeResolver (List<string> searchPaths)
		{
			this.searchPaths = searchPaths;
			havePaths = searchPaths.Count > 0;
		}

		protected override string? FindAssembly (string assemblyName)
		{
			if (!havePaths) {
				return null;
			}

			string? assemblyPath = null;
			string assemblyFileName = $"{assemblyName}.dll";

			foreach (string dir in searchPaths) {
				string path = Path.Combine (dir, assemblyFileName);
				if (File.Exists (path)) {
					assemblyPath = path;
					break;
				}
			}

			return assemblyPath;
		}

		protected override AssemblyDefinition ReadAssembly (string assemblyPath)
		{
			return AssemblyDefinition.ReadAssembly (assemblyPath);
		}
	}
}
