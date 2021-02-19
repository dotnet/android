using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task contains *temporary* workarounds for NuGet in .NET 5.
	/// </summary>
	public class FixupNuGetReferences : AndroidTask
	{
		public override string TaskPrefix => "FNR";

		[Required]
		public string [] PackageTargetFallback { get; set; }

		public ITaskItem [] CopyLocalItems { get; set; }

		[Output]
		public string [] AssembliesToAdd { get; set; }

		[Output]
		public ITaskItem [] AssembliesToRemove { get; set; }

		public override bool RunTask ()
		{
			if (CopyLocalItems == null || CopyLocalItems.Length == 0)
				return true;

			var assembliesToAdd     = new Dictionary<string, string> ();
			var assembliesToRemove  = new List<ITaskItem> ();
			var fallbackDirectories = new HashSet<string> ();

			foreach (var item in CopyLocalItems) {
				var directory = Path.GetDirectoryName (item.ItemSpec);
				var directoryName = Path.GetFileName (directory);
				Log.LogDebugMessage ($"{directoryName} -> {item.ItemSpec}");
				if (directoryName == "netstandard2.0") {
					var parent = Directory.GetParent (directory);
					foreach (var nugetDirectory in parent.EnumerateDirectories ()) {
						var name = Path.GetFileName (nugetDirectory.Name);
						foreach (var fallback in PackageTargetFallback) {
							if (!string.Equals (name, fallback, StringComparison.OrdinalIgnoreCase))
								continue;
							var fallbackDirectory = Path.Combine (parent.FullName, name);
							fallbackDirectories.Add (fallbackDirectory);

							// Remove the netstandard assembly, if there is a platform-specific one
							var path = Path.Combine (fallbackDirectory, Path.GetFileName (item.ItemSpec));
							if (File.Exists (path)) {
								Log.LogDebugMessage ($"Removing: {item.ItemSpec}");
								assembliesToRemove.Add (item);
							}
						}
					}
				}
			}

			// Look for any platform-specific assemblies
			foreach (var directory in fallbackDirectories) {
				foreach (var assembly in Directory.GetFiles (directory, "*.dll")) {
					var assemblyName = Path.GetFileName (assembly);
					if (!assembliesToAdd.ContainsKey (assemblyName)) {
						Log.LogDebugMessage ($"Adding: {assembly}");
						assembliesToAdd.Add (assemblyName, assembly);
					}
				}
			}

			AssembliesToAdd = assembliesToAdd.Values.ToArray ();
			AssembliesToRemove = assembliesToRemove.ToArray ();

			return !Log.HasLoggedErrors;
		}
	}
}
