using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CollectPdbFiles : AndroidTask
	{
		public override string TaskPrefix => "CPF";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Output]
		public ITaskItem[] PdbFiles { get; set; }

		[Output]
		public ITaskItem[] PortablePdbFiles { get; set; }

		public bool LegacySymbols { get; set; }

		public override bool RunTask ()
		{
			var pdbFiles = new List<ITaskItem> ();
			var portablePdbFiles = new List<ITaskItem> ();

			foreach (var file in ResolvedAssemblies) {
				var pdbFile = file.ItemSpec;

				if (!File.Exists (pdbFile))
					continue;

				if (Files.IsPortablePdb (pdbFile)) {
					portablePdbFiles.Add (file);
				} else {
					pdbFiles.Add (file);
					if (!LegacySymbols) {
						// Warn for <ProjectReference/> where a non-portable pdb was found
						var project = file.GetMetadata ("MSBuildSourceProjectFile");
						if (!string.IsNullOrEmpty (project)) {
							Log.LogCodedWarning ("XA0122", project, lineNumber: 0,
								message: $"{project} is generating legacy symbols that disables debugging for this project. Use 'DebugType=portable' instead.");
						}
					}
				}
			}

			PdbFiles = pdbFiles.ToArray ();
			PortablePdbFiles = portablePdbFiles.ToArray ();

			Log.LogDebugTaskItems ("  [Output] PdbFiles:", PdbFiles);
			Log.LogDebugTaskItems ("  [Output] PortablePdbFiles:", PortablePdbFiles);

			return !Log.HasLoggedErrors;
		}
	}
}
