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

		public void Add (string apkName, BlobAssemblyInfo blobAssembly)
		{
			log.LogMessage (MessageImportance.Low, $"Add: apkName == '{apkName}'");
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
				log.LogMessage (MessageImportance.Low, $"Checking if {b} is an index blob");
				if (!b.IsIndexBlob) {
					log.LogMessage (MessageImportance.Low, $"  it is not (ID: {b.ID})");
					return;
				}

				log.LogMessage (MessageImportance.Low, $"   it is");
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
			string indexBlobApkName = null;
			foreach (var kvp in blobs) {
				string apkName = kvp.Key;
				Blob blob = kvp.Value;

				if (!ret.ContainsKey (apkName)) {
					ret.Add (apkName, new List<string> ());
				}

				if (blob.Common == indexBlob || blob.Arch == indexBlob) {
					indexBlobApkName = apkName;
				}

				GenerateBlob (blob.Common, apkName);
				GenerateBlob (blob.Arch, apkName);
			}

			string manifestPath = indexBlob.WriteIndex (globalIndex);
			ret[indexBlobApkName].Add (manifestPath);

			return ret;

			void GenerateBlob (AssemblyBlob blob, string apkName)
			{
				blob.Generate (outputDirectory, globalIndex, ret[apkName]);
			}
		}
	}
}
