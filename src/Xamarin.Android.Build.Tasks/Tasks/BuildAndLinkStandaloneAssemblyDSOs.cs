using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

using TPLTask = System.Threading.Tasks.Task;

namespace Xamarin.Android.Tasks;

public class BuildAndLinkStandaloneAssemblyDSOs : AssemblyNativeSourceGenerationTask
{
	sealed class TargetDSO
	{
		public readonly string Abi;
		public readonly string OriginalAssemblyPath;
		public readonly string SourceFileBaseName;
		public readonly string DSOPath;
		public readonly string? Culture;
		public readonly ITaskItem TaskItem;

		public TargetDSO (ITaskItem dso)
		{
			TaskItem = dso;
			DSOPath = dso.ItemSpec;
			Abi = EnsureValidMetadata ("Abi");
			OriginalAssemblyPath = EnsureValidMetadata ("InputAssemblyPath");
			SourceFileBaseName = EnsureValidMetadata ("SourceFileBaseName");
			Culture = dso.GetMetadata ("SatelliteAssemblyCulture");

			string EnsureValidMetadata (string what)
			{
				string v = dso.GetMetadata (what);
				if (String.IsNullOrEmpty (v)) {
					throw new InvalidOperationException ($"Internal error: metadata '{what}' not found in item '{dso.ItemSpec}'");
				}

				return v;
			}
		}
	}

	sealed class LocalDSOAssemblyInfo : DSOAssemblyInfo
	{
		public readonly TargetDSO TargetDSO;
		public readonly ITaskItem SharedLibraryItem;

		public LocalDSOAssemblyInfo (TargetDSO targetDSO, ITaskItem sharedLibraryItem, string name, string inputFile, uint dataSize, uint compressedDataSize)
			: base (name, inputFile, dataSize, compressedDataSize)
		{
			TargetDSO = targetDSO;
			SharedLibraryItem = sharedLibraryItem;
		}
	}

	public override string TaskPrefix => "BALSAD";

	const uint DefaultParallelBuilds = 8;

	[Required]
	public ITaskItem[] TargetSharedLibraries { get; set; }

	[Required]
	public string SharedLibraryOutputDir { get; set; }

	[Required]
	public string AndroidBinUtilsDirectory { get; set; }

	public bool KeepGeneratedSources { get; set; }

	public string ParallelBuildsNumber { get; set; }

	[Output]
	public ITaskItem[] SharedLibraries { get; set; }

	protected override void Generate ()
	{
		var assemblies = new Dictionary<string, Dictionary<AndroidTargetArch, DSOAssemblyInfo>> (StringComparer.OrdinalIgnoreCase);
		var sharedLibraries = new List<ITaskItem> ();
		var supportedAbis = new HashSet<string> ();

		foreach (ITaskItem item in TargetSharedLibraries) {
			var dso = new TargetDSO (item);
			supportedAbis.Add (dso.Abi);

			var dsoItem = new TaskItem (dso.DSOPath);
			DSOAssemblyInfo dsoInfo = AddAssembly (dso, dsoItem, assemblies);

			dsoItem.SetMetadata ("Abi", dso.Abi);
			dsoItem.SetMetadata ("DataSymbolOffset", "<TODO>");
			dsoItem.SetMetadata ("DataSize", MonoAndroidHelper.CultureInvariantToString (dsoInfo.CompressedDataSize == 0 ? dsoInfo.DataSize : dsoInfo.CompressedDataSize));
			dsoItem.SetMetadata ("UncompressedDataSize", MonoAndroidHelper.CultureInvariantToString (dsoInfo.DataSize));
			dsoItem.SetMetadata ("Compressed", dsoInfo.CompressedDataSize == 0 ? "false" : "true");
			dsoItem.SetMetadata ("OriginalAssemblyPath", dso.OriginalAssemblyPath);
			dsoItem.SetMetadata ("InputAssemblyPath", dsoInfo.InputFile);

			if (!String.IsNullOrEmpty (dso.Culture)) {
				dsoItem.SetMetadata ("SatelliteAssemblyCulture", dso.Culture);
			}

			sharedLibraries.Add (dsoItem);
		}

		uint maxParallelBuilds;
		if (String.IsNullOrEmpty (ParallelBuildsNumber)) {
			maxParallelBuilds = DefaultParallelBuilds;
		} else if (!UInt32.TryParse (ParallelBuildsNumber, out maxParallelBuilds)) {
			Log.LogWarning ($"Unable to parse parallel builds number from '{ParallelBuildsNumber}', an unsigned integer is expected. Will default to {DefaultParallelBuilds}");
			maxParallelBuilds = DefaultParallelBuilds;
		}
		Log.LogDebugMessage ($"Will launch up to {maxParallelBuilds} builds at a time");

		// Adjust maxParallelBuilds to be the next highest even multiple of supportedAbisCount, since each assembly will be built supportedAbisCount times and we
		// want to build those sources together
		uint supportedAbisCount = (uint)supportedAbis.Count;
		if (maxParallelBuilds < supportedAbisCount) {
			maxParallelBuilds = supportedAbisCount;
			Log.LogDebugMessage ($"Maximum parallel builds number adjusted to match the number of supported ABIs, {supportedAbisCount}");
		} else if (maxParallelBuilds % supportedAbisCount != 0) {
			maxParallelBuilds += supportedAbisCount - (maxParallelBuilds % supportedAbisCount);
			Log.LogDebugMessage ($"Maximum parallel builds number adjusted to the next even multiple of number of abis, {maxParallelBuilds}");
		}

		var sourcesBatch = new List<GeneratedSource> ();
		uint remainingSources = (uint)assemblies.Count * supportedAbisCount;
		foreach (var kvp in assemblies) {
			Dictionary<AndroidTargetArch, DSOAssemblyInfo> infos = kvp.Value;

			string baseName = String.Empty;
			foreach (DSOAssemblyInfo info in infos.Values) {
				var localInfo = (LocalDSOAssemblyInfo)info;

				// All the architectures share the same base file name
				baseName = localInfo.TargetDSO.SourceFileBaseName;
				break;
			}

			var generator = new AssemblyDSOGenerator (infos);
			List<GeneratedSource> generatedSources = GenerateSources (supportedAbis, generator, generator.Construct (), baseName);
			sourcesBatch.AddRange (generatedSources);
			if ((uint)sourcesBatch.Count < maxParallelBuilds && remainingSources >= maxParallelBuilds) {
				continue;
			}

			CompileAndLink (sourcesBatch, infos);

			remainingSources -= (uint)sourcesBatch.Count;
			sourcesBatch.Clear ();
		}

		SharedLibraries = sharedLibraries.ToArray ();
	}

	void CompileAndLink (List<GeneratedSource> generatedSources, Dictionary<AndroidTargetArch, DSOAssemblyInfo> infos)
	{
		Log.LogDebugMessage ($"Compiling and linking {generatedSources.Count} shared libraries in parallel");
		List<NativeCompilationHelper.AssemblerConfig> configs = GetAssemblerConfigs (generatedSources);
		var tasks = new List<TPLTask> ();

		foreach (NativeCompilationHelper.AssemblerConfig config in configs) {
			tasks.Add (TPLTask.Factory.StartNew (() => DoCompileAndLink (config)));
		}

		// TODO: add timeout
		// TODO: add cancellation support
		try {
			TPLTask.WaitAll (tasks.ToArray ());
		} catch (AggregateException aex) {
			foreach (Exception ex in aex.InnerExceptions) {
				Log.LogErrorFromException (ex);
			}

			throw new InvalidOperationException ("Native compilation failed");
		}
	}

	void DoCompileAndLink (NativeCompilationHelper.AssemblerConfig config)
	{
		NativeCompilationHelper.RunAssembler (config);

		if (KeepGeneratedSources) {
			return;
		}

		try {
			if (File.Exists (config.InputSource)) {
				File.Delete (config.InputSource);
			}
		} catch (Exception ex) {
			Log.LogDebugMessage ($"Generated source file '{config.InputSource}' not removed. Exception was thrown while removing it: {ex}");
		}
	}

	List<NativeCompilationHelper.AssemblerConfig> GetAssemblerConfigs (List<GeneratedSource> generatedSources)
	{
		string assemblerPath = NativeCompilationHelper.GetAssemblerPath (AndroidBinUtilsDirectory);
		string workingDirectory = Path.GetFullPath (SourcesOutputDirectory);

		var configs = new List<NativeCompilationHelper.AssemblerConfig> ();
		foreach (GeneratedSource source in generatedSources) {
			string sourceFile = Path.GetFileName (source.FilePath);

			var config = new NativeCompilationHelper.AssemblerConfig (
				log: Log,
				assemblerPath: assemblerPath,
				inputSource: sourceFile,
				workingDirectory: workingDirectory
			);

			configs.Add (config);
		}

		return configs;
	}

	DSOAssemblyInfo AddAssembly (TargetDSO dso, ITaskItem dsoItem, Dictionary<string, Dictionary<AndroidTargetArch, DSOAssemblyInfo>> assemblies)
	{
		string asmName = Path.GetFileNameWithoutExtension (dso.OriginalAssemblyPath);
		if (!String.IsNullOrEmpty (dso.Culture)) {
			asmName = $"{dso.Culture}/{asmName}";
		}

		if (!assemblies.TryGetValue (asmName, out Dictionary<AndroidTargetArch, DSOAssemblyInfo> infos)) {
			infos = new Dictionary<AndroidTargetArch, DSOAssemblyInfo> ();
			assemblies.Add (asmName, infos);
		}

		string destinationSubdirectory = dso.Abi;
		if (!String.IsNullOrEmpty (dso.Culture)) {
			destinationSubdirectory = Path.Combine (destinationSubdirectory, dso.Culture);
		}

		CompressionResult cres = Compress (dso.OriginalAssemblyPath, destinationSubdirectory);
		string inputFile = cres.OutputFile;

		AndroidTargetArch targetArch = MonoAndroidHelper.AbiToTargetArch (dso.Abi);
		DSOAssemblyInfo dsoInfo = new LocalDSOAssemblyInfo (dso, dsoItem, GetAssemblyName (dso.TaskItem), inputFile, (uint)cres.InputFileInfo.Length, cres.CompressedSize);

		try {
			infos.Add (targetArch, dsoInfo);
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Internal error: failed to add '{dso.OriginalAssemblyPath}' for target arch {targetArch}", ex);
		}

		return dsoInfo;
	}
}
