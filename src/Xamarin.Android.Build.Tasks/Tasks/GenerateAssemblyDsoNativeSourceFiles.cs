using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateAssemblyDsoNativeSourceFiles : AndroidTask
{
	public override string TaskPrefix => "GADNSF";

	[Required]
	public string SourcesOutputDirectory { get; set; }

	[Required]
	public string CompressedAssembliesOutputDirectory { get; set; }

	[Required]
	public string[] SupportedAbis { get; set; }

	[Required]
	public ITaskItem[] Assemblies { get; set; }

	[Required]
	public bool EnableCompression { get; set; }

	public override bool RunTask ()
	{
		Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssembliesInfo = new ();
		AssemblyCompression? assemblyCompressor = null;
		if (EnableCompression) {
			assemblyCompressor = new AssemblyCompression (Log, CompressedAssembliesOutputDirectory);
			Log.LogDebugMessage ("Assembly compression ENABLED");
		} else {
			Log.LogDebugMessage ("Assembly compression DISABLED");
		}

		ulong assemblyDataSize = 0;
		Log.LogDebugMessage ("Processing the input assemblies");
		foreach (ITaskItem assembly in Assemblies) {
			FileInfo fi = new (assembly.ItemSpec);
			string inputFile;
			bool compressed;
			uint compressedSize;

			Log.LogDebugMessage ($"  Input: {assembly.ItemSpec}");
			if (assemblyCompressor != null) {
				(inputFile, compressed) = assemblyCompressor.CompressAssembly (assembly, fi);

				if (!compressed) {
					compressedSize = 0;
				} else {
					var cfi = new FileInfo (inputFile);
					compressedSize = (uint)cfi.Length;
				}
			} else {
				inputFile = assembly.ItemSpec;
				compressed = false;
				compressedSize = 0;
			}
			assemblyDataSize += compressedSize == 0 ? (ulong)fi.Length : compressedSize;
			Log.LogDebugMessage ($"    will include from: {inputFile} (compressed? {compressed}; compressedSize == {compressedSize}");
			AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assembly);
			if (!dsoAssembliesInfo.TryGetValue (arch, out List<DSOAssemblyInfo>? assemblyList)) {
				assemblyList = new List<DSOAssemblyInfo> ();
				dsoAssembliesInfo.Add (arch, assemblyList);
			}
			assemblyList.Add (new DSOAssemblyInfo (GetAssemblyName (assembly), inputFile, (uint)fi.Length, compressedSize));
			Log.LogDebugMessage ($"    added to list with name: {assemblyList[assemblyList.Count - 1].Name}");
		}

		Log.LogDebugMessage ($"Size of assembly data to stash: {assemblyDataSize}");
		Log.LogDebugMessage ($"Number of architectures to stash into DSOs: {dsoAssembliesInfo.Count}");
		foreach (var kvp in dsoAssembliesInfo) {
			Log.LogDebugMessage ($"  {kvp.Key}: {kvp.Value.Count} assemblies");
		}

		var generator = new AssemblyDSOGenerator (dsoAssembliesInfo);
		LLVMIR.LlvmIrModule module = generator.Construct ();

		return !Log.HasLoggedErrors;
	}

	string GetAssemblyName (ITaskItem assembly)
	{
		if (!assembly.ItemSpec.EndsWith (".resources.dll", StringComparison.OrdinalIgnoreCase)) {
			return Path.GetFileName (assembly.ItemSpec);
		}

		// It's a satellite assembly, %(DestinationSubDirectory) is the culture prefix
		string? destinationSubDir = assembly.GetMetadata ("DestinationSubDirectory");
		if (String.IsNullOrEmpty (destinationSubDir)) {
			throw new InvalidOperationException ($"Satellite assembly '{assembly.ItemSpec}' has no culture metadata item");
		}

		string ret = $"{destinationSubDir}{Path.GetFileName (assembly.ItemSpec)}";
		if (!assembly.ItemSpec.EndsWith (ret, StringComparison.OrdinalIgnoreCase)) {
			throw new InvalidOperationException ($"Invalid metadata in satellite assembly '{assembly.ItemSpec}', culture metadata ('{destinationSubDir}') doesn't match file path");
		}

		return ret;
	}
}
