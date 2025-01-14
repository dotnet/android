#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Compresses assemblies using LZ4 compression before placing them in the APK.
/// Note this is independent of whether they are stored compressed with ZIP in the APK.
/// Our runtime bits will LZ4 decompress them at assembly load time.
/// </summary>
public class CompressAssemblies : AndroidTask
{
	public override string TaskPrefix => "CAS";

	[Required]
	public string ApkOutputPath { get; set; } = "";

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
	public ITaskItem [] ResolvedFrameworkAssembliesOutput { get; set; } = [];

	[Output]
	public ITaskItem [] ResolvedUserAssembliesOutput { get; set; } = [];

	public override bool RunTask ()
	{
		if (IncludeDebugSymbols || !EnableCompression || !EmbedAssemblies) {
			ResolvedFrameworkAssembliesOutput = ResolvedFrameworkAssemblies;
			ResolvedUserAssembliesOutput = ResolvedUserAssemblies;
			return true;
		}

		var compressed_assemblies_info = GetCompressedAssemblyInfo ();

		// Get all the user and framework assemblies we may need to compresss
		var assemblies = ResolvedFrameworkAssemblies.Concat (ResolvedUserAssemblies).Where (asm => !(ShouldSkipAssembly (asm))).ToArray ();
		var per_arch_assemblies = MonoAndroidHelper.GetPerArchAssemblies (assemblies, SupportedAbis, true);
		var compressed_output_dir = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (ApkOutputPath), "..", "lz4"));

		foreach (var kvp in per_arch_assemblies) {
			Log.LogDebugMessage ($"Compressing assemblies for architecture '{kvp.Key}'");

			foreach (var asm in kvp.Value.Values) {
				MonoAndroidHelper.LogIfReferenceAssembly (asm, Log);

				var compressed_assembly = AssemblyCompression.Compress (Log, asm, compressed_assemblies_info, compressed_output_dir);

				if (compressed_assembly.HasValue ()) {
					Log.LogDebugMessage ($"Compressed '{asm.ItemSpec}' to '{compressed_assembly}'.");
					asm.SetMetadata ("CompressedAssembly", compressed_assembly);
				}
			}
		}

		ResolvedFrameworkAssembliesOutput = ResolvedFrameworkAssemblies;
		ResolvedUserAssembliesOutput = ResolvedUserAssemblies;

		return !Log.HasLoggedErrors;
	}

	IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> GetCompressedAssemblyInfo ()
	{
		var key = CompressedAssemblyInfo.GetKey (ProjectFullPath);
		Log.LogDebugMessage ($"Retrieving assembly compression info with key '{key}'");

		var compressedAssembliesInfo = BuildEngine4.UnregisterTaskObjectAssemblyLocal<IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (key, RegisteredTaskObjectLifetime.Build);

		if (compressedAssembliesInfo is null)
			throw new InvalidOperationException ($"Assembly compression info not found for key '{key}'. Compression will not be performed.");

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
