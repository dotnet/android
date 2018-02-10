using System;
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
			var manifest = AndroidAppManifest.Load (ManifestFile.ItemSpec, MonoAndroidHelper.SupportedVersions);
			var manifestApiLevel = manifest.TargetSdkVersion ?? manifest.MinSdkVersion ?? DefaultMinSDKVersion;
			dependencies.Add (CreateAndroidDependency ("platform", $"{Math.Max (targetApiLevel.Value, manifestApiLevel)}"));
			dependencies.Add (CreateAndroidDependency ("build-tool", BuildToolsVersion));
			if (!string.IsNullOrEmpty (PlatformToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ("platform-tool", PlatformToolsVersion));
			}
			if (!string.IsNullOrEmpty (ToolsVersion)) {
				dependencies.Add (CreateAndroidDependency ("tool", ToolsVersion));
			}
			Dependencies = dependencies.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
