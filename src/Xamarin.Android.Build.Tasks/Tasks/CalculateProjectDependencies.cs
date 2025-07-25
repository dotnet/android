#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CalculateProjectDependencies : AndroidTask
	{
		public override string TaskPrefix => "CPD";

		const int DefaultMinSDKVersion = 11;

		public string? CommandLineToolsVersion { get; set; }

		public string? AndroidApiLevel { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; } = "";

		[Required]
		public ITaskItem ManifestFile { get; set; } = null!;

		[Required]
		public string BuildToolsVersion { get; set; } = "";

		public string? PlatformToolsVersion { get; set; }

		public string? NdkVersion { get; set; }

		public bool NdkRequired { get; set; }

		public string? JdkVersion { get; set; }

		public bool GetJavaDependencies { get; set; } = false;

		[Output]
		public ITaskItem []? Dependencies { get; set; }

		[Output]
		public ITaskItem[]? JavaDependencies { get; set; }

		ITaskItem CreateAndroidDependency (string include, string version)
		{
			if (version.IsNullOrEmpty ())
				return new TaskItem (include);

			return new TaskItem (include, new Dictionary<string, string> {
				{ "Version", version }
			});
		}

		public override bool RunTask ()
		{
			var dependencies = new List<ITaskItem> ();
			var javaDependencies = new List<ITaskItem> ();
			var targetApiLevel = AndroidApiLevel.IsNullOrEmpty () ?
				MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion) :
				MonoAndroidHelper.SupportedVersions.GetApiLevelFromId (AndroidApiLevel);
			var manifestApiLevel = DefaultMinSDKVersion;
			if (File.Exists (ManifestFile.ItemSpec)) {
				var manifest = AndroidAppManifest.Load (ManifestFile.ItemSpec, MonoAndroidHelper.SupportedVersions);
				manifestApiLevel = manifest.TargetSdkVersion ?? manifest.MinSdkVersion ?? DefaultMinSDKVersion;
			}
			var sdkVersion = Math.Max (targetApiLevel ?? DefaultMinSDKVersion, manifestApiLevel);
			dependencies.Add (CreateAndroidDependency ($"platforms/android-{sdkVersion}", $""));
			dependencies.Add (CreateAndroidDependency ($"build-tools/{BuildToolsVersion}", BuildToolsVersion));
			if (!PlatformToolsVersion.IsNullOrEmpty ()) {
				dependencies.Add (CreateAndroidDependency ("platform-tools", PlatformToolsVersion));
			}
			if (!CommandLineToolsVersion.IsNullOrEmpty ()) {
				dependencies.Add (CreateAndroidDependency ($"cmdline-tools/{CommandLineToolsVersion}", CommandLineToolsVersion));
			}
			if (!NdkVersion.IsNullOrEmpty () && NdkRequired) {
				dependencies.Add (CreateAndroidDependency ("ndk-bundle", NdkVersion));
			}
			if (!JdkVersion.IsNullOrEmpty () && GetJavaDependencies) {
				javaDependencies.Add (CreateAndroidDependency ("jdk", JdkVersion));
			}
			Dependencies = dependencies.ToArray ();
			JavaDependencies = javaDependencies.ToArray ();
			return !Log.HasLoggedErrors;
		}
	}
}
