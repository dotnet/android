using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Java.Interop.BootstrapTasks
{
	public class JdkInfo : Task
	{
		const string JARSIGNER = "jarsigner.exe";
		const string MDREG_KEY = @"SOFTWARE\Novell\Mono for Android";
		const string MDREG_JAVA_SDK = "JavaSdkDirectory";

		[Required]
		public ITaskItem Output { get; set; }

		[Output]
		public string JavaSdkDirectory { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (JdkInfo)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Output)}: {Output}");

			var javaSdkPath = GetJavaSdkPath ();
			if (string.IsNullOrEmpty(javaSdkPath)) {
				Log.LogError ("JavaSdkPath is blank");
				return false;
			}

			Log.LogMessage (MessageImportance.Low, $"  JavaSdkPath: {javaSdkPath}");

			var jvmPath = Path.Combine (javaSdkPath, "jre", "bin", "server", "jvm.dll");
			if (!File.Exists (jvmPath)) {
				Log.LogError ($"JdkJvmPath not found at {jvmPath}");
				return false;
			}

			var javaIncludePath = Path.Combine (javaSdkPath, "include");
			var includes = new List<string> { javaIncludePath };
			includes.AddRange (Directory.GetDirectories (javaIncludePath)); //Include dirs such as "win32"

			var includeXmlTags = new StringBuilder ();
			foreach (var include in includes) {
				includeXmlTags.AppendLine ($"<JdkIncludePath Include=\"{include}\" />");
			}

			Directory.CreateDirectory (Path.GetDirectoryName (Output.ItemSpec));
			File.WriteAllText (Output.ItemSpec, $@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Choose>
    <When Condition="" '$(JdkJvmPath)' == '' "">
      <PropertyGroup>
        <JdkJvmPath>{jvmPath}</JdkJvmPath>
      </PropertyGroup>
      <ItemGroup>
        {includeXmlTags}
      </ItemGroup>
    </When>
  </Choose>
  <PropertyGroup>
    <JavaCPath Condition="" '$(JavaCPath)' == '' "">{Path.Combine (javaSdkPath, "bin", "javac.exe")}</JavaCPath>
    <JarPath Condition="" '$(JarPath)' == '' "">{Path.Combine (javaSdkPath, "bin", "jar.exe")}</JarPath>
  </PropertyGroup>
</Project>");

			JavaSdkDirectory = javaSdkPath;
			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (JavaSdkDirectory)}: {JavaSdkDirectory}");

			return !Log.HasLoggedErrors;
		}

		string GetJavaSdkPath ()
		{
			// check the user specified path
			var roots = new [] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			const RegistryEx.Wow64 wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			foreach (var root in roots) {
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_JAVA_SDK, wow, "bin", JARSIGNER))
					return RegistryEx.GetValueString (root, regKey, MDREG_JAVA_SDK, wow);
			}

			string subkey = @"SOFTWARE\JavaSoft\Java Development Kit";

			Log.LogMessage (MessageImportance.Low, "Looking for Java 6 SDK...");

			foreach (var wow64 in new [] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 }) {
				string key_name = string.Format (@"{0}\{1}\{2}", "HKLM", subkey, "CurrentVersion");
				var currentVersion = RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey, "CurrentVersion", wow64);

				if (!string.IsNullOrEmpty (currentVersion)) {
					Log.LogMessage (MessageImportance.Low, $"  Key {key_name} found.");

					// No matter what the CurrentVersion is, look for 1.6 or 1.7 or 1.8
					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64, "bin", JARSIGNER))
						return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64);

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64, "bin", JARSIGNER))
						return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64);

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64, "bin", JARSIGNER))
						return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64);
				}

				Log.LogMessage (MessageImportance.Low, $"  Key {key_name} not found.");
			}

			// We ran out of things to check..
			return null;
		}

		string GetMDRegistryKey ()
		{
			var regKey = Environment.GetEnvironmentVariable ("XAMARIN_ANDROID_REGKEY");
			return string.IsNullOrWhiteSpace (regKey) ? MDREG_KEY : regKey;
		}

		private bool CheckRegistryKeyForExecutable (UIntPtr key, string subkey, string valueName, RegistryEx.Wow64 wow64, string subdir, string exe)
		{
			string key_name = string.Format (@"{0}\{1}\{2}", key == RegistryEx.CurrentUser ? "HKCU" : "HKLM", subkey, valueName);

			var value = RegistryEx.GetValueString (key, subkey, valueName, wow64);
			var path = string.IsNullOrEmpty (value) ? null : value;

			if (path == null) {
				Log.LogMessage (MessageImportance.Low, $"  Key {key_name} not found.");
				return false;
			}

			if (!FindExecutableInDirectory (exe, Path.Combine (path, subdir)).Any ()) {
				Log.LogMessage (MessageImportance.Low, $"  Key {key_name} found:\n    Path does not contain {exe} in \\{subdir} ({path}).");
				return false;
			}

			Log.LogMessage (MessageImportance.Low, $"  Key {key_name} found:\n    Path contains {exe} in \\{subdir} ({path}).");

			return true;
		}

		IEnumerable<string> FindExecutableInDirectory (string executable, string dir)
		{
			foreach (var exe in Executables (executable))
				if (File.Exists (Path.Combine (dir, exe)))
					yield return dir;
		}

		IEnumerable<string> Executables (string executable)
		{
			yield return executable;
			var pathExt = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

			if (pathExts == null)
				yield break;

			foreach (var ext in pathExts)
				yield return Path.ChangeExtension (executable, ext);
		}
	}
}
