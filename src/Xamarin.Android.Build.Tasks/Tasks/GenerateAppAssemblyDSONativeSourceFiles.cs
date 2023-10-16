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

	[Required]
	public ITaskItem[] StandaloneAssemblyDSOs { get; set; }

	protected override void Generate ()
	{
		Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssembliesInfo = new ();

		var satelliteAssemblies = new List<DSOAssemblyInfo> ();

		// The figure here includes only the "fast path" assemblies, that is those which end up in libxamarin-app.so
		ulong inputAssemblyDataSize = 0;

		// This variable, however, keeps the total size of all the assemblies, both from libxamarin-app.so and from
		// their respective .so shared libraries.
		ulong uncompressedAssemblyDataSize = 0;
		AndroidTargetArch arch;
		var fastPathAssemblies = new HashSet<string> (FastPathAssemblyNames, StringComparer.OrdinalIgnoreCase);

		Log.LogDebugMessage ("Processing input assemblies");
		foreach (ITaskItem assembly in Assemblies) {
			if (!fastPathAssemblies.Contains (Path.GetFileName (assembly.ItemSpec))) {
				continue;
			}

			CompressionResult cres = Compress (assembly);
			string inputFile = cres.OutputFile;

			Log.LogDebugMessage ($"  Fast path input: {assembly.ItemSpec}; compressed? {cres.Compressed}");
			inputAssemblyDataSize += cres.Compressed ? (ulong)cres.InputFileInfo.Length : cres.CompressedSize;
			uncompressedAssemblyDataSize += (ulong)cres.InputFileInfo.Length;

			if (!MonoAndroidHelper.IsSatelliteAssembly (assembly)) {
				arch = MonoAndroidHelper.GetTargetArch (assembly);
				StoreAssembly (arch, assembly, inputFile, cres.InputFileInfo.Length, cres.CompressedSize);
				continue;
			}

			// Satellite assemblies don't have any ABI, so they need to be added to all supported architectures.
			// We will do it after this loop, when all architectures are known.
			satelliteAssemblies.Add (MakeAssemblyInfo (assembly, inputFile, cres.InputFileInfo.Length, cres.CompressedSize));
		}

		foreach (ITaskItem dsoItem in StandaloneAssemblyDSOs) {
			arch = MonoAndroidHelper.GetTargetArch (dsoItem);
			DSOAssemblyInfo info = MakeStandaloneAssemblyInfo (dsoItem);

			Log.LogDebugMessage ($"  Standalone input: {info.Name}");
			uncompressedAssemblyDataSize += info.DataSize;
			AddAssemblyToList (arch, info);
		}

		if (satelliteAssemblies.Count > 0) {
			foreach (DSOAssemblyInfo info in satelliteAssemblies) {
				foreach (AndroidTargetArch dsoArch in dsoAssembliesInfo.Keys.ToList ()) {
					AddAssemblyToList (dsoArch, info);
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

	DSOAssemblyInfo MakeStandaloneAssemblyInfo (ITaskItem dsoItem)
	{
		string name = Path.GetFileName (GetRequiredMetadata (DSOMetadata.OriginalAssemblyPath));
		string? cultureName = dsoItem.GetMetadata (DSOMetadata.SatelliteAssemblyCulture);

		if (!String.IsNullOrEmpty (cultureName)) {
			name = $"{cultureName}/{name}";
		}

		string inputFile = GetRequiredMetadata (DSOMetadata.InputAssemblyPath);
		string compressed = GetRequiredMetadata (DSOMetadata.Compressed);
		if (!Boolean.TryParse (compressed, out bool isCompressed)) {
			throw new InvalidOperationException ($"Internal error: unable to parse '{compressed}' as a boolean value, from the '{DSOMetadata.Compressed}' metadata of item {dsoItem}");
		}

		uint dataSize = GetUintFromRequiredMetadata (DSOMetadata.DataSize);
		uint compressedDataSize;
		if (!isCompressed) {
			compressedDataSize = 0;
		} else {
			compressedDataSize = dataSize;
			dataSize = GetUintFromRequiredMetadata (DSOMetadata.UncompressedDataSize);

		}

		return new DSOAssemblyInfo (name, inputFile, dataSize, compressedDataSize, isStandalone: true, Path.GetFileName (dsoItem.ItemSpec)) {
			AssemblyLoadInfoIndex = GetUintFromRequiredMetadata (DSOMetadata.AssemblyLoadInfoIndex),
		};

		string GetRequiredMetadata (string name)
		{
			string ret = dsoItem.GetMetadata (name);
			if (String.IsNullOrEmpty (ret)) {
				throw new InvalidOperationException ($"Internal error: item {dsoItem} doesn't contain required metadata item '{name}' or its value is an empty string");
			}

			return ret;
		}

		uint GetUintFromRequiredMetadata (string name)
		{
			string metadata = GetRequiredMetadata (name);
			if (!UInt32.TryParse (metadata, out uint value)) {
				throw new InvalidOperationException ($"Internal error: unable to parse '{metadata}' as a UInt32 value, from the '{name}' metadata of item {dsoItem}");
			}

			return value;
		}
	}
}
