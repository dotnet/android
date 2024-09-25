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

		public int CompileSdk { get; set; } = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;

		public int MinSdk { get; set; } = 21;

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
    compileSdk = {CompileSdk}
    defaultConfig {{
        minSdk = {MinSdk}
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
    <activity android:name="".TestActivity"">
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
