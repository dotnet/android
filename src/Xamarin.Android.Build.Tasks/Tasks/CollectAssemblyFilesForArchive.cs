#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// This task figures out the compression/assembly store/wrapping operations that need to
/// be performed on the assemblies before they are added to the APK. This is done "ahead of time"
/// so that the actual work can be done in an incremental way.
/// </summary>
public class CollectAssemblyFilesToCompress : AndroidTask
{
	public override string TaskPrefix => "CAF";

	[Required]
	public string AssemblyCompressionDirectory { get; set; } = "";

	public bool EmbedAssemblies { get; set; }

	[Required]
	public bool EnableCompression { get; set; }

	public bool IncludeDebugSymbols { get; set; }

	[Required]
	public string ProjectFullPath { get; set; } = "";

	[Required]
	public ITaskItem [] ResolvedFrameworkAssemblies { get; set; } = [];

	[Required]
	public ITaskItem [] ResolvedUserAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	[Output]
	public ITaskItem [] AssembliesToCompressOutput { get; set; } = [];

	[Output]
	public ITaskItem [] ResolvedFrameworkAssembliesOutput { get; set; } = [];

	[Output]
	public ITaskItem [] ResolvedUserAssembliesOutput { get; set; } = [];

	public override bool RunTask ()
	{
		ResolvedFrameworkAssembliesOutput = ResolvedFrameworkAssemblies;
		ResolvedUserAssembliesOutput = ResolvedUserAssemblies;

		// We aren't going to compress any assemblies
		if (IncludeDebugSymbols || !EnableCompression || !EmbedAssemblies)
			return true;

		var assemblies_to_compress = new List<ITaskItem> ();
		var compressed_assemblies_info = GetCompressedAssemblyInfo ();

		// Get all the user and framework assemblies we may need to compresss
		var assemblies = ResolvedFrameworkAssemblies.Concat (ResolvedUserAssemblies).Where (asm => !(ShouldSkipAssembly (asm))).ToArray ();
		var per_arch_assemblies = MonoAndroidHelper.GetPerArchAssemblies (assemblies, SupportedAbis, true);

		foreach (var kvp in per_arch_assemblies) {
			Log.LogDebugMessage ($"Preparing assemblies for architecture '{kvp.Key}'");

			foreach (var asm in kvp.Value.Values) {

				if (bool.TryParse (asm.GetMetadata ("AndroidSkipCompression"), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping compression of {asm.ItemSpec} due to 'AndroidSkipCompression' == 'true' ");
					continue;
				}

				if (!AssemblyCompression.TryGetDescriptorIndex (Log, asm, compressed_assemblies_info, out var descriptor_index)) {
					Log.LogDebugMessage ($"Skipping compression of {asm.ItemSpec} due to missing descriptor index.");
					continue;
				}

				var compressed_assembly = AssemblyCompression.GetCompressedAssemblyOutputPath (asm, AssemblyCompressionDirectory);

				assemblies_to_compress.Add (CreateAssemblyToCompress (asm.ItemSpec, compressed_assembly, descriptor_index));

				// Mark this assembly as "compressed", if the compression process fails we will remove this metadata later
				asm.SetMetadata ("CompressedAssembly", compressed_assembly);
			}
		}

		AssembliesToCompressOutput = assemblies_to_compress.ToArray ();

		return !Log.HasLoggedErrors;
	}

	TaskItem CreateAssemblyToCompress (string sourceAssembly, string destinationAssembly, uint descriptorIndex)
	{
		var item = new TaskItem (sourceAssembly);
		item.SetMetadata ("DestinationPath", destinationAssembly);
		item.SetMetadata ("DescriptorIndex", descriptorIndex.ToString ());

		return item;
	}

	IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> GetCompressedAssemblyInfo ()
	{
		var key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
		Log.LogDebugMessage ($"Retrieving assembly compression info with key '{key}'");

		var compressedAssembliesInfo = BuildEngine4.UnregisterTaskObjectAssemblyLocal<IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (key, RegisteredTaskObjectLifetime.Build);

		if (compressedAssembliesInfo is null)
			throw new InvalidOperationException ($"Assembly compression info not found for key '{key}'. Compression will not be performed.");
		BuildEngine4.RegisterTaskObjectAssemblyLocal (key, compressedAssembliesInfo, RegisteredTaskObjectLifetime.Build);

		return compressedAssembliesInfo;
	}

	bool ShouldSkipAssembly (ITaskItem asm)
	{
		var should_skip = asm.GetMetadataOrDefault ("AndroidSkipAddToPackage", false);

		if (should_skip)
			Log.LogDebugMessage ($"Skipping {asm.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");

		return should_skip;
	}
}
