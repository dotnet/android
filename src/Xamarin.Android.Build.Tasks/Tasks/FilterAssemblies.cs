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
	/// Filters a set of assemblies to be known as "Xamarin.Android" assemblies through various checks:
	/// * The presence of [assembly: System.Runtime.Versioning.TargetFramework("MonoAndroid,Version=v9.0")]
	/// * A Mono.Android.dll reference
	/// * An EmbeddedResource ending with *.jar
	/// * An EmbeddedResource beginning with __Android
	/// </summary>
	public class FilterAssemblies : AndroidTask
	{
		public override string TaskPrefix => "FLT";

		const string TargetFrameworkIdentifier = "MonoAndroid";
		const string MonoAndroidReference = "Mono.Android";

		public bool DesignTimeBuild { get; set; }

		public ITaskItem [] InputAssemblies { get; set; }

		[Output]
		public ITaskItem [] OutputAssemblies { get; set; }

		public override bool RunTask ()
		{
			if (InputAssemblies == null)
				return true;

			var output = new List<ITaskItem> (InputAssemblies.Length);
			foreach (var assemblyItem in InputAssemblies) {
				if (!File.Exists (assemblyItem.ItemSpec)) {
					Log.LogDebugMessage ($"Skipping non-existent dependency '{assemblyItem.ItemSpec}'.");
					continue;
				}
				using (var pe = new PEReader (File.OpenRead (assemblyItem.ItemSpec))) {
					var reader = pe.GetMetadataReader ();
					var assemblyDefinition = reader.GetAssemblyDefinition ();
					var targetFrameworkIdentifier = GetTargetFrameworkIdentifier (assemblyDefinition, reader);
					if (string.Compare (targetFrameworkIdentifier, TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) == 0) {
						output.Add (assemblyItem);
						continue;
					}

					// In the rare case, [assembly: TargetFramework("MonoAndroid,Version=v9.0")] may not match
					Log.LogDebugMessage ($"{nameof (TargetFrameworkIdentifier)} did not match: {assemblyItem.ItemSpec}");

					// Fallback to looking for a Mono.Android reference
					if (HasReference (reader)) {
						Log.LogDebugMessage ($"{MonoAndroidReference} reference found: {assemblyItem.ItemSpec}");
						output.Add (assemblyItem);
						continue;
					}
					// Fallback to looking for *.jar or __Android EmbeddedResource files
					if (HasEmbeddedResource (reader)) {
						Log.LogDebugMessage ($"EmbeddedResource found: {assemblyItem.ItemSpec}");
						output.Add (assemblyItem);
						continue;
					}
				}
			}
			OutputAssemblies = output.ToArray ();

			return !Log.HasLoggedErrors;
		}

		string GetTargetFrameworkIdentifier (AssemblyDefinition assembly, MetadataReader reader)
		{
			string targetFrameworkIdentifier = null;
			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				var name = reader.GetCustomAttributeFullName (attribute);
				switch (name) {
					case "System.Runtime.Versioning.TargetFrameworkAttribute":
						var arguments = attribute.GetCustomAttributeArguments ();
						foreach (var p in arguments.FixedArguments) {
							// Of the form "MonoAndroid,Version=v8.1"
							var value = p.Value?.ToString ();
							if (!string.IsNullOrEmpty (value)) {
								int commaIndex = value.IndexOf (",", StringComparison.Ordinal);
								if (commaIndex != -1) {
									targetFrameworkIdentifier = value.Substring (0, commaIndex);
									break;
								}
							}
						}
						break;
					case "Android.IncludeAndroidResourcesFromAttribute":
					case "Android.NativeLibraryReferenceAttribute":
					case "Java.Interop.JavaLibraryReferenceAttribute":
						Log.LogCodedError ("XA0121",
							$"Assembly '{reader.GetString (assembly.Name)}' is using '[assembly: {name}]', which is no longer supported. Use a newer version of this NuGet package or notify the library author.");
						break;
					default:
						break;
				}
			}
			return targetFrameworkIdentifier;
		}

		bool HasReference (MetadataReader reader)
		{
			foreach (var handle in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (handle);
				var name = reader.GetString (reference.Name);
				if (MonoAndroidReference == name) {
					return true;
				}
			}
			return false;
		}

		bool HasEmbeddedResource (MetadataReader reader)
		{
			foreach (var handle in reader.ManifestResources) {
				var resource = reader.GetManifestResource (handle);
				var name = reader.GetString (resource.Name);
				if (name.EndsWith (".jar", StringComparison.OrdinalIgnoreCase) ||
						name.StartsWith ("__Android", StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}
	}
}
