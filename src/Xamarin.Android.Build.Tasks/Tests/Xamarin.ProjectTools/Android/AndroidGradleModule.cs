using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public class AndroidGradleModule
	{
		public string Name { get; set; }

		public string PackageName = "com.example.gradletest";

		public string ModuleDirectory { get; private set; } = string.Empty;

		public string CompileSdkValue { get; set; } = GetDefaultCompileSdkLine ();

		public int MinSdk { get; set; } = XABuildConfig.AndroidMinimumDotNetApiLevel.Major;

		/// <summary>
		/// Returns the compileSdk Gradle DSL line for the default API level.
		/// Starting with API 37, Google ships platforms as "android-37.0" instead of "android-37",
		/// so Gradle needs compileSdkPreview = "android-37.0" instead of compileSdk = 37.
		/// </summary>
		public static string GetDefaultCompileSdkLine ()
		{
			return GetCompileSdkGradleLine (XABuildConfig.AndroidDefaultTargetDotnetApiLevel);
		}

		/// <summary>
		/// Returns the compileSdk Gradle DSL line for a given API level Version.
		/// Uses compileSdkPreview when the Version has a non-zero minor (e.g. 36.1)
		/// or when Major >= 37 (Google ships android-37.0 not android-37).
		/// </summary>
		public static string GetCompileSdkGradleLine (Version apiLevel)
		{
			// Non-zero minor versions always need the string form (e.g. "android-36.1")
			if (apiLevel.Minor != 0) {
				return $@"compileSdkPreview = ""android-{apiLevel}""";
			}
			// API 37+ ship as android-37.0, android-38.0 etc. — Gradle needs the string form
			if (apiLevel.Major >= 37) {
				return $@"compileSdkPreview = ""android-{apiLevel}""";
			}
			return $"compileSdk = {apiLevel.Major}";
		}

		/// <summary>
		/// Returns the compileSdk Gradle DSL line for a given integer API level.
		/// </summary>
		public static string GetCompileSdkGradleLine (int apiLevel)
		{
			return GetCompileSdkGradleLine (new Version (apiLevel, 0));
		}

		public bool IsApplication { get; set; } = false;

		public string AndroidManifestContent { get; set; } = string.Empty;

		public string BuildGradleFileContent { get; set; } = string.Empty;

		public List<AndroidItem.AndroidJavaSource> JavaSources { get; set; } = new List<AndroidItem.AndroidJavaSource> ();

		public AndroidGradleModule (string directory)
		{
			ModuleDirectory = directory;
			Name = Path.GetFileName (ModuleDirectory);
		}

		public void WriteAndroidManifest ()
		{	
			var manifestDirectory = Path.Combine (ModuleDirectory, "src", "main");
			Directory.CreateDirectory (manifestDirectory);

			var manifest = $@"
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
	{AndroidManifestContent}
</manifest>
";
			File.WriteAllText (Path.Combine (manifestDirectory, "AndroidManifest.xml"), manifest);
		}

		public void WriteJavaSource (AndroidItem.AndroidJavaSource javaSource)
		{
			var srcPathParts = new[] { ModuleDirectory, "src", "main", "java" };
			var javaDirectory = Path.Combine (srcPathParts.Concat (PackageName.Split ('.')).ToArray ());
			Directory.CreateDirectory (javaDirectory);
			File.WriteAllText (Path.Combine (javaDirectory, javaSource.Include ()), javaSource.TextContent ());
		}

		public void WriteGradleBuildFile ()
		{	
			var pluginId = IsApplication ? "com.android.application" : "com.android.library";
			var buildGradleContent = $@"
plugins {{
    id(""{pluginId}"")
}}
android {{
    namespace = ""com.example.{Name}""
    {CompileSdkValue}
    defaultConfig {{
        minSdk = {MinSdk}
    }}
    lint {{
        checkReleaseBuilds = false
    }}
}}
dependencies {{
    implementation(""androidx.appcompat:appcompat:1.6.1"")
    implementation(""com.google.android.material:material:1.11.0"")
}}
";
			File.WriteAllText (Path.Combine (ModuleDirectory, "build.gradle.kts"), string.IsNullOrEmpty (BuildGradleFileContent) ? buildGradleContent : BuildGradleFileContent);
		}

		public void Create ()
		{
			Directory.CreateDirectory (ModuleDirectory);
			if (IsApplication) {
				SetupDefaultApp ();
			} else {
				SetupDefaultLibrary ();
			}
			WriteGradleBuildFile ();
			WriteAndroidManifest ();
			foreach (var javaSource in JavaSources) {
				WriteJavaSource (javaSource);
			}
		}

		public void SetupDefaultApp ()
		{
			JavaSources.Add (new AndroidItem.AndroidJavaSource ("MainActivity.java") {
				TextContent = () => $@"
package {PackageName};
import android.os.Bundle;
import android.widget.TextView;
import androidx.appcompat.app.AppCompatActivity;
public class MainActivity extends AppCompatActivity {{
    @Override
    protected void onCreate(Bundle savedInstanceState) {{
        super.onCreate(savedInstanceState);
        TextView tv = new TextView(this);
        tv.setText(""Hello world!"");
        setContentView(tv);
    }}
}}
",
			});
			AndroidManifestContent = $@"
  <application android:label=""App"">
    <activity android:name="".TestActivity"" android:exported=""true"">
      <intent-filter>
        <action android:name=""android.intent.action.MAIN"" />
        <category android:name=""android.intent.category.LAUNCHER"" />
      </intent-filter>
    </activity>
  </application>
";
		}


		public void SetupDefaultLibrary ()
		{
			JavaSources.Add (new AndroidItem.AndroidJavaSource ($"{Name}Class.java") {
				TextContent = () => $@"
package {PackageName};
public class {Name}Class {{
    public static String getString(String myString) {{
        return myString + "" from java!"";
    }}
}}
",
			});
		}

	}
}
