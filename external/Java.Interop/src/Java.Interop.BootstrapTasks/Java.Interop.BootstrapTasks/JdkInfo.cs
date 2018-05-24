using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Java.Interop.BootstrapTasks
{
	public class JdkInfo : Task
	{
		const string JARSIGNER = "jarsigner.exe";
		const string MDREG_KEY = @"SOFTWARE\Novell\Mono for Android";
		const string MDREG_JAVA_SDK = "JavaSdkDirectory";

		public  string  JdksRoot              { get; set; }

		public  string  MaximumJdkVersion     { get; set; }

		static  Regex   VersionExtractor  = new Regex (@"(?<version>[\d]+(\.\d+)+)", RegexOptions.Compiled);

		[Required]
		public  ITaskItem       PropertyFile        { get; set; }

		[Required]
		public  ITaskItem       MakeFragmentFile    { get; set; }

		[Output]
		public  string          JavaHomePath        { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (JdkInfo)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (JdksRoot)}: {JdksRoot}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (MakeFragmentFile)}: {MakeFragmentFile}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (MaximumJdkVersion)}: {MaximumJdkVersion}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (PropertyFile)}: {PropertyFile}");

			var maxVersion  = GetMaxJdkVersion ();
			var java_home   = GetJavaHomePathFromEnvironment ();
			if (java_home != null) {
				var java_home_v = GetVersionFromPath (java_home);
				if (maxVersion != null && java_home_v != null && java_home_v > maxVersion) {
					Log.LogMessage (MessageImportance.Low, $"  Skipping JAVA_HOME default value of `{java_home}` as it exceeds MaximumJdkVersion={MaximumJdkVersion}.");
					java_home = null;
				}
				if (java_home != null && !Directory.Exists (Path.Combine (java_home, "include"))) {
					Log.LogMessage (MessageImportance.Low, $"  Skipping JAVA_HOME default value of `{java_home}` as it does not contain an `include` subdirectory.");
					java_home = null;
				}
			}
			java_home = java_home ?? GetJavaHomePathFromMachine (maxVersion);

			if (string.IsNullOrEmpty (java_home)) {
				Log.LogError ("Could not determine JAVA_HOME location. Please set JdksRoot or export the JAVA_HOME environment variable.");
				return false;
			}

			var includes    = new List<string> () {
				Path.Combine (java_home, "include"),
			};
			includes.AddRange (Directory.GetDirectories (includes [0]));

			var jarPath     = FindExecutablesInDirectory (Path.Combine (java_home, "bin"), "jar").First ();
			var javacPath   = FindExecutablesInDirectory (Path.Combine (java_home, "bin"), "javac").First ();
			var jdkJvmPaths = OS.IsMacOS
				? FindLibrariesInDirectory (java_home, "jli")
				: FindLibrariesInDirectory (Path.Combine (java_home, "jre"), "jvm");
			var jdkJvmPath  = jdkJvmPaths.First ();

			FileExists (jarPath);
			FileExists (javacPath);
			FileExists (jdkJvmPath);
			if (Log.HasLoggedErrors) {
				return false;
			}

			JavaHomePath  = java_home;

			Directory.CreateDirectory (Path.GetDirectoryName (PropertyFile.ItemSpec));
			Directory.CreateDirectory (Path.GetDirectoryName (MakeFragmentFile.ItemSpec));

			WritePropertyFile (jarPath, javacPath, jdkJvmPath, includes);
			WriteMakeFragmentFile (jarPath, javacPath, jdkJvmPath, includes);

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (JavaHomePath)}: {JavaHomePath}");

			return !Log.HasLoggedErrors;
		}

		Version GetMaxJdkVersion ()
		{
			if (string.IsNullOrEmpty (MaximumJdkVersion))
				return null;
			if (!MaximumJdkVersion.Contains (".")) {
				MaximumJdkVersion += ".0";
			}
			return new Version (MaximumJdkVersion);
		}

		Version GetVersionFromPath (string path)
		{
			var m = VersionExtractor.Match (path);
			if (!m.Success)
				return null;
			Version v;
			if (!Version.TryParse (m.Groups ["version"].Value, out v)) {
				return null;
			}
			return v;
		}

		void FileExists (string path)
		{
			if (!File.Exists (path)) {
				var name = Path.GetFileName (path);
				Log.LogError ($"Could not determine location of `{name}`; tried `{path}`.");
			}
		}

		void WritePropertyFile (string jarPath, string javacPath, string jdkJvmPath, IEnumerable<string> includes)
		{
			var msbuild = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");
			var project = new XElement (msbuild + "Project",
				new XElement (msbuild + "Choose",
					new XElement (msbuild + "When", new XAttribute ("Condition", " '$(JdkJvmPath)' == '' "),
						new XElement (msbuild + "PropertyGroup",
							new XElement (msbuild + "JdkJvmPath", jdkJvmPath)),
						new XElement (msbuild + "ItemGroup",
							includes.Select (i => new XElement (msbuild + "JdkIncludePath", new XAttribute ("Include", i)))))),
				new XElement (msbuild + "PropertyGroup",
					new XElement (msbuild + "JavaCPath", new XAttribute ("Condition", " '$(JavaCPath)' == '' "),
						javacPath),
					new XElement (msbuild + "JarPath", new XAttribute ("Condition", " '$(JarPath)' == '' "),
						jarPath)));
			project.Save (PropertyFile.ItemSpec);
		}

		void WriteMakeFragmentFile (string jarPath, string javacPath, string jdkJvmPath, IEnumerable<string> includes)
		{
			using (var o = new StreamWriter (MakeFragmentFile.ItemSpec)) {
				o.WriteLine ($"JI_JAR_PATH          := {jarPath}");
				o.WriteLine ($"JI_JAVAC_PATH        := {javacPath}");
				o.WriteLine ($"JI_JDK_INCLUDE_PATHS := {string.Join (" ", includes)}");
				o.WriteLine ($"JI_JVM_PATH          := {jdkJvmPath}");
			}
		}

		string GetJavaHomePathFromEnvironment ()
		{
			var java_home = Environment.GetEnvironmentVariable ("JAVA_HOME");
			if (!string.IsNullOrEmpty (java_home))
				return java_home;
			return null;
		}

		string GetJavaHomePathFromMachine (Version maxVersion)
		{
			var java_homes  = GetJavaHomePathsFromDirectory (JdksRoot)
				.Concat (GetJavaHomePathsFromJava ())
				.Concat (GetJavaHomePathsFromWindowsRegistry ())
				.Distinct ()
				.Where (d => Directory.Exists (d))
				.Select (jh => new {
					Path    = jh,
					Version = GetVersionFromPath (jh),
				})
				.Where (v => maxVersion == null ? true : v.Version <= maxVersion)
				.OrderByDescending (v => v.Version)
				.Select (v => v.Path)
				.ToList ();

			foreach (var p in java_homes) {
				Log.LogMessage (MessageImportance.Low, $"  Possible JAVA_HOME location: {p}");
			}

			return java_homes.FirstOrDefault ();
		}

		IEnumerable<string> GetJavaHomePathsFromDirectory (string jdksRoot)
		{
			if (string.IsNullOrEmpty (jdksRoot))
				yield break;
			if (!Directory.Exists (jdksRoot))
				yield break;
			foreach (var d in Directory.EnumerateDirectories (jdksRoot)) {
				var h = d;
				if (OS.IsMacOS)
					h = Path.Combine (h, "Contents", "Home");
				yield return h;
			}
		}

		IEnumerable<string> GetJavaHomePathsFromJava ()
		{
			var javas = Environment.GetEnvironmentVariable ("PATH").Split (Path.PathSeparator)
				.SelectMany (p => FindExecutablesInDirectory (p, "java"));

			foreach (var exe in javas) {
				const string JavaHome = "java.home = ";
				string java_home = null;
				Exec (exe, "-XshowSettings:properties -version", (o, e) => {
						int i = e.Data?.IndexOf (JavaHome) ?? -1;
						if (i < 0)
							return;
						Log.LogMessage (MessageImportance.Low, $"    {e.Data}");
						java_home = e.Data.Substring (JavaHome.Length + i);
						// `java -XshowSettings:properties -version | grep java.home` ends with `/jre` on macOS.
						// We need the parent dir so we can properly lookup the `include` directories
						if (java_home.EndsWith ("jre", StringComparison.OrdinalIgnoreCase)) {
							java_home = Path.GetDirectoryName (java_home);
						}
				});
				if (string.IsNullOrEmpty (java_home))
					continue;
				yield return java_home;
			}
		}

		void Exec (string java, string arguments, DataReceivedEventHandler output)
		{
			Log.LogMessage (MessageImportance.Low, $"  Tool {java} execution started with arguments: {arguments}");
			var psi = new ProcessStartInfo () {
				FileName                = java,
				Arguments               = arguments,
				UseShellExecute         = false,
				RedirectStandardInput   = false,
				RedirectStandardOutput  = true,
				RedirectStandardError   = true,
				CreateNoWindow          = true,
				WindowStyle             = ProcessWindowStyle.Hidden,
			};
			var p = new Process () {
				StartInfo   = psi,
			};
			p.OutputDataReceived    += output;
			p.ErrorDataReceived     += output;

			using (p) {
				p.StartInfo = psi;
				p.Start ();
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();
				p.WaitForExit ();
			}
		}

		IEnumerable<string> GetJavaHomePathsFromWindowsRegistry ()
		{
			if (Path.DirectorySeparatorChar == '/')
				yield break;

			// check the user specified path
			var roots = new [] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			const RegistryEx.Wow64 wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			foreach (var root in roots) {
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_JAVA_SDK, wow, "bin", JARSIGNER))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_JAVA_SDK, wow);
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
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64);

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64, "bin", JARSIGNER))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64);

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64, "bin", JARSIGNER))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64);
				}

				Log.LogMessage (MessageImportance.Low, $"  Key {key_name} not found.");
			}
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

			if (!FindExecutablesInDirectory (Path.Combine (path, subdir), exe).Any ()) {
				Log.LogMessage (MessageImportance.Low, $"  Key {key_name} found:\n    Path does not contain {exe} in \\{subdir} ({path}).");
				return false;
			}

			Log.LogMessage (MessageImportance.Low, $"  Key {key_name} found:\n    Path contains {exe} in \\{subdir} ({path}).");

			return true;
		}

		IEnumerable<string> FindExecutablesInDirectory (string dir, string executable)
		{
			foreach (var exe in Executables (executable)) {
				var p = Path.Combine (dir, exe);
				if (File.Exists (p))
					yield return p;
			}
		}

		IEnumerable<string> Executables (string executable)
		{
			var pathExt = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

			if (pathExts == null) {
				yield return executable;
				yield break;
			}

			foreach (var ext in pathExts)
				yield return Path.ChangeExtension (executable, ext);
		}

		IEnumerable<string> FindLibrariesInDirectory (string dir, string libraryName)
		{
			var library = string.Format (OS.NativeLibraryFormat, libraryName);
			return Directory.EnumerateFiles (dir, library, SearchOption.AllDirectories);
		}
	}
}
