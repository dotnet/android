using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class GenerateAppAssemblyDSONativeSourceFiles : AssemblyNativeSourceGenerationTask
{
	public override string TaskPrefix => "GADNSF";

	[Required]
	public string[] SupportedAbis { get; set; }

	[Required]
	public ITaskItem[] Assemblies { get; set; }

	[Required]
	public string[] FastPathAssemblyNames { get; set; }

	protected override void Generate ()
	{
		Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssembliesInfo = new ();

		var satelliteAssemblies = new List<DSOAssemblyInfo> ();
		ulong inputAssemblyDataSize = 0;
		ulong uncompressedAssemblyDataSize = 0;
		Log.LogDebugMessage ("Processing the input assemblies");
		foreach (ITaskItem assembly in Assemblies) {
			CompressionResult cres = Compress (assembly);
			string inputFile = cres.OutputFile;

			Log.LogDebugMessage ($"  Input: {assembly.ItemSpec}");
			inputAssemblyDataSize += cres.Compressed ? (ulong)cres.InputFileInfo.Length : cres.CompressedSize;
			uncompressedAssemblyDataSize += (ulong)cres.InputFileInfo.Length;

			AndroidTargetArch arch;
			if (!MonoAndroidHelper.IsSatelliteAssembly (assembly)) {
				arch = MonoAndroidHelper.GetTargetArch (assembly);
				StoreAssembly (arch, assembly, inputFile, cres.InputFileInfo.Length, cres.CompressedSize);
				continue;
			}

			// Satellite assemblies don't have any ABI, so they need to be added to all supported architectures.
			// We will do it after this loop, when all architectures are known.
			satelliteAssemblies.Add (MakeAssemblyInfo (assembly, inputFile, cres.InputFileInfo.Length, cres.CompressedSize));
		}

		if (satelliteAssemblies.Count > 0) {
			foreach (DSOAssemblyInfo info in satelliteAssemblies) {
				foreach (AndroidTargetArch arch in dsoAssembliesInfo.Keys.ToList ()) {
					AddAssemblyToList (arch, info);
				}
			}
		}

		Log.LogDebugMessage ($"Size of assembly data to stash: {inputAssemblyDataSize}");
		Log.LogDebugMessage ($"Number of architectures to stash into DSOs: {dsoAssembliesInfo.Count}");
		foreach (var kvp in dsoAssembliesInfo) {
			Log.LogDebugMessage ($"  {kvp.Key}: {kvp.Value.Count} assemblies");
		}

		var generator = new AssemblyDSOGenerator (FastPathAssemblyNames, dsoAssembliesInfo, inputAssemblyDataSize, uncompressedAssemblyDataSize);
		GenerateSources (SupportedAbis, generator, generator.Construct (), PrepareAbiItems.AssemblyDSOBase);

		void StoreAssembly (AndroidTargetArch arch, ITaskItem assembly, string inputFile, long fileLength, uint compressedSize, DSOAssemblyInfo? info = null)
		{
			AddAssemblyToList (arch, MakeAssemblyInfo (assembly, inputFile, fileLength, compressedSize));
		}

		void AddAssemblyToList (AndroidTargetArch arch, DSOAssemblyInfo info)
		{
			if (!dsoAssembliesInfo.TryGetValue (arch, out List<DSOAssemblyInfo>? assemblyList)) {
				assemblyList = new List<DSOAssemblyInfo> ();
				dsoAssembliesInfo.Add (arch, assemblyList);
			}
			assemblyList.Add (info);
			Log.LogDebugMessage ($"    added to arch {arch} with name: {assemblyList[assemblyList.Count - 1].Name}");
		}
	}
}
