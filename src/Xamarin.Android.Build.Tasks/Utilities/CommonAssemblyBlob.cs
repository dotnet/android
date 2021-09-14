using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class CommonAssemblyBlob : AssemblyBlob
	{
		readonly List<BlobAssemblyInfo> assemblies;

		public CommonAssemblyBlob (string apkName, string archiveAssembliesPrefix, TaskLoggingHelper log)
			: base (apkName, archiveAssembliesPrefix, log)
		{
			assemblies = new List <BlobAssemblyInfo> ();
		}

		public override void Add (BlobAssemblyInfo blobAssembly)
		{
			if (!String.IsNullOrEmpty (blobAssembly.Abi)) {
				throw new InvalidOperationException ($"Architecture-specific assembly cannot be added to an architecture-agnostic blob ({blobAssembly.FilesystemAssemblyPath})");
			}

			Log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: adding Common assembly {blobAssembly.FilesystemAssemblyPath}");
			assemblies.Add (blobAssembly);
			AssemblyIndex.Add (new AssemblyBlobIndexEntry (GetAssemblyName (blobAssembly), ID));
		}

		public override void Generate (string outputDirectory, List<AssemblyBlobIndexEntry> globalIndex, List<string> blobPaths)
		{
			if (assemblies.Count == 0) {
				return;
			}

			Generate (Path.Combine (outputDirectory, $"{ApkName}_{BlobPrefix}{BlobExtension}"), assemblies, globalIndex, blobPaths);
		}
	}
}
