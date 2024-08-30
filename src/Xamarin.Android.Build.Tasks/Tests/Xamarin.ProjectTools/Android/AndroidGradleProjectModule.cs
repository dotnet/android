using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public abstract class AndroidGradleModule
	{
		public string Name { get; set; }

		public string ModuleDirectory { get; private set; } = string.Empty;

		public int CompileSdk { get; set; } = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;

		public int MinSdk { get; set; } = 21;

		public List<AndroidItem.AndroidJavaSource> JavaSources { get; set; } = new List<AndroidItem.AndroidJavaSource> ();

		public AndroidGradleModule (string directory)
		{
			ModuleDirectory = directory;
			Name = Path.GetFileName (ModuleDirectory);
		}

		public virtual void Create ()
		{
			if (Directory.Exists (ModuleDirectory))
				Directory.Delete (ModuleDirectory, true);
			Directory.CreateDirectory (ModuleDirectory);

			CreateAndroidManifest ();

			foreach (var javaSource in JavaSources) {
				CreateJavaSource (javaSource);
			}
		}

		public virtual void CreateAndroidManifest (string innerManifestContent = "")
		{	
			var manifestDirectory = Path.Combine (ModuleDirectory, "src", "main");
			Directory.CreateDirectory (manifestDirectory);

			var manifest = $@"
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
	{innerManifestContent}
</manifest>
";
			File.WriteAllText (Path.Combine (manifestDirectory, "AndroidManifest.xml"), manifest);
		}

		void CreateJavaSource (AndroidItem.AndroidJavaSource javaSource)
		{
			var javaDirectory = Path.Combine (ModuleDirectory, "src", "main", "java", "com", "example");
			Directory.CreateDirectory (javaDirectory);
			File.WriteAllText (Path.Combine (javaDirectory, javaSource.Include ()), javaSource.TextContent ());
		}
	}

	public class AndroidGradleAppModule : AndroidGradleModule
	{
		public AndroidGradleAppModule (string directory)
			: base (directory)
		{
		}

		public override void Create ()
		{
			base.Create ();
		}
	}


	public class AndroidGradleLibraryModule : AndroidGradleModule
	{
		public AndroidGradleLibraryModule (string directory)
			: base (directory)
		{
		}

		public override void Create ()
		{
			base.Create ();
			File.WriteAllText (Path.Combine (ModuleDirectory, "build.gradle.kts"), build_gradle_kts_content);
		}

		string build_gradle_kts_content =>
$@"
plugins {{
    id(""com.android.library"")
}}
android {{
    namespace = ""com.example.{Name}""
    compileSdk = {CompileSdk}
    defaultConfig {{
        minSdk = {MinSdk}
    }}
}}
";

	}
}
