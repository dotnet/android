using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CalculateAdditionalResourceCacheDirectories : AndroidTask
	{
		public override string TaskPrefix => "CAR";

		[Required]
		public string[] AdditionalAndroidResourcePaths { get; set; }

		[Required]
		public string CacheDirectory { get; set; }

		[Output]
		public ITaskItem[] AdditionalResourceCachePaths { get; set; }

		public override bool RunTask ()
		{
			if (!AdditionalAndroidResourcePaths.Any ())
				return true;

			Directory.CreateDirectory (CacheDirectory);
			var directories = new List<ITaskItem> ();

			foreach (var path in AdditionalAndroidResourcePaths) {
				var targetDir = Path.Combine (CacheDirectory, Files.HashString (path));
				directories.Add (new TaskItem (Path.GetFullPath (targetDir).TrimEnd (Path.DirectorySeparatorChar)));
			}
			AdditionalResourceCachePaths = directories.ToArray ();

			Log.LogDebugTaskItems ("  [Output] AdditionalResourceCachePaths:", AdditionalResourceCachePaths);
			return !Log.HasLoggedErrors;
		}
	}
}

