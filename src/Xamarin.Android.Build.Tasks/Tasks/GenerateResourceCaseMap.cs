// Copyright (C) 2021 Microsoft, Inc. All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateResourceCaseMap : AndroidTask
	{
		public override string TaskPrefix => "GRCM";
		public ITaskItem[] Resources { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }

		[Required]
		public string ProjectDir { get; set; }

		public ITaskItem[] AdditionalResourceDirectories { get; set; }

		[Required]
		public ITaskItem OutputFile { get; set; }

		private Dictionary<string, string> resource_fixup = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		public override bool RunTask ()
		{
			// ResourceDirectory may be a relative path, and
			// we need to compare it to absolute paths
			ResourceDirectory = Path.GetFullPath (ResourceDirectory);

			// Create our capitalization maps so we can support mixed case resources
			foreach (var item in Resources) {
				var path = Path.GetFullPath (item.ItemSpec);
				if (!path.StartsWith (ResourceDirectory, StringComparison.OrdinalIgnoreCase)) {
					Log.LogDebugMessage ($"Skipping {item}. Path is not include the '{ResourceDirectory}'");
					continue;
				}

				var name = path.Substring (ResourceDirectory.Length).TrimStart ('/', '\\');
				var logical_name = item.GetMetadata ("LogicalName").Replace ('\\', '/');
				if (string.IsNullOrEmpty (logical_name))
					logical_name = Path.GetFileName (path);

				AddRename (name.Replace ('/', Path.DirectorySeparatorChar), logical_name.Replace ('/', Path.DirectorySeparatorChar));
			}
			foreach (var additionalDir in AdditionalResourceDirectories ?? Array.Empty<ITaskItem>()) {
				var dir = Path.Combine (ProjectDir, Path.GetDirectoryName (additionalDir.ItemSpec));
				var file = Path.Combine (dir, "__res_name_case_map.txt");
				if (!File.Exists (file)) {
					// .NET 6 .aar files place the file in a sub-directory
					file = Path.Combine (dir, ".net", "__res_name_case_map.txt");
					if (!File.Exists (file))
						continue;
				}
				foreach (var line in File.ReadLines (file)) {
					if (string.IsNullOrEmpty (line))
						continue;
					string [] tok = line.Split (';');
					AddRename (tok [1].Replace ('/', Path.DirectorySeparatorChar), tok [0].Replace ('/', Path.DirectorySeparatorChar));
				}
			}

			if (MonoAndroidHelper.SaveMapFile (BuildEngine4, Path.GetFullPath (OutputFile.ItemSpec), resource_fixup)) {
				Log.LogDebugMessage ($"Writing to: {OutputFile.ItemSpec}");
			} else {
				Log.LogDebugMessage ($"Up to date: {OutputFile.ItemSpec}");
			}

			return !Log.HasLoggedErrors;
		}

		private void AddRename (string android, string user)
		{
			var from = android;
			var to = user;

			if (from.Contains ('.'))
				from = from.Substring (0, from.LastIndexOf ('.'));
			if (to.Contains ('.'))
				to = to.Substring (0, to.LastIndexOf ('.'));

			from = NormalizeAlternative (from);
			to = NormalizeAlternative (to);

			string curTo;

			if (resource_fixup.TryGetValue (from, out curTo)) {
				if (string.Compare (to, curTo, StringComparison.OrdinalIgnoreCase) != 0) {
					var ext = Path.GetExtension (android);
					var dir = Path.GetDirectoryName (user);

					Log.LogDebugMessage ("Resource target names differ; got '{0}', expected '{1}'.",
						Path.Combine (dir, Path.GetFileName (to) + ext),
						Path.Combine (dir, Path.GetFileName (curTo) + ext));
				}
				return;
			}
			Log.LogDebugMessage ($"Adding map from '{from}' to '{to}'.");
			resource_fixup.Add (from, to);
		}

		static string NormalizeAlternative (string value)
		{
			int s = value.IndexOfAny (new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

			if (s < 0)
				return value;

			int a = value.IndexOf ('-');

			return
				ResourceParser.GetNestedTypeName (value.Substring (0, (a < 0 || a >= s) ? s : a)).ToLowerInvariant () +
				value.Substring (s);
		}
	}
}
