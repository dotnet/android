using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class AssemblyBlobGenerator
	{
		sealed class Blob
		{
			public AssemblyBlob Common;
			public AssemblyBlob Arch;
		}

		readonly string archiveAssembliesPrefix;
		readonly TaskLoggingHelper log;

		// NOTE: when/if we have parallel BuildApk this should become a ConcurrentDictionary
		readonly Dictionary<string, Blob> blobs;

		AssemblyBlob indexBlob;

		public AssemblyBlobGenerator (string archiveAssembliesPrefix, TaskLoggingHelper log)
		{
			if (String.IsNullOrEmpty (archiveAssembliesPrefix)) {
				throw new ArgumentException ("must not be null or empty", nameof (archiveAssembliesPrefix));
			}

			this.archiveAssembliesPrefix = archiveAssembliesPrefix;
			this.log = log;

			blobs = new Dictionary<string, Blob> (StringComparer.Ordinal);
		}

		public void Add (BlobAssemblyInfo blobAssembly)
		{
			Add ("base", blobAssembly);
		}

		public void Add (string apkName, BlobAssemblyInfo blobAssembly)
		{
			if (String.IsNullOrEmpty (apkName)) {
				throw new ArgumentException ("must not be null or empty", nameof (apkName));
			}

			Blob blob;
			if (!blobs.ContainsKey (apkName)) {
				blob = new Blob {
					Common = new CommonAssemblyBlob (apkName, archiveAssembliesPrefix, log),
					Arch = new ArchAssemblyBlob (apkName, archiveAssembliesPrefix, log)
				};

				blobs.Add (apkName, blob);
				SetIndexBlob (blob.Common);
				SetIndexBlob (blob.Arch);
			}

			blob = blobs[apkName];
			if (String.IsNullOrEmpty (blobAssembly.Abi)) {
				blob.Common.Add (blobAssembly);
			} else {
				blob.Arch.Add (blobAssembly);
			}

			void SetIndexBlob (AssemblyBlob b)
			{
				if (!b.IsIndexBlob) {
					return;
				}

				if (indexBlob != null) {
					throw new InvalidOperationException ("Index blob already set!");
				}

				indexBlob = b;
			}
		}

		public Dictionary<string, List<string>> Generate (string outputDirectory)
		{
			if (blobs.Count == 0) {
				return null;
			}

			if (indexBlob == null) {
				throw new InvalidOperationException ("Index blob not found");
			}

			var globalIndex = new List<AssemblyBlobIndexEntry> ();
			var ret = new Dictionary<string, List<string>> (StringComparer.Ordinal);
			foreach (var kvp in blobs) {
				string apkName = kvp.Key;
				Blob blob = kvp.Value;

				if (!ret.ContainsKey (apkName)) {
					ret.Add (apkName, new List<string> ());
				}

				List<string> blobPaths = ret[apkName];
				GenerateBlob (blob.Common, blobPaths);
				GenerateBlob (blob.Arch, blobPaths);
			}

			indexBlob.WriteIndex (globalIndex);
			return ret;

			void GenerateBlob (AssemblyBlob blob, List<string> blobPaths)
			{
				blob.Generate (outputDirectory, globalIndex, blobPaths);
			}
		}
	}
}
