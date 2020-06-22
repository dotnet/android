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
	/// * %(AbiDirectory) if an assembly has an architecture-specific version
	/// </summary>
	public class ProcessAssemblies : AndroidTask
	{
		public override string TaskPrefix => "PRAS";

		public string LinkMode { get; set; }
		
		[Required]
		public string IntermediateAssemblyDirectory { get; set; }

		public ITaskItem [] InputAssemblies { get; set; }

		[Output]
		public ITaskItem [] OutputAssemblies { get; set; }

		[Output]
		public ITaskItem [] ShrunkAssemblies { get; set; }

		public override bool RunTask ()
		{
			var output = new Dictionary<Guid, ITaskItem> ();

			foreach (var assembly in InputAssemblies) {
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
					}
				}
			}

			OutputAssemblies = output.Values.ToArray ();

			// Set %(AbiDirectory) for architecture-specific assemblies
			var fileNames = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
			foreach (var assembly in OutputAssemblies) {
				var fileName = Path.GetFileName (assembly.ItemSpec);
				if (fileNames.TryGetValue (fileName, out ITaskItem other)) {
					SetAbiDirectory (assembly, fileName);
					SetAbiDirectory (other, fileName);
				} else {
					fileNames.Add (fileName, assembly);
					assembly.SetMetadata ("IntermediateLinkerOutput", Path.Combine (IntermediateAssemblyDirectory, fileName));
				}
			}

			// Set ShrunkAssemblies for _RemoveRegisterAttribute and <BuildApk/>
			if (!string.IsNullOrEmpty (LinkMode) && !string.Equals (LinkMode, "None", StringComparison.OrdinalIgnoreCase)) {
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
		/// Sets %(AbiDirectory) based on %(RuntimeIdentifier)
		/// </summary>
		void SetAbiDirectory (ITaskItem assembly, string fileName)
		{
			var rid = assembly.GetMetadata ("RuntimeIdentifier");
			var abi = MonoAndroidHelper.RuntimeIdentifierToAbi (rid);
			if (!string.IsNullOrEmpty (abi)) {
				assembly.SetMetadata ("AbiDirectory", abi);
				assembly.SetMetadata ("IntermediateLinkerOutput", Path.Combine (IntermediateAssemblyDirectory, abi, fileName));
			} else {
				Log.LogDebugMessage ($"Android ABI not found for: {assembly.ItemSpec}");
				assembly.SetMetadata ("IntermediateLinkerOutput", Path.Combine (IntermediateAssemblyDirectory, fileName));
			}
		}
	}
}
