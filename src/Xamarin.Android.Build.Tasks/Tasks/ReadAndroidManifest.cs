#nullable enable
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Since GenerateJavaStubs can get skipped on incremental builds, this task parses the merged AndroidManifest.xml for values needed later in the build.
	/// </summary>
	public class ReadAndroidManifest : AndroidTask
	{
		public override string TaskPrefix => "RAM";

		[Required]
		public string ManifestFile { get; set; } = "";

		public string? AndroidSdkDirectory { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; } = "";

		public string []? ReferenceAssemblyPaths { get; set; }

		public bool ReadSdkVersionsOnly { get; set; }

		[Output]
		public string CompileSdkVersion { get; set; } = "";

		[Output]
		public int MinimumSdkVersion { get; set; }

		[Output]
		public int TargetSdkVersion { get; set; }

		/// <summary>
		/// True if //manifest/application/[@android:extractNativeLibs='false']. False otherwise.
		/// </summary>
		[Output]
		public bool EmbeddedDSOsEnabled { get; set; }

		[Output]
		public ITaskItem []? UsesLibraries { get; set; }

		[Output]
		public bool UseEmbeddedDex { get; set; } = false;

		[Output]
		public bool IsTestOnly { get; set; } = false;

		[Output]
		public string? PackageName { get; set; }

		[Output]
		public string? ApplicationLabel { get; set; }

		[Output]
		public string? VersionName { get; set; }

		[Output]
		public string? VersionCode { get; set; }

		public override bool RunTask ()
		{
			var androidNs = AndroidAppManifest.AndroidXNamespace;
			var document = XDocument.Load (ManifestFile);
			var usesSdk = document.Root?.Element ("uses-sdk");
			int? compileSdk = MonoAndroidHelper.TryParseApiLevel (AndroidApiLevel, out var version) ? version.Major : null;
			CompileSdkVersion = compileSdk?.ToString () ?? "";
			var minSdkValue = usesSdk?.Attribute (androidNs + "minSdkVersion")?.Value
				?? XABuildConfig.AndroidMinimumDotNetApiLevel.Major.ToString ();
			MinimumSdkVersion = int.TryParse (minSdkValue, out var minSdk) ? minSdk : 1;
			TargetSdkVersion = int.TryParse (usesSdk?.Attribute (androidNs + "targetSdkVersion")?.Value, out var targetSdk)
				? targetSdk : compileSdk ?? 21;

			if (ReadSdkVersionsOnly) {
				return true;
			}

			if (AndroidSdkDirectory.IsNullOrEmpty ()) {
				throw new System.ArgumentException ("An Android SDK directory is required to read application attributes.", nameof (AndroidSdkDirectory));
			}
			var supportedVersions = MonoAndroidHelper.GetSupportedVersions (BuildEngine4, ReferenceAssemblyPaths);
			var manifest = AndroidAppManifest.Load (document, supportedVersions);
			var root = manifest.Document.Element ("manifest");
			var app = root?.Element ("application");

			PackageName = root?.Attribute ("package")?.Value;
			VersionName = root?.Attribute (androidNs + "versionName")?.Value;
			VersionCode = root?.Attribute (androidNs + "versionCode")?.Value;
			ApplicationLabel = app?.Attribute (androidNs + "label")?.Value;

			if (app != null) {
				string? text = app.Attribute (androidNs + "extractNativeLibs")?.Value;
				if (bool.TryParse (text, out bool value)) {
					EmbeddedDSOsEnabled = !value;
				}

				text = app.Attribute (androidNs + "useEmbeddedDex")?.Value;
				if (bool.TryParse (text, out value)) {
					UseEmbeddedDex = value;
				}

				text = app.Attribute (androidNs + "testOnly")?.Value;
				if (bool.TryParse (text, out value)) {
					IsTestOnly = value;
				}

				var libraries = new List<ITaskItem> ();
				var platformPath = AndroidSdkInfo.TryGetPlatformDirectoryFromApiLevel (AndroidSdkDirectory, AndroidApiLevel, supportedVersions);
				if (platformPath == null) {
					Log.LogDebugMessage ($"Could not find platform directory for API level '{AndroidApiLevel}'");
				}
				foreach (var uses_library in app.Elements ("uses-library")) {
					var attribute = uses_library.Attribute (androidNs + "name");
					if (attribute != null && !attribute.Value.IsNullOrEmpty ()) {
						if (platformPath == null)
							continue;
						var required = uses_library.Attribute (androidNs + "required")?.Value;
						var path = Path.Combine (platformPath, "optional", $"{attribute.Value}.jar");
						if (File.Exists (path)) {
							libraries.Add (new TaskItem (path));
						} else {
							if (!bool.TryParse (required, out bool isRequired))
								isRequired = true;
							if (isRequired)
								Log.LogWarningForXmlNode ("XA4218", ManifestFile, attribute, Properties.Resources.XA4218, path);
						}
					}
				}
				UsesLibraries = libraries.ToArray ();
			}

			return !Log.HasLoggedErrors;
		}
	}
}
