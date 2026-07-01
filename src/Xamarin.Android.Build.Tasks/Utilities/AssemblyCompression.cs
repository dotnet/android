#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Reader-side helpers for Zstandard-compressed AssemblyStore assemblies. The actual
	/// compression is performed by the <c>CompressAssemblies</c> task in
	/// Microsoft.Android.Build.Tasks.dll (net11.0), which uses
	/// <c>System.IO.Compression.ZstandardEncoder</c>.
	/// </summary>
	class AssemblyCompression
	{
		// Gets the descriptor index for the specified assembly from the compressed assembly info
		public static bool TryGetDescriptorIndex (TaskLoggingHelper log, ITaskItem assembly, IDictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>> compressedAssembliesInfo, out uint descriptorIndex)
		{
			descriptorIndex = 0;

			var key = CompressedAssemblyInfo.GetDictionaryKey (assembly);
			var arch = MonoAndroidHelper.GetTargetArch (assembly);

			if (!compressedAssembliesInfo.TryGetValue (arch, out Dictionary<string, CompressedAssemblyInfo> assembliesInfo)) {
				throw new InvalidOperationException ($"Internal error: compression assembly info for architecture {arch} not available");
			}

			if (!assembliesInfo.TryGetValue (key, out CompressedAssemblyInfo info) || info == null) {
				log.LogDebugMessage ($"Assembly missing from {nameof (CompressedAssemblyInfo)}: {key}");
				return false;
			}

			descriptorIndex = info.DescriptorIndex;

			return true;
		}

		// Gets the output path for the compressed assembly
		public static string GetCompressedAssemblyOutputPath (ITaskItem assembly, string compressedOutputDir)
		{
			var assemblyOutputDir = GetCompressedAssemblyOutputDirectory (assembly, compressedOutputDir);
			return Path.Combine (assemblyOutputDir, $"{Path.GetFileName (assembly.ItemSpec)}.zst");
		}

		static string GetCompressedAssemblyOutputDirectory (ITaskItem assembly, string compressedOutputDir)
		{
			string assemblyOutputDir;
			string subDirectory = assembly.GetMetadata ("DestinationSubDirectory");
			string abi = MonoAndroidHelper.GetAssemblyAbi (assembly);

			if (!subDirectory.IsNullOrEmpty () && !(subDirectory.EndsWith ($"{abi}/", StringComparison.Ordinal) || subDirectory.EndsWith ($"{abi}\\", StringComparison.Ordinal))) {
				assemblyOutputDir = Path.Combine (compressedOutputDir, abi, subDirectory);
			} else {
				assemblyOutputDir = Path.Combine (compressedOutputDir, abi);
			}

			return assemblyOutputDir;
		}
	}
}
