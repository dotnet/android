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
			File.WriteAllText (BuildFilePath, build_gradle_kts_content);
			foreach (var module in Modules) {
				module.Create ();
				File.AppendAllText (settingsFile, $"{Environment.NewLine}include(\":{module.Name}\")");
			}
			File.AppendAllText (Path.Combine (ProjectDirectory, "gradle.properties"), "android.useAndroidX=true");
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
