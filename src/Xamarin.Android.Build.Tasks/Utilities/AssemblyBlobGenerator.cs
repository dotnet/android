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

		// IDs must be counted per AssemblyBlobGenerator instance because it's possible that a single build will create more than one instance of the class and each time
		// the blobs must be assigned IDs starting from 0, or there will be errors due to "missing" index blob
		readonly Dictionary<string, uint> apkIds = new Dictionary<string, uint> (StringComparer.Ordinal);

		// Global assembly index must be restarted from 0 for the same reasons as apkIds above and at the same time it must be unique for each assembly added to **any**
		// blob, thus we need to keep the state here
		AssemblyBlobGlobalIndex globalIndexCounter = new AssemblyBlobGlobalIndex ();

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
					Common = new CommonAssemblyBlob (apkName, archiveAssembliesPrefix, log, GetNextBlobID (apkName), globalIndexCounter),
					Arch = new ArchAssemblyBlob (apkName, archiveAssembliesPrefix, log, GetNextBlobID (apkName), globalIndexCounter)
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

		uint GetNextBlobID (string apkName)
		{
			// NOTE: NOT thread safe, if we ever have parallel runs of BuildApk this operation must either be atomic or protected with a lock
			if (!apkIds.ContainsKey (apkName)) {
				apkIds.Add (apkName, 0);
			}
			return apkIds[apkName]++;
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
