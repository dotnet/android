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
		public readonly string InputAssemblyPath;
		public readonly string SourceFileName;
		public readonly string DSOPath;
		public readonly string? Culture;

		public TargetDSO (ITaskItem dso)
		{
			DSOPath = dso.ItemSpec;
			Abi = EnsureValidMetadata ("Abi");
			InputAssemblyPath = EnsureValidMetadata ("InputAssemblyPath");
			SourceFileName = EnsureValidMetadata ("SourceFileName");
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
		var sharedLibraries = new List<ITaskItem> ();
		foreach (ITaskItem item in TargetSharedLibraries) {
			var dso = new TargetDSO (item);
			var dsoItem = new TaskItem (dso.DSOPath);

			dsoItem.SetMetadata ("DataSymbolOffset", "<TODO>");
			dsoItem.SetMetadata ("DataSize", "<TODO>");
			dsoItem.SetMetadata ("Compressed", "<TODO>");
			dsoItem.SetMetadata ("InputAssemblyPath", dso.InputAssemblyPath);

			if (!String.IsNullOrEmpty (dso.Culture)) {
				dsoItem.SetMetadata ("SatelliteAssemblyCulture", dso.Culture);
			}

			sharedLibraries.Add (dsoItem);
		}

		SharedLibraries = sharedLibraries.ToArray ();
	}
}
