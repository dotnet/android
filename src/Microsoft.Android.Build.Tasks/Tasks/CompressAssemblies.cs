#nullable enable
using System.Collections.Generic;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

/// <summary>
/// Compresses assemblies using Zstandard compression before placing them in the APK.
/// Note this is independent of whether they are stored compressed with ZIP in the APK.
/// Our runtime bits will Zstd decompress them at assembly load time.
///
/// This task lives in Microsoft.Android.Build.Tasks.dll (net11.0) because it uses
/// System.IO.Compression.ZstandardEncoder, which is not available in netstandard2.0.
/// It is imported with &lt;UsingTask ... Runtime="NET" TaskFactory="TaskHostFactory" /&gt;.
/// </summary>
public class CompressAssemblies : AndroidTask
{
	public override string TaskPrefix => "CAS";

	[Required]
	public ITaskItem [] AssembliesToCompress { get; set; } = [];

	/// <summary>
	/// The Zstandard compression level (quality) to use, in the range 1-22. Higher values
	/// produce a smaller AssemblyStore at the cost of build time; decompression speed is
	/// independent of the level. Flows from the <c>$(_AndroidAssemblyStoreCompressionLevel)</c>
	/// MSBuild property.
	/// </summary>
	[Required]
	public int CompressionLevel { get; set; } = 3;

	[Output]
	public ITaskItem [] FailedToCompressAssembliesOutput { get; set; } = [];

	// Zstandard's valid quality range for the levels we expose (positive levels only).
	const int MinCompressionLevel = 1;
	const int MaxCompressionLevel = 22;

	public override bool RunTask ()
	{
		if (CompressionLevel < MinCompressionLevel || CompressionLevel > MaxCompressionLevel) {
			Log.LogCodedError ("XA5304", Properties.Resources.XA5304, CompressionLevel, MinCompressionLevel, MaxCompressionLevel);
			return false;
		}

		var failed_assemblies = new List<ITaskItem> ();

		foreach (var assembly in AssembliesToCompress) {
			ReferenceAssemblyChecker.LogIfReferenceAssembly (assembly, Log);

			if (!assembly.TryGetRequiredMetadata ("AssembliesToCompress", "DestinationPath", Log, out var destination_path))
				break;

			if (!assembly.TryGetRequiredMetadata ("AssembliesToCompress", "DescriptorIndex", Log, out var descriptor_index_string))
				break;

			if (!uint.TryParse (descriptor_index_string, out var descriptor_index)) {
				Log.LogCodedError ("XA5303", Properties.Resources.XA5303, descriptor_index_string, assembly.ItemSpec);
				break;
			}

			if (!AssemblyCompressor.TryCompress (Log, assembly.ItemSpec, destination_path, descriptor_index, CompressionLevel)) {
				failed_assemblies.Add (assembly);
				continue;
			}

			Log.LogDebugMessage ($"Compressed '{assembly.ItemSpec}' to '{destination_path}'.");
		}

		FailedToCompressAssembliesOutput = failed_assemblies.ToArray ();

		return !Log.HasLoggedErrors;
	}
}
