using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.AssemblyBlobReader;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	class ArchiveAssemblyHelper
	{
		readonly string archivePath;
		readonly string assembliesRootDir;
		bool useAssemblyBlobs;

		public ArchiveAssemblyHelper (string archivePath, bool useAssemblyBlobs)
		{
			if (String.IsNullOrEmpty (archivePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (archivePath));
			}

			this.archivePath = archivePath;
			this.useAssemblyBlobs = useAssemblyBlobs;

			string extension = Path.GetExtension (archivePath) ?? String.Empty;
			if (String.Compare (".aab", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "assemblies/";
			} else if (String.Compare (".apk", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "base/root/assemblies";
			} else {
				throw new InvalidOperationException ($"Unrecognized archive extension '{extension}'");
			}
		}

		public (IEnumerable<string> existingFiles, IEnumerable<string> missingFiles, IEnumerable<string> additionalFiles) Contains (string[] assemblyNames)
		{
			if (assemblyNames == null) {
				throw new ArgumentNullException (nameof (assemblyNames));
			}

			if (assemblyNames.Length == 0) {
				throw new ArgumentException ("must not be empty", nameof (assemblyNames));
			}

			if (useAssemblyBlobs) {
				return BlobContains (assemblyNames);
			}

			return ArchiveContains (assemblyNames);
		}

		(IEnumerable<string> existingFiles, IEnumerable<string> missingFiles, IEnumerable<string> additionalFiles) ArchiveContains (string[] assemblyNames)
		{
			using (var zip = ZipHelper.OpenZip (archivePath)) {
				IEnumerable<ZipEntry> existingFiles = zip.Where (a => a.FullName.StartsWith (assembliesRootDir, StringComparison.InvariantCultureIgnoreCase));
				IEnumerable<string> missingFiles = assemblyNames.Where (x => !zip.ContainsEntry (assembliesRootDir + Path.GetFileName (x)));
				IEnumerable<string> additionalFiles = existingFiles.Where (x => !assemblyNames.Contains (Path.GetFileName (x.FullName))).Select (x => x.FullName);

				return (existingFiles.Select (x => x.FullName), missingFiles, additionalFiles);
			}
		}

		(IEnumerable<string> existingFiles, IEnumerable<string> missingFiles, IEnumerable<string> additionalFiles) BlobContains (string[] assemblyNames)
		{
			// TODO: support files not related to assemblies (e.g. rc.bin)
			// TODO: support .config and .pdb/.mdb files
			// Blobs don't store the assembly extension
			IEnumerable<string> expectedNames = assemblyNames.Select (x => Path.GetFileNameWithoutExtension (x));

			var explorer = new BlobExplorer (archivePath);
			if (explorer.AssembliesByName.Count == 0) {
				return (new string[0], new string[0], new string[0]);
			}

			IEnumerable<string> existingFiles = explorer.AssembliesByName.Keys;
			IEnumerable<string> missingFiles = expectedNames.Where (x => !explorer.AssembliesByName.ContainsKey (x));
			IEnumerable<string> additionalFiles = existingFiles.Where (x => !expectedNames.Contains (x));

			return (existingFiles, missingFiles, additionalFiles);
		}
	}
}
