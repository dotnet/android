// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;
using ThreadingTasks = System.Threading.Tasks;

namespace Xamarin.Android.Tasks
{
	public class ConvertResourcesCases : AsyncTask
	{
		[Required]
		public ITaskItem[] ResourceDirectories { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		[Required]
		public string CustomViewMapFile { get; set; }

		public string AndroidConversionFlagFile { get; set; }

		public string ResourceNameCaseMap { get; set; }

		Dictionary<string,string> resource_name_case_map;
		ConcurrentDictionary<string, HashSet<string>> customViewMap;
		Dictionary<string, string> acw_map = new Dictionary<string, string> ();

		public override bool Execute ()
		{
			resource_name_case_map = MonoAndroidHelper.LoadResourceCaseMap (ResourceNameCaseMap);
			acw_map = MonoAndroidHelper.LoadAcwMapFile (AcwMapFile);


			if (CustomViewMapFile != null)
				customViewMap = Xamarin.Android.Tasks.MonoAndroidHelper.LoadCustomViewMapFile (BuildEngine4, CustomViewMapFile);

			Yield();
			try
			{
				// Look in the resource xml's for capitalized stuff and fix them
				var task = ThreadingTasks.Task.Run(() =>
				{
					DoExecute();
				}, Token);

				task.ContinueWith(Complete).ConfigureAwait(false);

				base.Execute();
			}
			finally
			{
				Reacquire();
			}

			if (customViewMap != null)
				Xamarin.Android.Tasks.MonoAndroidHelper.SaveCustomViewMapFile (BuildEngine4, CustomViewMapFile, customViewMap);

			return true;
		}

		void DoExecute ()
		{
			ThreadingTasks.ParallelOptions options = new ThreadingTasks.ParallelOptions {
				CancellationToken = Token,
				TaskScheduler = ThreadingTasks.TaskScheduler.Default,
			};

			ThreadingTasks.Parallel.ForEach (ResourceDirectories, options, FixupResources);
		}

		void FixupResources (ITaskItem item)
		{
			var resdir = item.ItemSpec;
			if (!Path.IsPathRooted (resdir))
			{
				resdir = Path.Combine(WorkingDirectory, resdir);
			}
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
			LogDebugMessage ("  AndroidConversionFlagFile modified: {0}", lastUpdate);
			
			var resourcedirectories = ResourceDirectories.Where (s => s != item).Select(s => s.ItemSpec).ToArray();
			// Fix up each file
			foreach (string file in xmls) {
				var srcmodifiedDate = File.GetLastWriteTimeUtc (file);
				if (srcmodifiedDate <= lastUpdate) {
					LogDebugMessage ("  Skipping: {0}  {1} <= {2}", file, srcmodifiedDate, lastUpdate);
					continue;
				}
				LogDebugMessage ("  Processing: {0}   {1} > {2}", file, srcmodifiedDate, lastUpdate);
				MonoAndroidHelper.SetWriteable (file);
				bool success = AndroidResource.UpdateXmlResource (resdir, file, acw_map,
					resourcedirectories, (level, message) => {
						switch (level) {
						case TraceLevel.Error:
							FixupResourceFilenameAndLogCodedError ("XA1002", message, file, resdir, resource_name_case_map);
							break;
						case TraceLevel.Warning:
							FixupResourceFilenameAndLogCodedWarning ("XA1001", message, file, resdir, resource_name_case_map);
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
						customViewMap.TryAdd (e, set = new HashSet<string> ());
					set.Add (file);
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
				MonoAndroidHelper.CleanBOM (file);
			}
		}
	}
}
