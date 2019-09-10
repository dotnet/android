﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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

			var md5 = MD5.Create ();
			Directory.CreateDirectory (CacheDirectory);
			var directories = new List<ITaskItem> ();

			foreach (var path in AdditionalAndroidResourcePaths) {
				var cacheSubDirectory = string.Concat (md5.ComputeHash (
									Encoding.UTF8.GetBytes (path)).Select (b => b.ToString ("X02"))
								);
				var targetDir = Path.Combine (CacheDirectory, cacheSubDirectory);
				directories.Add (new TaskItem (Path.GetFullPath (targetDir).TrimEnd (Path.DirectorySeparatorChar)));
			}
			AdditionalResourceCachePaths = directories.ToArray ();

			Log.LogDebugTaskItems ("  [Output] AdditionalResourceCachePaths:", AdditionalResourceCachePaths);
			return !Log.HasLoggedErrors;
		}
	}
}

