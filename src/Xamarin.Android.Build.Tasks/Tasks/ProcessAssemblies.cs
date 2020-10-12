using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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

		public string LinkMode { get; set; }

		public bool IncludeDebugSymbols { get; set; }

		public ITaskItem [] InputAssemblies { get; set; }

		[Output]
		public ITaskItem [] OutputAssemblies { get; set; }

		[Output]
		public ITaskItem [] ShrunkAssemblies { get; set; }

		[Output]
		public ITaskItem [] ResolvedSymbols { get; set; }

		public override bool RunTask ()
		{
			var output = new Dictionary<Guid, ITaskItem> ();
			var symbols = new Dictionary<string, ITaskItem> ();

			if (ResolvedSymbols != null) {
				foreach (var symbol in ResolvedSymbols) {
					symbols [symbol.ItemSpec] = symbol;
				}
			}

			foreach (var assembly in InputAssemblies) {
				if (!File.Exists (assembly.ItemSpec)) {
					Log.LogDebugMessage ($"Skipping non-existent dependency '{assembly.ItemSpec}'.");
					continue;
				}
				using (var pe = new PEReader (File.OpenRead (assembly.ItemSpec))) {
					var reader = pe.GetMetadataReader ();
					var module = reader.GetModuleDefinition ();
					var mvid = reader.GetGuid (module.Mvid);
					if (!output.ContainsKey (mvid)) {
						output.Add (mvid, assembly);

						// Set metadata, such as %(FrameworkAssembly) and %(HasMonoAndroidReference)
						string packageId = assembly.GetMetadata ("NuGetPackageId");
						bool frameworkAssembly = packageId.StartsWith ("Microsoft.NETCore.App.Runtime.") ||
							packageId.StartsWith ("Microsoft.Android.Runtime.");
						assembly.SetMetadata ("FrameworkAssembly", frameworkAssembly.ToString ());
						assembly.SetMetadata ("HasMonoAndroidReference", MonoAndroidHelper.HasMonoAndroidReference (reader).ToString ());
					} else {
						Log.LogDebugMessage ($"Removing duplicate: {assembly.ItemSpec}");

						var symbolPath = Path.ChangeExtension (assembly.ItemSpec, ".pdb");
						if (symbols.Remove (symbolPath)) {
							Log.LogDebugMessage ($"Removing duplicate: {symbolPath}");
						}
					}
				}
			}

			OutputAssemblies = output.Values.ToArray ();
			ResolvedSymbols = symbols.Values.ToArray ();

			// Set %(DestinationSubDirectory) and %(DestinationSubPath) for architecture-specific assemblies
			var fileNames = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
			foreach (var assembly in OutputAssemblies) {
				var fileName = Path.GetFileName (assembly.ItemSpec);
				symbols.TryGetValue (Path.ChangeExtension (assembly.ItemSpec, ".pdb"), out var symbol);
				if (fileNames.TryGetValue (fileName, out ITaskItem other)) {
					SetDestinationSubDirectory (assembly, fileName, symbol);
					if (other != null) {
						symbols.TryGetValue (Path.ChangeExtension (other.ItemSpec, ".pdb"), out symbol);
						SetDestinationSubDirectory (other, fileName, symbol);
						// We don't need to check "other" again
						fileNames [fileName] = null;
					}
				} else {
					fileNames.Add (fileName, assembly);
					assembly.SetDestinationSubPath ();
					symbol?.SetDestinationSubPath ();
				}
			}

			// Set ShrunkAssemblies for _RemoveRegisterAttribute and <BuildApk/>
			if (!string.IsNullOrEmpty (LinkMode) && !string.Equals (LinkMode, "None", StringComparison.OrdinalIgnoreCase) && !IncludeDebugSymbols) {
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

		/// <summary>
		/// Sets %(DestinationSubDirectory) and %(DestinationSubPath) based on %(RuntimeIdentifier)
		/// </summary>
		void SetDestinationSubDirectory (ITaskItem assembly, string fileName, ITaskItem symbol)
		{
			var rid = assembly.GetMetadata ("RuntimeIdentifier");
			var abi = MonoAndroidHelper.RuntimeIdentifierToAbi (rid);
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
