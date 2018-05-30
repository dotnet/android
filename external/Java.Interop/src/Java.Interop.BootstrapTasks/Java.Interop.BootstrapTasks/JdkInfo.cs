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

			var maxVersion      = GetVersion (MaximumJdkVersion);

			string jarPath      = null;
			string javacPath    = null;
			string jdkJvmPath   = null;
			string includePath  = null;

			var java_home   = GetJavaHomePathFromEnvironment ();
			if (!ValidateJdkPath (maxVersion, java_home, out jarPath, out javacPath, out jdkJvmPath, out includePath)) {
				java_home = null;
			}

			if (java_home == null &&
					(java_home = GetJavaHomePathFromLibexec ()) != null &&
					!ValidateJdkPath (maxVersion, java_home, out jarPath, out javacPath, out jdkJvmPath, out includePath)) {
				java_home = null;
			}

			if (java_home == null &&
					(java_home = GetJavaHomePathFromAlternatives ()) != null &&
					!ValidateJdkPath (maxVersion, java_home, out jarPath, out javacPath, out jdkJvmPath, out includePath)) {
				java_home = null;
			}

			if (java_home == null &&
					(java_home = GetJavaHomePathFromMachine (maxVersion)) != null &&
					!ValidateJdkPath (maxVersion, java_home, out jarPath, out javacPath, out jdkJvmPath, out includePath)) {
				java_home = null;
			}

			if (java_home == null) {
				Log.LogError ("Could not determine JAVA_HOME location. Please set JdksRoot or export the JAVA_HOME environment variable.");
				return false;
			}

			var includes    = new List<string> () {
				includePath,
			};
			includes.AddRange (Directory.GetDirectories (includePath));

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

		Version GetVersion (string value)
		{
			if (string.IsNullOrEmpty (value))
				return null;
			if (!value.Contains (".")) {
				value += ".0";
			}
			Version v;
			if (Version.TryParse (value, out v))
				return v;
			return null;
		}

		Version GetVersionFromPath (string path)
		{
			var m = VersionExtractor.Match (path);
			if (!m.Success)
				return null;
			return GetVersion (m.Groups ["version"].Value);
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

		bool ValidateJdkPath (Version maxVersion, string java_home)
		{
			return ValidateJdkPath (maxVersion, java_home,
					out _, out _, out _, out _);
		}

		bool ValidateJdkPath (Version maxVersion, string java_home,
				out string jarPath, out string javacPath, out string jdkJvmPath, out string includePath)
		{
			jarPath = javacPath = jdkJvmPath = includePath = null;

			if (string.IsNullOrEmpty (java_home) || !Directory.Exists (java_home))
				return false;

			var pathVersion = GetVersionFromPath (java_home);
			if (maxVersion != null && pathVersion != null && pathVersion > maxVersion) {
				Log.LogMessage (MessageImportance.Low,
						$"  Skipping JAVA_HOME value of `{java_home}` as it exceeds MaximumJdkVersion={MaximumJdkVersion}.");
				return false;
			}

			jarPath         = FindExecutablesInDirectory (Path.Combine (java_home, "bin"), "jar").FirstOrDefault ();
			javacPath       = FindExecutablesInDirectory (Path.Combine (java_home, "bin"), "javac").FirstOrDefault ();
			var jdkJvmPaths = OS.IsMacOS
				? FindLibrariesInDirectory (java_home, "jli")
				: FindLibrariesInDirectory (Path.Combine (java_home, "jre"), "jvm");
			jdkJvmPath      = jdkJvmPaths.FirstOrDefault ();
			includePath     = Path.Combine (java_home, "include");

			if (jarPath == null) {
				Log.LogMessage (MessageImportance.Low, $"  Skipping JAVA_HOME value of `{java_home}` as `jar` could not be found.");
				return false;
			}
			if (javacPath == null) {
				Log.LogMessage (MessageImportance.Low, $"  Skipping JAVA_HOME value of `{java_home}` as `javac` could not be found.");
				return false;
			}
			if (jdkJvmPath == null) {
				var jvm = OS.IsMacOS ? "libjli.dylib" : string.Format (OS.NativeLibraryFormat, "jvm");
				Log.LogMessage (MessageImportance.Low, $"  Skipping JAVA_HOME value of `{java_home}` as `{jvm} could not be found.");
				return false;
			}
			if (!Directory.Exists (includePath)) {
				Log.LogMessage (MessageImportance.Low, $"  Skipping JAVA_HOME value of `{java_home}` as the `include` directory could not be found.");
				return false;
			}

			return true;
		}

		string GetJavaHomePathFromEnvironment ()
		{
			var java_home = Environment.GetEnvironmentVariable ("JAVA_HOME");
			if (!string.IsNullOrEmpty (java_home))
				return java_home;
			return null;
		}

		// macOS
		string GetJavaHomePathFromLibexec ()
		{
			var java_home	= Path.GetFullPath ("/usr/libexec/java_home");
			if (!File.Exists (java_home)) {
				return null;
			}
			string path = null;
			Exec (java_home, "", (o, e) => {
					if (string.IsNullOrEmpty (e.Data))
						return;
					Log.LogMessage (MessageImportance.Low, $"    {e.Data}");
					path = e.Data;
			});
			return path;
		}

		// Linux
		string GetJavaHomePathFromAlternatives ()
		{
			var alternatives  = Path.GetFullPath ("/etc/alternatives/java");
			if (!File.Exists (alternatives))
				return null;
			string targetJava = null;
			Exec ("readlink", $"\"{alternatives}\"", (o, e) => {
					if (string.IsNullOrEmpty (e.Data))
						return;
					Log.LogMessage (MessageImportance.Low, $"    {e.Data}");
					targetJava = e.Data;
			});
			if (string.IsNullOrEmpty (targetJava))
				return null;
			return GetJavaHomePathFromJava (targetJava);
		}

		string GetJavaHomePathFromMachine (Version maxVersion)
		{
			var java_homes  = GetJavaHomePathsFromDirectory (JdksRoot)
				.Concat (GetJavaHomePathsFromJava ())
				.Concat (GetJavaHomePathsFromWindowsRegistry ())
				.Where (d => Directory.Exists (d))
				.Distinct ()
				.ToList ();

			foreach (var p in java_homes) {
				Log.LogMessage (MessageImportance.Low, $"  Possible JAVA_HOME location: {p}");
			}

			var versionComparer = new ComparisonComparer<JdkComparisonInfo>((x, y) => {
					int r = 0;
					if (x.Version != null && y.Version != null)
						r = x.Version.CompareTo (y.Version);
					return r;
			});

			java_homes  = java_homes.Where (d => ValidateJdkPath (maxVersion, d))
				.Select (jh => new JdkComparisonInfo {
					Path    = jh,
					Version = GetVersionFromPath (jh),
				})
				.Where (v => (maxVersion == null || v.Version == null) ? true : v.Version <= maxVersion)
				.OrderByDescending (v => v, versionComparer)
				.ThenByDescending (v => Directory.GetLastWriteTimeUtc (v.Path))
				.Select (v => v.Path)
				.ToList ();

			foreach (var p in java_homes) {
				Log.LogMessage (MessageImportance.Low, $"  Filtered JAVA_HOME location: {p}");
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
				var java_home = GetJavaHomePathFromJava (exe);
				if (string.IsNullOrEmpty (java_home))
					continue;
				yield return java_home;
			}
		}

		string GetJavaHomePathFromJava (string java)
		{
			const string JavaHome = "java.home = ";
			string java_home = null;
			Exec (java, "-XshowSettings:properties -version", (o, e) => {
					int i = e.Data?.IndexOf (JavaHome) ?? -1;
					if (i < 0)
						return;
					Log.LogMessage (MessageImportance.Low, $"    {e.Data}");
					java_home = e.Data.Substring (JavaHome.Length + i);
					// `java -XshowSettings:properties -version 2>&1 | grep java.home` ends with `/jre` on macOS.
					// We need the parent dir so we can properly lookup the `include` directories
					if (java_home.EndsWith ("jre", StringComparison.OrdinalIgnoreCase)) {
						java_home = Path.GetDirectoryName (java_home);
					}
			});
			return java_home;
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

		class JdkComparisonInfo {
			public  string    Path;
			public  Version   Version;
		}
	}

	class ComparisonComparer<T> : IComparer<T> {

		Comparison<T> comparison;

		public ComparisonComparer (Comparison<T> comparison)
		{
			this.comparison = comparison;
		}

		public int Compare (T x, T y)
		{
			return comparison (x, y);
		}
	}
}
