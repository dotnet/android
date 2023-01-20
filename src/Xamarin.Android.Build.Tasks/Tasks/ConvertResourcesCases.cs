// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class ConvertResourcesCases : AndroidTask
	{
		public override string TaskPrefix => "CRC";

		[Required]
		public ITaskItem[] ResourceDirectories { get; set; }

		[Required]
		public string CustomViewMapFile { get; set; }

		public string AndroidConversionFlagFile { get; set; }

		Dictionary<string,string> _resource_name_case_map;
		Dictionary<string, HashSet<string>> customViewMap;

		Dictionary<string, string> resource_name_case_map => _resource_name_case_map ??= MonoAndroidHelper.LoadResourceCaseMap (BuildEngine4, ProjectSpecificTaskObjectKey);

		public override bool RunTask ()
		{
			if (CustomViewMapFile != null)
				customViewMap = Xamarin.Android.Tasks.MonoAndroidHelper.LoadCustomViewMapFile (BuildEngine4, CustomViewMapFile);

			// Look in the resource xml's for capitalized stuff and fix them
			foreach (var dir in ResourceDirectories) {
				var skipResourceProcessing = dir.GetMetadata (ResolveLibraryProjectImports.AndroidSkipResourceProcessing);
				if (skipResourceProcessing != null && skipResourceProcessing.Equals ("true", StringComparison.OrdinalIgnoreCase)) {
					var originalFile = dir.GetMetadata (ResolveLibraryProjectImports.OriginalFile);
					Log.LogDebugMessage ($"Skipping: `{dir.ItemSpec}` via `{ResolveLibraryProjectImports.AndroidSkipResourceProcessing}`, original file: `{originalFile}`...");
					continue;
				}

				FixupResources (dir);
			}

			if (customViewMap != null)
				Xamarin.Android.Tasks.MonoAndroidHelper.SaveCustomViewMapFile (BuildEngine4, CustomViewMapFile, customViewMap);

			return !Log.HasLoggedErrors;
		}

		void FixupResources (ITaskItem item)
		{
			var resdir = item.ItemSpec;
			// Find all the xml and axml files
			var xmls = new List<string> ();
			var colorDir = Path.Combine (resdir, "color");
			var rawDir = Path.Combine (resdir, "raw");
			foreach (var file in Directory.GetFiles (resdir, "*.*xml", SearchOption.AllDirectories)) {
				if (file.StartsWith (colorDir, StringComparison.Ordinal) || file.StartsWith (rawDir, StringComparison.Ordinal))
					continue;
				var ext = Path.GetExtension (file);
				if (ext != ".xml" && ext != ".axml")
				    continue;
				xmls.Add (file);
			}

			var lastUpdate = DateTime.MinValue;
			if (!string.IsNullOrEmpty (AndroidConversionFlagFile) && File.Exists (AndroidConversionFlagFile)) {
				lastUpdate = File.GetLastWriteTimeUtc (AndroidConversionFlagFile);
			}
			Log.LogDebugMessage ("  AndroidConversionFlagFile modified: {0}", lastUpdate);

			var resourcedirectories = new List<string> ();
			foreach (var dir in ResourceDirectories) {
				if (dir == item)
					continue;
				resourcedirectories.Add (dir.ItemSpec);
			}

			// Fix up each file
			foreach (string file in xmls) {
				var srcmodifiedDate = File.GetLastWriteTimeUtc (file);
				if (srcmodifiedDate <= lastUpdate) {
					continue;
				}
				Log.LogDebugMessage ("  Processing: {0}   {1} > {2}", file, srcmodifiedDate, lastUpdate);
				Files.SetWriteable (file);
				bool success = AndroidResource.UpdateXmlResource (resdir, file,
					resourcedirectories, (level, message) => {
						switch (level) {
						case TraceLevel.Error:
							Log.FixupResourceFilenameAndLogCodedError ("XA1002", message, file, resdir, resource_name_case_map);
							break;
						case TraceLevel.Warning:
							Log.FixupResourceFilenameAndLogCodedWarning ("XA1001", message, file, resdir, resource_name_case_map);
							break;
						default:
							Log.LogDebugMessage (message);
							break;
						}
					}, registerCustomView : (e, filename) => {
					if (customViewMap == null)
						return;
					HashSet<string> set;
					if (!customViewMap.TryGetValue (e, out set))
						customViewMap.Add (e, set = new HashSet<string> ());
					set.Add (filename);
				});
				if (!success) {
					//If we failed to write the file, a warning is logged, we should skip to the next file
					continue;
				}

				// We strip away an eventual UTF-8 BOM from the XML file.
				// This is a requirement for the Android designer because the desktop Java renderer
				// doesn't support those type of BOM (it really wants the document to start
				// with "<?"). Since there is no way to plug into the file saving mechanism in X.S
				// we strip those here and point the designer to use resources from obj/
				Files.CleanBOM (file);
			}
		}
	}
}
