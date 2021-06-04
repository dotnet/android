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

		public bool PublishTrimmed { get; set; }

		public ITaskItem [] InputAssemblies { get; set; }

		[Output]
		public ITaskItem [] OutputAssemblies { get; set; }

		[Output]
		public ITaskItem [] ShrunkAssemblies { get; set; }

		[Output]
		public ITaskItem [] ResolvedSymbols { get; set; }

		public override bool RunTask ()
		{
			var output = new List<ITaskItem> ();
			var symbols = new Dictionary<string, ITaskItem> ();

			if (ResolvedSymbols != null) {
				foreach (var symbol in ResolvedSymbols.Where (Filter)) {
					symbols [symbol.ItemSpec] = symbol;
				}
			}

			// Group by assembly file name
			foreach (var group in InputAssemblies.Where (Filter).GroupBy (a => Path.GetFileName (a.ItemSpec))) {
				// Get the unique list of MVIDs
				var mvids = new HashSet<Guid> ();
				bool? frameworkAssembly = null, hasMonoAndroidReference = null;
				foreach (var assembly in group) {
					using (var pe = new PEReader (File.OpenRead (assembly.ItemSpec))) {
						var reader = pe.GetMetadataReader ();
						var module = reader.GetModuleDefinition ();
						var mvid = reader.GetGuid (module.Mvid);
						mvids.Add (mvid);

						// Calculate %(FrameworkAssembly) and %(HasMonoAndroidReference) for the first
						if (frameworkAssembly == null) {
							string packageId = assembly.GetMetadata ("NuGetPackageId") ?? "";
							frameworkAssembly = packageId.StartsWith ("Microsoft.NETCore.App.Runtime.") ||
								packageId.StartsWith ("Microsoft.Android.Runtime.");
						}
						if (hasMonoAndroidReference == null) {
							hasMonoAndroidReference = MonoAndroidHelper.HasMonoAndroidReference (reader);
						}
						assembly.SetMetadata ("FrameworkAssembly", frameworkAssembly.ToString ());
						assembly.SetMetadata ("HasMonoAndroidReference", hasMonoAndroidReference.ToString ());
					}
				}
				// If we end up with more than 1 unique mvid, we need *all* assemblies
				if (mvids.Count > 1) {
					foreach (var assembly in group) {
						var symbolPath = Path.ChangeExtension (assembly.ItemSpec, ".pdb");
						if (!symbols.TryGetValue (symbolPath, out var symbol)) {
							// Sometimes .pdb files are not included in @(ResolvedFileToPublish), so add them if they exist
							if (File.Exists (symbolPath)) {
								symbols [symbolPath] = symbol = new TaskItem (symbolPath);
							}
						}
						SetDestinationSubDirectory (assembly, group.Key, symbol);
						output.Add (assembly);
					}
				} else {
					// Otherwise only include the first assembly
					bool first = true;
					foreach (var assembly in group) {
						var symbolPath = Path.ChangeExtension (assembly.ItemSpec, ".pdb");
						if (first) {
							first = false;
							if (!symbols.TryGetValue (symbolPath, out var symbol)) {
								// Sometimes .pdb files are not included in @(ResolvedFileToPublish), so add them if they exist
								if (File.Exists (symbolPath)) {
									symbols [symbolPath] = symbol = new TaskItem (symbolPath);
								}
							}
							symbol?.SetDestinationSubPath ();
							assembly.SetDestinationSubPath ();
							output.Add (assembly);
						} else {
							symbols.Remove (symbolPath);
						}
					}
				}
			}

			OutputAssemblies = output.ToArray ();
			ResolvedSymbols = symbols.Values.ToArray ();

			// Set ShrunkAssemblies for _RemoveRegisterAttribute and <BuildApk/>
			if (PublishTrimmed) {
				ShrunkAssemblies = OutputAssemblies.Select (a => {
					var dir = Path.GetDirectoryName (a.ItemSpec);
					var file = Path.GetFileName (a.ItemSpec);
					return new TaskItem (a) {
						ItemSpec = Path.Combine (dir, "shrunk", file),
					};
				}).ToArray ();
			}

			return !Log.HasLoggedErrors;
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
		void SetDestinationSubDirectory (ITaskItem assembly, string fileName, ITaskItem symbol)
		{
			var rid = assembly.GetMetadata ("RuntimeIdentifier");
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
			} else {
				Log.LogDebugMessage ($"Android ABI not found for: {assembly.ItemSpec}");
				assembly.SetDestinationSubPath ();
				symbol?.SetDestinationSubPath ();
			}
		}
	}
}
