﻿using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CalculateProjectDependencies : AndroidTask
	{
		public override string TaskPrefix => "CPD";

		const int DefaultMinSDKVersion = 11;

		public string CommandLineToolsVersion { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public ITaskItem ManifestFile { get; set; }

		[Required]
		public string BuildToolsVersion { get; set; }

		public string PlatformToolsVersion { get; set; }

		public string ToolsVersion { get; set; }

		public string NdkVersion { get; set; }

		public bool NdkRequired { get; set; }

		[Output]
		public ITaskItem [] Dependencies { get; set; }

		ITaskItem CreateAndroidDependency (string include, string version)
		{
			if (string.IsNullOrEmpty (version))
				return new TaskItem (include);

			return new TaskItem (include, new Dictionary<string, string> {
				{ "Version", version }
			});
		}

		public override bool RunTask ()
		{
			var dependencies = new List<ITaskItem> ();
			var targetApiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			var manifestApiLevel = DefaultMinSDKVersion;
			if (File.Exists (ManifestFile.ItemSpec)) {
				var manifest = AndroidAppManifest.Load (ManifestFile.ItemSpec, MonoAndroidHelper.SupportedVersions);
				manifestApiLevel = manifest.TargetSdkVersion ?? manifest.MinSdkVersion ?? DefaultMinSDKVersion;
			}
			var sdkVersion = Math.Max (targetApiLevel.Value, manifestApiLevel);
			dependencies.Add (CreateAndroidDependency ($"platforms/android-{sdkVersion}", $""));
			dependencies.Add (CreateAndroidDependency ($"build-tools/{BuildToolsVersion}", BuildToolsVersion));
			if (!string.IsNullOrEmpty (PlatformToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ("platform-tools", PlatformToolsVersion));
			}
			if (!string.IsNullOrEmpty (CommandLineToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ($"cmdline-tools/{CommandLineToolsVersion}", CommandLineToolsVersion));
			}
			if (!string.IsNullOrEmpty (ToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ("tools", ToolsVersion));
			}
			if (!string.IsNullOrEmpty (NdkVersion) && NdkRequired) {
				dependencies.Add (CreateAndroidDependency ("ndk-bundle", NdkVersion));
			}
			Dependencies = dependencies.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
