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

	[Required]
	public ITaskItem[] TargetSharedLibraries { get; set; }

	[Required]
	public string SharedLibraryOutputDir { get; set; }

	[Required]
	public string AndroidBinUtilsDirectory { get; set; }

	public bool KeepGeneratedSources { get; set; }

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

			CompileAndLink (generatedSources, infos);

			if (!KeepGeneratedSources) {
				foreach (GeneratedSource src in generatedSources) {
					try {
						if (File.Exists (src.FilePath)) {
							File.Delete (src.FilePath);
						}
					} catch (Exception ex) {
						Log.LogDebugMessage ($"Generated source file '{src.FilePath}' not removed. Exception was thrown while removing it: {ex}");
					}
				}
			}
		}

		SharedLibraries = sharedLibraries.ToArray ();
	}

	void CompileAndLink (List<GeneratedSource> generatedSources, Dictionary<AndroidTargetArch, DSOAssemblyInfo> infos)
	{
		List<NativeCompilationHelper.Config> configs = GetAssemblerConfigs (generatedSources);
		var tasks = new List<TPLTask> ();

		foreach (NativeCompilationHelper.Config config in configs) {
			tasks.Add (TPLTask.Factory.StartNew (() => NativeCompilationHelper.RunAssembler (config)));
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

	List<NativeCompilationHelper.Config> GetAssemblerConfigs (List<GeneratedSource> generatedSources)
	{
		string assemblerPath = NativeCompilationHelper.GetAssemblerPath (AndroidBinUtilsDirectory);
		string workingDirectory = Path.GetFullPath (SourcesOutputDirectory);

		var configs = new List<NativeCompilationHelper.Config> ();
		foreach (GeneratedSource source in generatedSources) {
			string sourceFile = Path.GetFileName (source.FilePath);

			var config = new NativeCompilationHelper.Config (
				log: Log,
				assemblerPath: assemblerPath,
				assemblerOptions: NativeCompilationHelper.MakeAssemblerOptions (sourceFile),
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
