using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class AssemblyStoreGenerator
	{
		sealed class Store
		{
			public AssemblyStore Common;
			public AssemblyStore Arch;
		}

		readonly string archiveAssembliesPrefix;
		readonly TaskLoggingHelper log;

		// NOTE: when/if we have parallel BuildApk this should become a ConcurrentDictionary
		readonly Dictionary<string, Store> stores;

		AssemblyStore indexStore;

		// IDs must be counted per AssemblyStoreGenerator instance because it's possible that a single build will create more than one instance of the class and each time
		// the stores must be assigned IDs starting from 0, or there will be errors due to "missing" index store
		readonly Dictionary<string, uint> apkIds = new Dictionary<string, uint> (StringComparer.Ordinal);

		// Global assembly index must be restarted from 0 for the same reasons as apkIds above and at the same time it must be unique for each assembly added to **any**
		// assembly store, thus we need to keep the state here
		AssemblyStoreGlobalIndex globalIndexCounter = new AssemblyStoreGlobalIndex ();

		public AssemblyStoreGenerator (string archiveAssembliesPrefix, TaskLoggingHelper log)
		{
			if (String.IsNullOrEmpty (archiveAssembliesPrefix)) {
				throw new ArgumentException ("must not be null or empty", nameof (archiveAssembliesPrefix));
			}

			this.archiveAssembliesPrefix = archiveAssembliesPrefix;
			this.log = log;

			stores = new Dictionary<string, Store> (StringComparer.Ordinal);
		}

		public void Add (string apkName, AssemblyStoreAssemblyInfo storeAssembly)
		{
			log.LogMessage (MessageImportance.Low, $"Add: apkName == '{apkName}'");
			if (String.IsNullOrEmpty (apkName)) {
				throw new ArgumentException ("must not be null or empty", nameof (apkName));
			}

			Store store;
			if (!stores.ContainsKey (apkName)) {
				store = new Store {
					Common = new CommonAssemblyStore (apkName, archiveAssembliesPrefix, log, GetNextStoreID (apkName), globalIndexCounter),
					Arch = new ArchAssemblyStore (apkName, archiveAssembliesPrefix, log, GetNextStoreID (apkName), globalIndexCounter)
				};

				stores.Add (apkName, store);
				SetIndexStore (store.Common);
				SetIndexStore (store.Arch);
			}

			store = stores[apkName];
			if (String.IsNullOrEmpty (storeAssembly.Abi)) {
				store.Common.Add (storeAssembly);
			} else {
				store.Arch.Add (storeAssembly);
			}

			void SetIndexStore (AssemblyStore b)
			{
				log.LogMessage (MessageImportance.Low, $"Checking if {b} is an index store");
				if (!b.IsIndexStore) {
					log.LogMessage (MessageImportance.Low, $"  it is not (ID: {b.ID})");
					return;
				}

				log.LogMessage (MessageImportance.Low, $"   it is");
				if (indexStore != null) {
					throw new InvalidOperationException ("Index store already set!");
				}

				indexStore = b;
			}
		}

		uint GetNextStoreID (string apkName)
		{
			// NOTE: NOT thread safe, if we ever have parallel runs of BuildApk this operation must either be atomic or protected with a lock
			if (!apkIds.ContainsKey (apkName)) {
				apkIds.Add (apkName, 0);
			}
			return apkIds[apkName]++;
		}

		public Dictionary<string, List<string>> Generate (string outputDirectory)
		{
			if (stores.Count == 0) {
				return null;
			}

			if (indexStore == null) {
				throw new InvalidOperationException ("Index store not found");
			}

			var globalIndex = new List<AssemblyStoreIndexEntry> ();
			var ret = new Dictionary<string, List<string>> (StringComparer.Ordinal);
			string indexStoreApkName = null;
			foreach (var kvp in stores) {
				string apkName = kvp.Key;
				Store store = kvp.Value;

				if (!ret.ContainsKey (apkName)) {
					ret.Add (apkName, new List<string> ());
				}

				if (store.Common == indexStore || store.Arch == indexStore) {
					indexStoreApkName = apkName;
				}

				GenerateStore (store.Common, apkName);
				GenerateStore (store.Arch, apkName);
			}

			string manifestPath = indexStore.WriteIndex (globalIndex);
			ret[indexStoreApkName].Add (manifestPath);

			return ret;

			void GenerateStore (AssemblyStore store, string apkName)
			{
				store.Generate (outputDirectory, globalIndex, ret[apkName]);
			}
		}
	}
}
