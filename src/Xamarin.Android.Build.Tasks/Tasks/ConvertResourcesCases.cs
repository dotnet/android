// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;

namespace Xamarin.Android.Tasks
{
	public class ConvertResourcesCases : Task
	{
		[Required]
		public ITaskItem[] ResourceDirectories { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		public string AndroidConversionFlagFile { get; set; }

		public string ResourceNameCaseMap { get; set; }

		Dictionary<string,string> resource_name_case_map;

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ConvertResourcesCases Task");
			Log.LogDebugMessage ("  ResourceDirectories: {0}", ResourceDirectories);
			Log.LogDebugMessage ("  AcwMapFile: {0}", AcwMapFile);
			Log.LogDebugMessage ("  AndroidConversionFlagFile: {0}", AndroidConversionFlagFile);
			Log.LogDebugMessage ("  ResourceNameCaseMap: {0}", ResourceNameCaseMap);

			resource_name_case_map = MonoAndroidHelper.LoadResourceCaseMap (ResourceNameCaseMap);
			var acw_map = MonoAndroidHelper.LoadAcwMapFile (AcwMapFile);

			// Look in the resource xml's for capitalized stuff and fix them
			FixupResources (acw_map);

			return true;
		}


		void FixupResources (Dictionary<string, string> acwMap)
		{
			foreach (var dir in ResourceDirectories)
				FixupResources (dir, acwMap);
		}

		void FixupResources (ITaskItem item, Dictionary<string, string> acwMap)
		{
			var resdir = item.ItemSpec;
			// Find all the xml and axml files
			var xmls = new[] { resdir }
				.Concat (Directory.EnumerateDirectories (resdir, "*", SearchOption.AllDirectories)
					.Except (Directory.EnumerateDirectories (resdir, "color*", SearchOption.TopDirectoryOnly))
					.Except (Directory.EnumerateDirectories (resdir, "raw*", SearchOption.TopDirectoryOnly)))
				.SelectMany (dir => Directory.EnumerateFiles (dir, "*.xml")
					.Concat (Directory.EnumerateFiles (dir, "*.axml")));

			var lastUpdate = DateTime.MinValue;
			if (!string.IsNullOrEmpty (AndroidConversionFlagFile) && File.Exists (AndroidConversionFlagFile)) {
				lastUpdate = File.GetLastWriteTimeUtc (AndroidConversionFlagFile);
			}
			Log.LogDebugMessage ("  AndroidConversionFlagFile modified: {0}", lastUpdate);
			// Fix up each file
			foreach (string file in xmls) {
				var srcmodifiedDate = File.GetLastWriteTimeUtc (file);
				if (srcmodifiedDate <= lastUpdate) {
					Log.LogDebugMessage ("  Skipping: {0}  {1} <= {2}", file, srcmodifiedDate, lastUpdate);
					continue;
				}
				Log.LogDebugMessage ("  Processing: {0}   {1} > {2}", file, srcmodifiedDate, lastUpdate);
				var tmpdest = Path.GetTempFileName ();
				File.Copy (file, tmpdest, overwrite: true);
				MonoAndroidHelper.SetWriteable (tmpdest);
				try {
					bool success = AndroidResource.UpdateXmlResource (resdir, tmpdest, acwMap,
						ResourceDirectories.Where (s => s != item).Select(s => s.ItemSpec), (t, m) => {
							string targetfile = file;
							if (targetfile.StartsWith (resdir, StringComparison.InvariantCultureIgnoreCase)) {
								targetfile = file.Substring (resdir.Length).TrimStart (Path.DirectorySeparatorChar);
								if (resource_name_case_map.TryGetValue (targetfile, out string temp))
									targetfile = temp;
								targetfile = Path.Combine ("Resources", targetfile);
							}
							switch (t) {
								case TraceLevel.Error:
									Log.LogCodedError ("XA1002", file: targetfile, lineNumber: 0, message: m);
									break;
								case TraceLevel.Warning:
									Log.LogCodedWarning ("XA1001", file: targetfile, lineNumber: 0, message: m);
									break;
								default:
									Log.LogDebugMessage (m);
									break;
							}
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
					MonoAndroidHelper.CleanBOM (tmpdest);

					if (MonoAndroidHelper.CopyIfChanged (tmpdest, file)) {
						MonoAndroidHelper.SetWriteable (file);
						MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (file, srcmodifiedDate, Log);
					}
				} finally {
					File.Delete (tmpdest);
				}
			}
		}
	}
}
