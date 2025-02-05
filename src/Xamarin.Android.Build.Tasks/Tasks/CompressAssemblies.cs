#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

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
	public ITaskItem [] AssembliesToCompress { get; set; } = [];

	[Output]
	public ITaskItem [] FailedToCompressAssembliesOutput { get; set; } = [];

	public override bool RunTask ()
	{
		var failed_assemblies = new List<ITaskItem> ();

		foreach (var assembly in AssembliesToCompress) {
			MonoAndroidHelper.LogIfReferenceAssembly (assembly, Log);

			if (!assembly.TryGetRequiredMetadata ("AssembliesToCompress", "DestinationPath", Log, out var destination_path))
				break;

			if (!assembly.TryGetRequiredMetadata ("AssembliesToCompress", "DescriptorIndex", Log, out var descriptor_index_string))
				break;

			if (!uint.TryParse (descriptor_index_string, out var descriptor_index)) {
				Log.LogError ($"Failed to parse 'DescriptorIndex' metadata value '{descriptor_index_string}' for assembly '{assembly.ItemSpec}'");
				break;
			}
			
			if (!AssemblyCompression.TryCompress (Log, assembly.ItemSpec, destination_path, descriptor_index)) {
				failed_assemblies.Add (assembly);
				continue;
			}

			Log.LogDebugMessage ($"Compressed '{assembly.ItemSpec}' to '{destination_path}'.");
		}

		FailedToCompressAssembliesOutput = failed_assemblies.ToArray ();

		return !Log.HasLoggedErrors;
	}
}
