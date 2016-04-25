// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
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

		public override bool Execute ()
		{
			Log.LogDebugMessage ("ConvertResourcesCases Task");
			Log.LogDebugMessage ("  ResourceDirectories: {0}", ResourceDirectories);
			Log.LogDebugMessage ("  AcwMapFile: {0}", AcwMapFile);

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

			// Fix up each file
			foreach (string file in xmls) {
				Log.LogMessage (MessageImportance.High, "  Processing: {0}", file);
				var srcmodifiedDate = File.GetLastWriteTimeUtc (file);
				var tmpdest = file + ".tmp";
				MonoAndroidHelper.CopyIfChanged (file, tmpdest);
				MonoAndroidHelper.SetWriteable (tmpdest);
				try {
					AndroidResource.UpdateXmlResource (tmpdest, acwMap,
						ResourceDirectories.Where (s => s != item).Select(s => s.ItemSpec));

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
