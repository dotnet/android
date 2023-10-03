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

		public ulong? XAInputAssemblyDataOffset { get; set; }

		public LocalDSOAssemblyInfo (TargetDSO targetDSO, ITaskItem sharedLibraryItem, string name, string inputFile, uint dataSize, uint compressedDataSize)
			: base (name, inputFile, dataSize, compressedDataSize)
		{
			TargetDSO = targetDSO;
			SharedLibraryItem = sharedLibraryItem;
		}
	}

	public static class DSOMetadata
	{
		public const string Compressed               = "Compressed";
		public const string DataSize                 = "DataSize";
		public const string DataSymbolOffset         = "DataSymbolOffset";
		public const string InputAssemblyPath        = "InputAssemblyPath";
		public const string OriginalAssemblyPath     = "OriginalAssemblyPath";
		public const string SatelliteAssemblyCulture = "SatelliteAssemblyCulture";
		public const string UncompressedDataSize     = "UncompressedDataSize";
	}

	const uint DefaultParallelBuilds = 8;

	public override string TaskPrefix => "BALSAD";

	[Required]
	public ITaskItem[] TargetSharedLibraries { get; set; }

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
			dsoItem.SetMetadata (DSOMetadata.DataSize, MonoAndroidHelper.CultureInvariantToString (dsoInfo.CompressedDataSize == 0 ? dsoInfo.DataSize : dsoInfo.CompressedDataSize));
			dsoItem.SetMetadata (DSOMetadata.UncompressedDataSize, MonoAndroidHelper.CultureInvariantToString (dsoInfo.DataSize));
			dsoItem.SetMetadata (DSOMetadata.Compressed, dsoInfo.CompressedDataSize == 0 ? "false" : "true");
			dsoItem.SetMetadata (DSOMetadata.OriginalAssemblyPath, dso.OriginalAssemblyPath);
			dsoItem.SetMetadata (DSOMetadata.InputAssemblyPath, dsoInfo.InputFile);

			if (!String.IsNullOrEmpty (dso.Culture)) {
				dsoItem.SetMetadata (DSOMetadata.SatelliteAssemblyCulture, dso.Culture);
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
			List<GeneratedSource> generatedSources = GenerateSources (supportedAbis, generator, generator.Construct (), baseName, sourceState: infos);
			sourcesBatch.AddRange (generatedSources);
			if ((uint)sourcesBatch.Count < maxParallelBuilds && remainingSources >= maxParallelBuilds) {
				continue;
			}

			CompileAndLink (sourcesBatch);

			remainingSources -= (uint)sourcesBatch.Count;
			sourcesBatch.Clear ();
		}

		SharedLibraries = sharedLibraries.ToArray ();
	}

	void CompileAndLink (List<GeneratedSource> generatedSources)
	{
		Log.LogDebugMessage ($"Compiling and linking {generatedSources.Count} shared libraries in parallel");
		List<NativeCompilationHelper.AssemblerConfig> configs = GetAssemblerConfigs (generatedSources);
		var tasks = new List<TPLTask> ();

		foreach (NativeCompilationHelper.AssemblerConfig config in configs) {
			var infos = config.State as Dictionary<AndroidTargetArch, DSOAssemblyInfo>;
			if (infos == null) {
				throw new InvalidOperationException ($"Internal error: state for '{config.OutputFile}' is of invalid type.");
			}

			tasks.Add (TPLTask.Factory.StartNew (() => DoCompileAndLink (config, infos)));
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

	void DoCompileAndLink (NativeCompilationHelper.AssemblerConfig config, Dictionary<AndroidTargetArch, DSOAssemblyInfo> infos)
	{
		bool success = NativeCompilationHelper.RunAssembler (config);
		if (!success) {
			return;
		}

		if (!infos.TryGetValue (config.TargetArch, out DSOAssemblyInfo genericDsoInfo)) {
			throw new InvalidOperationException ($"Internal error: DSO info for arch '{config.TargetArch}' not found (input file: {config.InputSource})");
		}

		var dsoInfo = genericDsoInfo as LocalDSOAssemblyInfo;
		if (dsoInfo == null) {
			throw new InvalidOperationException ($"Internal error: DSO info must be an instance of {nameof(LocalDSOAssemblyInfo)}, but it was {genericDsoInfo.GetType ()} instead");
		}

		var linkerConfig = new NativeCompilationHelper.LinkerConfig (
			log: Log,
			targetArch: config.TargetArch,
			linkerPath: NativeCompilationHelper.GetLinkerPath (AndroidBinUtilsDirectory),
			outputSharedLibrary: dsoInfo.SharedLibraryItem.ItemSpec
		);

		string inputObjectFile;
		if (!String.IsNullOrEmpty (config.WorkingDirectory)) {
			inputObjectFile = Path.Combine (config.WorkingDirectory, config.OutputFile);
		} else {
			inputObjectFile = config.OutputFile;
		}
		linkerConfig.ObjectFilePaths.Add (inputObjectFile);

		success = NativeCompilationHelper.RunLinker (linkerConfig);
		if (!success || KeepGeneratedSources) {
			return;
		}

		(dsoInfo.XAInputAssemblyDataOffset, ulong? symbolSize) = ELFHelper.GetExportedSymbolOffsetAndSize (Log, linkerConfig.OutputFile, AssemblyDSOGenerator.XAInputAssemblyDataVarName);
		if (dsoInfo.XAInputAssemblyDataOffset == null) {
			Log.LogError ($"Shared library '{linkerConfig.OutputFile}' does not export the required symbol '{AssemblyDSOGenerator.XAInputAssemblyDataVarName}'");
			return;
		}
		Log.LogDebugMessage ($"Shared library '{linkerConfig.OutputFile}' has symbol '{AssemblyDSOGenerator.XAInputAssemblyDataVarName}' at offset {dsoInfo.XAInputAssemblyDataOffset}");
		dsoInfo.SharedLibraryItem.SetMetadata (DSOMetadata.DataSymbolOffset, MonoAndroidHelper.CultureInvariantToString (dsoInfo.XAInputAssemblyDataOffset));

		ulong expectedSize;
		if (dsoInfo.CompressedDataSize == 0) {
			expectedSize = dsoInfo.DataSize;
		} else {
			expectedSize = dsoInfo.CompressedDataSize;
		}

		if (expectedSize != symbolSize) {
			Log.LogError ($"Shared library '{linkerConfig.OutputFile}' symbol '{AssemblyDSOGenerator.XAInputAssemblyDataVarName}' has invalid size {symbolSize} (expected {expectedSize})");
			return;
		}

		string sourceFile;
		if (!String.IsNullOrEmpty (config.WorkingDirectory)) {
			sourceFile = Path.Combine (config.WorkingDirectory, config.InputSource);
		} else {
			sourceFile = config.InputSource;
		}

		try {
			Log.LogDebugMessage ($"Will delete source: {sourceFile}");
			if (File.Exists (sourceFile)) {
				File.Delete (sourceFile);
			} else {
				Log.LogDebugMessage ("  file doesn't exist");
			}
		} catch (Exception ex) {
			Log.LogDebugMessage ($"Generated source file '{sourceFile}' not removed. Exception was thrown while removing it: {ex}");
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
				targetArch: source.TargetArch,
				assemblerPath: assemblerPath,
				inputSource: sourceFile,
				workingDirectory: workingDirectory
			) {
				State = source.State,
			};

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
