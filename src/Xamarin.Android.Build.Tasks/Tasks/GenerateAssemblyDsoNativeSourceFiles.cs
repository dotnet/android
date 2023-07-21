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
		}

		foreach (ITaskItem assembly in Assemblies) {
			FileInfo fi = new (assembly.ItemSpec);
			string inputFile;
			bool compressed;
			ulong compressedSize;

			if (assemblyCompressor != null) {
				(inputFile, compressed) = assemblyCompressor.CompressAssembly (assembly, fi);

				if (!compressed) {
					compressedSize = 0;
				} else {
					var cfi = new FileInfo (inputFile);
					compressedSize = (ulong)cfi.Length;
				}
			} else {
				inputFile = assembly.ItemSpec;
				compressed = false;
				compressedSize = 0;
			}

			AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assembly);
			if (!dsoAssembliesInfo.TryGetValue (arch, out List<DSOAssemblyInfo>? assemblyList)) {
				assemblyList = new List<DSOAssemblyInfo> ();
				dsoAssembliesInfo.Add (arch, assemblyList);
			}
			assemblyList.Add (new DSOAssemblyInfo (GetAssemblyName (assembly), inputFile, (ulong)fi.Length, compressedSize));
		}

		return true;
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
