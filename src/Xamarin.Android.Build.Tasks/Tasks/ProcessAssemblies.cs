#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Processes .dll files coming from @(ResolvedFileToPublish). Removes duplicate .NET assemblies by MVID.
	///
	/// Also sets some metadata:
	/// * %(FrameworkAssembly)=True to determine if framework or user assembly
	/// * %(HasMonoAndroidReference)=True for incremental build performance
	/// * Modify %(DestinationSubDirectory) and %(DestinationSubPath) if an assembly has an architecture-specific version
	/// </summary>
	public class ProcessAssemblies : AndroidTask
	{
		public override string TaskPrefix => "PRAS";

		[Required]
		public string [] RuntimeIdentifiers { get; set; } = Array.Empty<string>();

		public bool AndroidIncludeDebugSymbols { get; set; }

		public bool PublishTrimmed { get; set; }

		public ITaskItem [] InputAssemblies { get; set; } = Array.Empty<ITaskItem> ();

		public ITaskItem [] InputJavaLibraries { get; set; } = Array.Empty<ITaskItem> ();

		[Output]
		public ITaskItem []? OutputAssemblies { get; set; }

		[Output]
		public ITaskItem []? OutputJavaLibraries { get; set; }

		[Output]
		public ITaskItem []? ShrunkAssemblies { get; set; }

		[Output]
		public ITaskItem []? ResolvedSymbols { get; set; }

		public override bool RunTask ()
		{
			var output = new List<ITaskItem> ();
			var symbols = new Dictionary<string, ITaskItem> ();

			if (ResolvedSymbols != null) {
				foreach (var symbol in ResolvedSymbols.Where (Filter)) {
					symbols [symbol.ItemSpec] = symbol;
				}
			}

			// We only need to "dedup" assemblies when there is more than one RID
			// if (RuntimeIdentifiers.Length > 1) {
			// 	Log.LogDebugMessage ("Deduplicating assemblies per RuntimeIdentifier");
			// 	DeduplicateAssemblies (output, symbols);
			// } else {
				Log.LogDebugMessage ("Found a single RuntimeIdentifier");
				SetMetadataForAssemblies (output, symbols);
				//}

			OutputAssemblies = output.ToArray ();
			ResolvedSymbols = symbols.Values.ToArray ();

			// Set ShrunkAssemblies for _RemoveRegisterAttribute and <BuildApk/>
			// This should match the Condition on the _RemoveRegisterAttribute target
			if (PublishTrimmed && !AndroidIncludeDebugSymbols) {
				var shrunkAssemblies = new List<ITaskItem> (OutputAssemblies.Length);
				foreach (var assembly in OutputAssemblies) {
					var dir = Path.GetDirectoryName (assembly.ItemSpec);
					var file = Path.GetFileName (assembly.ItemSpec);
					shrunkAssemblies.Add (new TaskItem (assembly) {
						ItemSpec = Path.Combine (dir, "shrunk", file),
					});
				}
				ShrunkAssemblies = shrunkAssemblies.ToArray ();
			}

			if (InputJavaLibraries != null) {
				var javaLibraries = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
				foreach (var item in InputJavaLibraries) {
					if (!IsFromAKnownRuntimePack (item))
						continue;
					var name = Path.GetFileNameWithoutExtension(item.ItemSpec);
					if (!javaLibraries.ContainsKey (name)) {
						javaLibraries [name] = item;
					}
				}
				OutputJavaLibraries = javaLibraries.Values.ToArray ();
			}

			return !Log.HasLoggedErrors;
		}

		void SetAssemblyAbiMetadata (string abi, string assetType, ITaskItem assembly, ITaskItem? symbol, bool isDuplicate)
		{
			if (String.IsNullOrEmpty (abi) || (!isDuplicate && String.Compare ("native", assetType, StringComparison.OrdinalIgnoreCase) != 0)) {
				return;
			}

			assembly.SetMetadata ("Abi", abi);
			if (symbol != null) {
				symbol.SetMetadata ("Abi", abi);
			}
		}

		void SetAssemblyAbiMetadata (ITaskItem assembly, ITaskItem? symbol, bool isDuplicate)
		{
			string assetType = assembly.GetMetadata ("AssetType");
			string rid = assembly.GetMetadata ("RuntimeIdentifier");
			if (!String.IsNullOrEmpty (assembly.GetMetadata ("Culture")) || String.Compare ("resources", assetType, StringComparison.OrdinalIgnoreCase) == 0) {
				// Satellite assemblies are abi-agnostic, they shouldn't have the Abi metadata set
				return;
			}

			SetAssemblyAbiMetadata (AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid), assetType, assembly, symbol, isDuplicate);
		}

		void SetMetadataForAssemblies (List<ITaskItem> output, Dictionary<string, ITaskItem> symbols)
		{
			foreach (var assembly in InputAssemblies) {
				var symbol = GetOrCreateSymbolItem (symbols, assembly);
				SetAssemblyAbiMetadata (assembly, symbol, isDuplicate: false);
				symbol?.SetDestinationSubPath ();
				assembly.SetDestinationSubPath ();
				assembly.SetMetadata ("FrameworkAssembly", IsFromAKnownRuntimePack (assembly).ToString ());
				assembly.SetMetadata ("HasMonoAndroidReference", MonoAndroidHelper.HasMonoAndroidReference (assembly).ToString ());
				output.Add (assembly);
			}
		}

		void DeduplicateAssemblies (List<ITaskItem> output, Dictionary<string, ITaskItem> symbols)
		{
			// Group by assembly file name
			foreach (var group in InputAssemblies.Where (Filter).GroupBy (a => Path.GetFileName (a.ItemSpec))) {
				// Get the unique list of MVIDs
				var mvids = new HashSet<Guid> ();
				bool? frameworkAssembly = null, hasMonoAndroidReference = null;
				foreach (var assembly in group) {
					using var pe = new PEReader (File.OpenRead (assembly.ItemSpec));
					var reader = pe.GetMetadataReader ();
					var module = reader.GetModuleDefinition ();
					var mvid = reader.GetGuid (module.Mvid);
					mvids.Add (mvid);

					// Calculate %(FrameworkAssembly) and %(HasMonoAndroidReference) for the first
					if (frameworkAssembly == null) {
						frameworkAssembly = IsFromAKnownRuntimePack (assembly);
					}
					if (hasMonoAndroidReference == null) {
						hasMonoAndroidReference = MonoAndroidHelper.IsMonoAndroidAssembly (assembly) ||
							MonoAndroidHelper.HasMonoAndroidReference (reader);
					}
					assembly.SetMetadata ("FrameworkAssembly", frameworkAssembly.ToString ());
					assembly.SetMetadata ("HasMonoAndroidReference", hasMonoAndroidReference.ToString ());
				}
				// If we end up with more than 1 unique mvid, we need *all* assemblies
				if (mvids.Count > 1) {
					foreach (var assembly in group) {
						var symbol = GetOrCreateSymbolItem (symbols, assembly);
						SetDestinationSubDirectory (assembly, group.Key, symbol, isDuplicate: true);
						output.Add (assembly);
					}
				} else {
					// Otherwise only include the first assembly
					bool first = true;
					foreach (var assembly in group) {
						if (first) {
							first = false;

							var symbol = GetOrCreateSymbolItem (symbols, assembly);
							symbol?.SetDestinationSubPath ();
							assembly.SetDestinationSubPath ();
							output.Add (assembly);
							SetAssemblyAbiMetadata (assembly, symbol, false);
						} else {
							symbols.Remove (Path.ChangeExtension (assembly.ItemSpec, ".pdb"));
						}
					}
				}
			}
		}

		static bool IsFromAKnownRuntimePack (ITaskItem assembly)
		{
			string packageId = assembly.GetMetadata ("NuGetPackageId") ?? "";
			return packageId.StartsWith ("Microsoft.NETCore.App.Runtime.", StringComparison.Ordinal) ||
				packageId.StartsWith ("Microsoft.Android.Runtime.", StringComparison.Ordinal);
		}

		static ITaskItem? GetOrCreateSymbolItem (Dictionary<string, ITaskItem> symbols, ITaskItem assembly)
		{
			var symbolPath = Path.ChangeExtension (assembly.ItemSpec, ".pdb");
			if (!symbols.TryGetValue (symbolPath, out var symbol)) {
				// Sometimes .pdb files are not included in @(ResolvedFileToPublish), so add them if they exist
				if (File.Exists (symbolPath)) {
					symbols [symbolPath] = symbol = new TaskItem (symbolPath);
				}
			}
			return symbol;
		}

		bool Filter (ITaskItem item)
		{
			if (!File.Exists (item.ItemSpec)) {
				Log.LogDebugMessage ($"Skipping non-existent file '{item.ItemSpec}'.");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Sets %(DestinationSubDirectory) and %(DestinationSubPath) based on %(RuntimeIdentifier)
		/// </summary>
		void SetDestinationSubDirectory (ITaskItem assembly, string fileName, ITaskItem? symbol, bool isDuplicate)
		{
			var rid = assembly.GetMetadata ("RuntimeIdentifier");
			string assetType = assembly.GetMetadata ("AssetType");

			// Satellite assemblies have `RuntimeIdentifier` set, but they shouldn't - they aren't specific to any architecture, so they should have none of the
			// abi-specific metadata set
			//
			if (!String.IsNullOrEmpty (assembly.GetMetadata ("Culture")) ||
			    String.Compare ("resources", assetType, StringComparison.OrdinalIgnoreCase) == 0) {
				rid = String.Empty;
			}

			var abi = AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid);
			if (!string.IsNullOrEmpty (abi)) {
				string destination = Path.Combine (assembly.GetMetadata ("DestinationSubDirectory"), abi);
				assembly.SetMetadata ("DestinationSubDirectory", destination + Path.DirectorySeparatorChar);
				assembly.SetMetadata ("DestinationSubPath", Path.Combine (destination, fileName));
				if (symbol != null) {
					destination = Path.Combine (symbol.GetMetadata ("DestinationSubDirectory"), abi);
					symbol.SetMetadata ("DestinationSubDirectory", destination + Path.DirectorySeparatorChar);
					symbol.SetMetadata ("DestinationSubPath", Path.Combine (destination, Path.GetFileName (symbol.ItemSpec)));
				}

				SetAssemblyAbiMetadata (abi, assetType, assembly, symbol, isDuplicate);
			} else {
				Log.LogDebugMessage ($"Android ABI not found for: {assembly.ItemSpec}");
				assembly.SetDestinationSubPath ();
				symbol?.SetDestinationSubPath ();
			}
		}
	}
}
