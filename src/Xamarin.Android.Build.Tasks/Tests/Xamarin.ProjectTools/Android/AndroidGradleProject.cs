using System;
using System.Collections.Generic;
using System.IO;


namespace Xamarin.ProjectTools
{
	public class AndroidGradleProject
	{
		public string ProjectDirectory { get; private set; } = string.Empty;

		public List<AndroidGradleModule> Modules { get; set; } = new List<AndroidGradleModule> ();

		public string BuildFilePath => Path.Combine (ProjectDirectory, "build.gradle.kts");

		/// <summary>
		/// Android Gradle Plugin version (e.g., "8.5.0", "9.0.0")
		/// </summary>
		public string AgpVersion { get; set; } = "8.5.0";

		/// <summary>
		/// Gradle wrapper version to use (e.g., "8.12", "9.0"). If null, uses system default.
		/// </summary>
		public string? GradleVersion { get; set; }

		GradleCLI gradleCLI = new GradleCLI ();

		public AndroidGradleProject (string directory)
		{
			ProjectDirectory = directory;
		}

		public void Create ()
		{
			Directory.CreateDirectory (ProjectDirectory);
			gradleCLI.Init (ProjectDirectory);
			var settingsFile = Path.Combine (ProjectDirectory, "settings.gradle.kts");
			File.WriteAllText (settingsFile, settings_gradle_kts_content);
			File.WriteAllText (BuildFilePath, GetBuildGradleKtsContent ());
			foreach (var module in Modules) {
				module.Create ();
				File.AppendAllText (settingsFile, $"{Environment.NewLine}include(\":{module.Name}\")");
			}
			File.AppendAllText (Path.Combine (ProjectDirectory, "gradle.properties"), "android.useAndroidX=true");

			// Update Gradle wrapper version if specified
			if (!string.IsNullOrEmpty (GradleVersion)) {
				var wrapperPropertiesPath = Path.Combine (ProjectDirectory, "gradle", "wrapper", "gradle-wrapper.properties");
				if (File.Exists (wrapperPropertiesPath)) {
					var content = File.ReadAllText (wrapperPropertiesPath);
					// Replace the distribution URL with the specified Gradle version
					content = System.Text.RegularExpressions.Regex.Replace (
						content,
						@"distributionUrl=.*",
						$@"distributionUrl=https\://services.gradle.org/distributions/gradle-{GradleVersion}-bin.zip"
					);
					File.WriteAllText (wrapperPropertiesPath, content);
				}
			}
		}

		public static AndroidGradleProject CreateDefault (string projectDir, bool isApplication = false)
		{
			var proj = new AndroidGradleProject (projectDir) {
				Modules = {
					new AndroidGradleModule (Path.Combine (projectDir, "TestModule")) {
						IsApplication = isApplication,
					},
				},
			};
			proj.Create ();
			return proj;
		}

		/// <summary>
		/// Creates a default Gradle project with specified AGP and Gradle versions.
		/// </summary>
		public static AndroidGradleProject CreateDefault (string projectDir, string agpVersion, string? gradleVersion, bool isApplication = false)
		{
			var proj = new AndroidGradleProject (projectDir) {
				AgpVersion = agpVersion,
				GradleVersion = gradleVersion,
				Modules = {
					new AndroidGradleModule (Path.Combine (projectDir, "TestModule")) {
						IsApplication = isApplication,
					},
				},
			};
			proj.Create ();
			return proj;
		}

		string GetBuildGradleKtsContent () =>
$@"
plugins {{
    id(""com.android.application"") version ""{AgpVersion}"" apply false
    id(""com.android.library"") version ""{AgpVersion}"" apply false
}}
";
		const string settings_gradle_kts_content =
@"
pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}
";

	}
}
