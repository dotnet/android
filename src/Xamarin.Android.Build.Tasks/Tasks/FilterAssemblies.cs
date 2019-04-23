using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Filters a set of assemblies based on a given TargetFrameworkIdentifier or FallbackReference
	/// </summary>
	public class FilterAssemblies : Task
	{
		/// <summary>
		/// The MonoAndroid portion of [assembly: System.Runtime.Versioning.TargetFramework("MonoAndroid,v9.0")]
		/// </summary>
		[Required]
		public string TargetFrameworkIdentifier { get; set; }

		/// <summary>
		/// If TargetFrameworkIdentifier is missing, we can look for Mono.Android.dll references instead
		/// </summary>
		public string FallbackReference { get; set; }

		[Required]
		public bool DesignTimeBuild { get; set; }

		public ITaskItem [] InputAssemblies { get; set; }

		[Output]
		public ITaskItem [] OutputAssemblies { get; set; }

		public override bool Execute ()
		{
			if (InputAssemblies == null)
				return true;

			var output = new List<ITaskItem> (InputAssemblies.Length);
			foreach (var assemblyItem in InputAssemblies) {
				if (DesignTimeBuild && !File.Exists (assemblyItem.ItemSpec)) {
					Log.LogDebugMessage ($"Skipping non-existent dependency '{assemblyItem.ItemSpec}' during a design-time build.");
					continue;
				}
				using (var pe = new PEReader (File.OpenRead (assemblyItem.ItemSpec))) {
					var reader = pe.GetMetadataReader ();
					var assemblyDefinition = reader.GetAssemblyDefinition ();
					var targetFrameworkIdentifier = assemblyDefinition.GetTargetFrameworkIdentifier (reader);
					if (string.Compare (targetFrameworkIdentifier, TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) == 0) {
						output.Add (assemblyItem);
						continue;
					}
					// Fallback to looking at references
					if (string.IsNullOrEmpty (targetFrameworkIdentifier) && !string.IsNullOrEmpty (FallbackReference)) {
						Log.LogDebugMessage ($"Checking references for: {assemblyItem.ItemSpec}");
						foreach (var handle in reader.AssemblyReferences) {
							var reference = reader.GetAssemblyReference (handle);
							var name = reader.GetString (reference.Name);
							if (FallbackReference == name) {
								output.Add (assemblyItem);
								break;
							}
						}
					}
				}
			}
			OutputAssemblies = output.ToArray ();

			return !Log.HasLoggedErrors;
		}
	}
}
