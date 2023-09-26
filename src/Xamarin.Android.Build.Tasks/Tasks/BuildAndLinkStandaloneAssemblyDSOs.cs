using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

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
			GenerateSources (supportedAbis, generator, generator.Construct (), baseName);
		}

		SharedLibraries = sharedLibraries.ToArray ();
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
