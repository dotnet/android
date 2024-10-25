using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Filters a set of assemblies to be known as ".NET for Android" assemblies through various checks:
	/// * The presence of [assembly: System.Runtime.Versioning.TargetFramework("MonoAndroid,Version=v9.0")]
	/// * A Mono.Android.dll reference
	/// * An EmbeddedResource ending with *.jar
	/// * An EmbeddedResource beginning with __Android
	/// </summary>
	public class FilterAssemblies : AndroidTask
	{
		public override string TaskPrefix => "FLT";

		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.AppDomain;

		public ITaskItem [] InputAssemblies { get; set; }

		[Output]
		public ITaskItem [] OutputAssemblies { get; set; }

		public override bool RunTask ()
		{
			if (InputAssemblies == null)
				return true;

			var output = new List<ITaskItem> (InputAssemblies.Length);
			foreach (var assemblyItem in InputAssemblies) {
				// Skip .NET 6.0 <FrameworkReference/> assemblies
				var frameworkReferenceName = assemblyItem.GetMetadata ("FrameworkReferenceName") ?? "";
				if (frameworkReferenceName == "Microsoft.Android") {
					continue; // No need to process Mono.Android.dll or Java.Interop.dll
				}
				if (frameworkReferenceName.StartsWith ("Microsoft.NETCore.", StringComparison.OrdinalIgnoreCase)) {
					continue; // No need to process BCL assemblies
				}
				if (string.Equals (assemblyItem.GetMetadata ("TargetPlatformIdentifier"), "android", StringComparison.OrdinalIgnoreCase)) {
					output.Add (assemblyItem);
					continue;
				}
				try {
					ProcessAssembly (assemblyItem, output);
				} catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) {
					Log.LogDebugMessage ($"Skipping non-existent dependency '{assemblyItem.ItemSpec}'.");
				}

			}
			OutputAssemblies = output.ToArray ();

			return !Log.HasLoggedErrors;
		}

		void ProcessAssembly(ITaskItem assemblyItem, List<ITaskItem> output)
		{
			using var pe = new PEReader (File.OpenRead (assemblyItem.ItemSpec));
			var reader = pe.GetMetadataReader ();
			// Check in-memory cache
			var module = reader.GetModuleDefinition ();
			var key = (nameof (FilterAssemblies), reader.GetGuid (module.Mvid));
			var value = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal (key, Lifetime);
			if (value is bool isMonoAndroidAssembly) {
				if (isMonoAndroidAssembly) {
					Log.LogDebugMessage ($"Cached: {assemblyItem.ItemSpec}");
					output.Add (assemblyItem);
				}
				return;
			}
			// Check assembly definition
			var assemblyDefinition = reader.GetAssemblyDefinition ();
			if (IsAndroidAssembly (assemblyDefinition, reader)) {
				output.Add (assemblyItem);
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, value: true, Lifetime);
				return;
			}
			// Fallback to looking for a Mono.Android reference
			if (MonoAndroidHelper.HasMonoAndroidReference (reader)) {
				Log.LogDebugMessage ($"Mono.Android reference found: {assemblyItem.ItemSpec}");
				output.Add (assemblyItem);
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, value: true, Lifetime);
				return;
			}
			// Fallback to looking for *.jar or __Android EmbeddedResource files
			if (HasEmbeddedResource (reader)) {
				Log.LogDebugMessage ($"EmbeddedResource found: {assemblyItem.ItemSpec}");
				output.Add (assemblyItem);
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, value: true, Lifetime);
				return;
			}
			// Not a MonoAndroid assembly, store false
			BuildEngine4.RegisterTaskObjectAssemblyLocal (key, value: false, Lifetime);
		}

		bool IsAndroidAssembly (AssemblyDefinition assembly, MetadataReader reader)
		{
			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				var name = reader.GetCustomAttributeFullName (attribute, Log);
				switch (name) {
					case "System.Runtime.Versioning.TargetFrameworkAttribute":
						string targetFrameworkIdentifier = null;
						foreach (var p in attribute.GetCustomAttributeArguments ().FixedArguments) {
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
						if (string.Equals (targetFrameworkIdentifier, "MonoAndroid", StringComparison.OrdinalIgnoreCase)) {
							return true;
						}
						break;
					case "System.Runtime.Versioning.TargetPlatformAttribute":
						foreach (var p in attribute.GetCustomAttributeArguments ().FixedArguments) {
							// Of the form "android30"
							var value = p.Value?.ToString ();
							if (value != null && value.StartsWith ("android", StringComparison.OrdinalIgnoreCase)) {
								return true;
							}
						}
						break;
					case "Android.IncludeAndroidResourcesFromAttribute":
					case "Android.NativeLibraryReferenceAttribute":
					case "Java.Interop.JavaLibraryReferenceAttribute":
						Log.LogCodedError ("XA0121", Properties.Resources.XA0121, reader.GetString (assembly.Name), name);
						break;
					default:
						break;
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
