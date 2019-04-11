using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Filters a set of assemblies based on a given TargetFrameworkIdentifier
	/// </summary>
	public class FilterAssemblies : Task
	{
		[Required]
		public string TargetFrameworkIdentifier { get; set; }

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
					if (targetFrameworkIdentifier == TargetFrameworkIdentifier) {
						output.Add (assemblyItem);
					}
				}
			}
			OutputAssemblies = output.ToArray ();

			return !Log.HasLoggedErrors;
		}
	}
}
