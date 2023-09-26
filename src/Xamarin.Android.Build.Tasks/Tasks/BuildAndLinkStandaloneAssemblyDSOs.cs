using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
		public readonly string SourceFilePath;
		public readonly string DSOPath;
		public readonly string? Culture;
		public readonly ITaskItem TaskItem;

		public TargetDSO (ITaskItem dso, string sourcesDir)
		{
			TaskItem = dso;
			DSOPath = dso.ItemSpec;
			Abi = EnsureValidMetadata ("Abi");
			OriginalAssemblyPath = EnsureValidMetadata ("InputAssemblyPath");
			SourceFilePath = Path.Combine (sourcesDir, EnsureValidMetadata ("SourceFileName"));
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

		foreach (ITaskItem item in TargetSharedLibraries) {
			var dso = new TargetDSO (item, SourcesOutputDirectory);
			string inputFilePath = AddAssembly (dso, assemblies);

			var dsoItem = new TaskItem (dso.DSOPath);

			dsoItem.SetMetadata ("DataSymbolOffset", "<TODO>");
			dsoItem.SetMetadata ("DataSize", "<TODO>");
			dsoItem.SetMetadata ("Compressed", "<TODO>");
			dsoItem.SetMetadata ("OriginalAssemblyPath", dso.OriginalAssemblyPath);
			dsoItem.SetMetadata ("InputAssemblyPath", inputFilePath);

			if (!String.IsNullOrEmpty (dso.Culture)) {
				dsoItem.SetMetadata ("SatelliteAssemblyCulture", dso.Culture);
			}

			sharedLibraries.Add (dsoItem);
		}

		SharedLibraries = sharedLibraries.ToArray ();
	}

	string AddAssembly (TargetDSO dso, Dictionary<string, Dictionary<AndroidTargetArch, DSOAssemblyInfo>> assemblies)
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
		DSOAssemblyInfo dsoInfo = MakeAssemblyInfo (dso.TaskItem, inputFile, cres.InputFileInfo.Length, cres.CompressedSize);

		try {
			infos.Add (targetArch, dsoInfo);
		} catch (Exception ex) {
			throw new InvalidOperationException ($"Internal error: failed to add '{dso.OriginalAssemblyPath}' for target arch {targetArch}", ex);
		}

		return dsoInfo.InputFile;
	}
}
