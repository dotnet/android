// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class GetMonoPlatformJar : Task
	{
		[Required]
		public string TargetFrameworkDirectory { get; set; }

		[Output]
		public string MonoPlatformJarPath { get; set; }

		[Output]
		public string MonoPlatformDexPath { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GetMonoPlatformJar Task");
			Log.LogDebugMessage ("  TargetFrameworkDirectory: {0}", TargetFrameworkDirectory);
			
			var directories = TargetFrameworkDirectory.Split (new char[] { ';', ','} ,StringSplitOptions.RemoveEmptyEntries);

			foreach (var dir in directories)
				if (File.Exists (Path.Combine (dir, "mono.android.jar"))) {
					MonoPlatformJarPath = Path.Combine (dir, "mono.android.jar");
					MonoPlatformDexPath = Path.ChangeExtension (MonoPlatformJarPath, ".dex");
					Log.LogMessage (MessageImportance.Low, "  MonoPlatformJarPath: {0}", MonoPlatformJarPath);
					Log.LogMessage (MessageImportance.Low, "  MonoPlatformDexPath: {0}", MonoPlatformDexPath);

					return true;
				}

			Log.LogError ("Could not find mono.android.jar");

			return false;
		}
	}
}
