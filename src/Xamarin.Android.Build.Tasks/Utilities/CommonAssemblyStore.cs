using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class CommonAssemblyStore : AssemblyStore
	{
		readonly List<AssemblyStoreAssemblyInfo> assemblies;

		public CommonAssemblyStore (string apkName, string archiveAssembliesPrefix, TaskLoggingHelper log, uint id, AssemblyStoreGlobalIndex globalIndexCounter)
			: base (apkName, archiveAssembliesPrefix, log, id, globalIndexCounter)
		{
			assemblies = new List <AssemblyStoreAssemblyInfo> ();
		}

		public override void Add (AssemblyStoreAssemblyInfo blobAssembly)
		{
			if (!String.IsNullOrEmpty (blobAssembly.Abi)) {
				throw new InvalidOperationException ($"Architecture-specific assembly cannot be added to an architecture-agnostic blob ({blobAssembly.FilesystemAssemblyPath})");
			}

			assemblies.Add (blobAssembly);
		}

		public override void Generate (string outputDirectory, List<AssemblyStoreIndexEntry> globalIndex, List<string> blobPaths)
		{
			// Always generate this blob, even if there are no assembly entries as this blob contains the global index
			Generate (Path.Combine (outputDirectory, $"{ApkName}_{BlobPrefix}{BlobExtension}"), assemblies, globalIndex, blobPaths);
		}
	}
}
