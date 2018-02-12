using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CalculateProjectDependencies : Task
	{
		const int DefaultMinSDKVersion = 11;

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public ITaskItem ManifestFile { get; set; }

		[Required]
		public string BuildToolsVersion { get; set; }

		public string PlatformToolsVersion { get; set; }

		public string ToolsVersion { get; set; }

		public string NdkVersion { get; set; }

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

		public override bool Execute ()
		{
			var dependencies = new List<ITaskItem> ();
			var targetApiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			var manifestApiLevel = DefaultMinSDKVersion;
			if (File.Exists (ManifestFile.ItemSpec)) {
				var manifest = AndroidAppManifest.Load (ManifestFile.ItemSpec, MonoAndroidHelper.SupportedVersions);
				manifestApiLevel = manifest.TargetSdkVersion ?? manifest.MinSdkVersion ?? DefaultMinSDKVersion;
			}
			var sdkVersion = Math.Max (targetApiLevel.Value, manifestApiLevel);
			dependencies.Add (CreateAndroidDependency ($"platforms;android-{sdkVersion}", $"android-{sdkVersion}"));
			dependencies.Add (CreateAndroidDependency ($"build-tools;{BuildToolsVersion}", BuildToolsVersion));
			if (!string.IsNullOrEmpty (PlatformToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ("platform-tools", PlatformToolsVersion));
			}
			if (!string.IsNullOrEmpty (ToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ("tools", ToolsVersion));
			}
			if (!string.IsNullOrEmpty (NdkVersion)) {
				dependencies.Add (CreateAndroidDependency ("ndk-bundle", NdkVersion));
			}
			Dependencies = dependencies.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
