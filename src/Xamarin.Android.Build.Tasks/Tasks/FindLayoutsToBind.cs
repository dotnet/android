using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class FindLayoutsToBind : Task
	{
		static readonly string DirSeparator = Path.DirectorySeparatorChar == '\\' ? @"\\" : "/";
		static readonly Regex layoutPathRegex = new Regex ($".*{DirSeparator}+layout(-?\\w+)*{DirSeparator}+.*\\.a?xml$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public bool GenerateLayoutBindings { get; set; }

		public string BindingDependenciesCacheFile { get; set; }

		public ITaskItem[] BoundLayouts { get; set; }

		[Required]
		public ITaskItem[] ResourceFiles { get; set; }

		[Output]
		public ITaskItem[] LayoutsToBind { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("FindLayoutsToBind Task");
			Log.LogDebugMessage ($"  GenerateLayoutBindings: {GenerateLayoutBindings}");
			Log.LogDebugMessage ($"  BindingDependenciesCacheFile: {BindingDependenciesCacheFile}");
			Log.LogDebugTaskItems ("  BoundLayouts:", BoundLayouts);
			Log.LogDebugTaskItems ("  ResourceFiles:", ResourceFiles);

			var layouts = new Dictionary <string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
			if (GenerateLayoutBindings) {
				Log.LogDebugMessage ("Collecting all layouts");
				foreach (ITaskItem item in ResourceFiles) {
					AddLayoutFile (item.ItemSpec, layouts);
				}
			}

			if (BoundLayouts != null && BoundLayouts.Length > 0) {
				Log.LogDebugMessage ("Collecting bound layouts");
				foreach (ITaskItem item in BoundLayouts) {
					if (layouts.ContainsKey (item.ItemSpec))
						continue;

					// We need the whole item because of the possible metadata it may contain
					layouts.Add (item.ItemSpec, item);
				}
			}

			if (!String.IsNullOrEmpty (BindingDependenciesCacheFile) && File.Exists (BindingDependenciesCacheFile)) {
				Log.LogDebugMessage ("Collecting cached dependencies");
				foreach (string line in File.ReadAllLines (BindingDependenciesCacheFile)) {
					AddLayoutFile (line, layouts);
				}
			}
			LayoutsToBind = layouts.Values.ToArray ();
			if (layouts.Count == 0)
				Log.LogDebugMessage ("No layout file qualifies for binding code generation");
			Log.LogDebugTaskItems ("   LayoutsToBind:", LayoutsToBind, true);
			return !Log.HasLoggedErrors;
		}

		void AddLayoutFile (string filePath, Dictionary <string, ITaskItem> layouts)
		{
			if (String.IsNullOrEmpty (filePath) || !File.Exists (filePath))
				return;

			if (layouts.ContainsKey (filePath) || !layoutPathRegex.IsMatch (filePath))
				return;

			layouts.Add (filePath, new TaskItem (filePath));
		}
	}
}
