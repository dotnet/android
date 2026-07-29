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

		public AndroidGradleProject (string directory)
		{
			ProjectDirectory = directory;
		}

		public void Create ()
		{
			Directory.CreateDirectory (ProjectDirectory);
			CopyGradleWrapper ();
			var settingsFile = Path.Combine (ProjectDirectory, "settings.gradle.kts");
			File.WriteAllText (settingsFile, GetSettingsGradleKtsContent ());
			File.WriteAllText (BuildFilePath, build_gradle_kts_content);
			foreach (var module in Modules) {
				module.Create ();
				File.AppendAllText (settingsFile, $"{Environment.NewLine}include(\":{module.Name}\")");
			}
			File.WriteAllText (Path.Combine (ProjectDirectory, "gradle.properties"), """
# Exercise Gradle configuration-cache compatibility.
org.gradle.configuration-cache=true
# Build independent modules concurrently.
org.gradle.parallel=true
# Reuse task outputs across test builds.
org.gradle.caching=true
# Required by the AndroidX dependencies used by generated modules.
android.useAndroidX=true
""");
		}

		/// <summary>
		/// Copies the repository wrapper so generated projects do not depend on <c>gradle init</c> or a CI distribution download.
		/// </summary>
		void CopyGradleWrapper ()
		{
			var sourceDirectory = Path.Combine (XABuildPaths.TopDirectory, "build-tools", "gradle");
			var destinationWrapperDirectory = Path.Combine (ProjectDirectory, "gradle", "wrapper");
			Directory.CreateDirectory (destinationWrapperDirectory);

			CopyFile ("gradlew", ProjectDirectory);
			CopyFile ("gradlew.bat", ProjectDirectory);
			CopyFile (Path.Combine ("gradle", "wrapper", "gradle-wrapper.jar"), destinationWrapperDirectory);
			CopyFile (Path.Combine ("gradle", "wrapper", "gradle-wrapper.properties"), destinationWrapperDirectory);

			void CopyFile (string relativePath, string destinationDirectory)
			{
				var source = Path.Combine (sourceDirectory, relativePath);
				var destination = Path.Combine (destinationDirectory, Path.GetFileName (relativePath));
				File.Copy (source, destination, overwrite: true);
				if (!TestEnvironment.IsWindows) {
					File.SetUnixFileMode (destination, File.GetUnixFileMode (source));
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

		const string build_gradle_kts_content =
@"
plugins {
    id(""com.android.application"") version ""8.5.0"" apply false
    id(""com.android.library"") version ""8.5.0"" apply false
}
";
		string GetSettingsGradleKtsContent ()
		{
			var gradleConfigurationDirectory = Path.Combine (XABuildPaths.TopDirectory, "eng", "gradle").Replace ('\\', '/');

			return $$"""
// See: eng/gradle/plugin-repositories.gradle, eng/gradle/dependency-repositories.gradle
pluginManagement {
    apply(from = "{{gradleConfigurationDirectory}}/plugin-repositories.gradle", to = this)
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    apply(from = "{{gradleConfigurationDirectory}}/dependency-repositories.gradle", to = this)
}
""";
		}

	}
}
