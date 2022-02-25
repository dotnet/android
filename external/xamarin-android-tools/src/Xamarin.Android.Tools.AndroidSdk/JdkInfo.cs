using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

using Xamarin.Android.Tools.AndroidSdk.Properties;

namespace Xamarin.Android.Tools
{
	public class JdkInfo {

		public      string                              HomePath                    {get;}

		public      string?                             Locator                     {get;}

		public      string                              JarPath                     {get;}
		public      string                              JavaPath                    {get;}
		public      string                              JavacPath                   {get;}
		public      string                              JdkJvmPath                  {get;}
		public      ReadOnlyCollection<string>          IncludePath                 {get;}

		public      Version?                            Version                     => javaVersion.Value;
		public      string?                             Vendor                      {
			get {
				if (GetJavaSettingsPropertyValue ("java.vendor", out string? vendor))
					return vendor;
				return null;
			}
		}

		public      ReadOnlyDictionary<string, string>  ReleaseProperties           {get;}
		public      IEnumerable<string>                 JavaSettingsPropertyKeys    => javaProperties.Value.Keys;

		Lazy<Dictionary<string, List<string>>>      javaProperties;
		Lazy<Version?>                              javaVersion;

		Action<TraceLevel, string>                  logger;

		public JdkInfo (string homePath)
			: this (homePath, null, null)
		{
		}

		public JdkInfo (string homePath, string? locator = null, Action<TraceLevel, string>? logger = null)
		{
			if (homePath == null)
				throw new ArgumentNullException (nameof (homePath));
			if (!Directory.Exists (homePath))
				throw new ArgumentException ("Not a directory", nameof (homePath));

			HomePath            = homePath;
			Locator             = locator;
			this.logger         = logger ?? AndroidSdkInfo.DefaultConsoleLogger;

			var binPath         = Path.Combine (HomePath, "bin");
			JarPath             = ProcessUtils.FindExecutablesInDirectory (binPath, "jar").FirstOrDefault ();
			JavaPath            = ProcessUtils.FindExecutablesInDirectory (binPath, "java").FirstOrDefault ();
			JavacPath           = ProcessUtils.FindExecutablesInDirectory (binPath, "javac").FirstOrDefault ();

			string? jdkJvmPath  = GetJdkJvmPath ();

			ValidateFile ("jar",    JarPath);
			ValidateFile ("java",   JavaPath);
			ValidateFile ("javac",  JavacPath);
			ValidateFile ("jvm",    jdkJvmPath);

			JdkJvmPath          = jdkJvmPath!;

			var includes        = new List<string> ();
			var jdkInclude      = Path.Combine (HomePath, "include");

			if (Directory.Exists (jdkInclude)) {
				includes.Add (jdkInclude);
				includes.AddRange (Directory.GetDirectories (jdkInclude));
			}


			ReleaseProperties   = GetReleaseProperties();

			IncludePath         = new ReadOnlyCollection<string> (includes);

			javaProperties      = new Lazy<Dictionary<string, List<string>>> (
				() => GetJavaProperties (this.logger),
				LazyThreadSafetyMode.ExecutionAndPublication);
			javaVersion         = new Lazy<Version?> (GetJavaVersion, LazyThreadSafetyMode.ExecutionAndPublication);
		}

		public JdkInfo (string homePath, string locator)
			: this (homePath, locator, null)
		{
		}

		public override string ToString()
		{
			return $"JdkInfo(Version={Version}, Vendor=\"{Vendor}\", HomePath=\"{HomePath}\", Locator=\"{Locator}\")";
		}

		public bool GetJavaSettingsPropertyValues (string key, [NotNullWhen (true)] out IEnumerable<string>? value)
		{
			value       = null;
			var props   = javaProperties.Value;
			if (props.TryGetValue (key, out var v)) {
				value = v;
				return true;
			}
			return false;
		}

		public bool GetJavaSettingsPropertyValue (string key, [NotNullWhen (true)] out string? value)
		{
			value       = null;
			var props   = javaProperties.Value;
			if (props.TryGetValue (key, out var v)) {
				if (v.Count > 1)
					throw new InvalidOperationException ($"Requested to get one string value when property `{key}` contains `{v.Count}` values.");
				value   = v [0];
				return true;
			}
			return false;
		}

		string? GetJdkJvmPath ()
		{
			string  jreDir  = Path.Combine (HomePath, "jre");
			string  libDir  = Path.Combine (HomePath, "lib");

			if (OS.IsMac) {
				return FindLibrariesInDirectory (jreDir, "jli").FirstOrDefault () ??
					FindLibrariesInDirectory (libDir, "jli").FirstOrDefault ();
			}
			if (OS.IsWindows) {
				string  binServerDir    = Path.Combine (HomePath, "bin", "server");
				return FindLibrariesInDirectory (jreDir, "jvm").FirstOrDefault () ??
					FindLibrariesInDirectory (binServerDir, "jvm").FirstOrDefault ();
			}
			return FindLibrariesInDirectory (jreDir, "jvm").FirstOrDefault () ??
				FindLibrariesInDirectory (libDir, "jvm").FirstOrDefault ();
		}

		static IEnumerable<string> FindLibrariesInDirectory (string dir, string libraryName)
		{
			if (!Directory.Exists (dir))
				return Enumerable.Empty<string> ();
			var library = string.Format (OS.NativeLibraryFormat, libraryName);
			return Directory.EnumerateFiles (dir, library, SearchOption.AllDirectories);
		}

		void ValidateFile (string name, string? path)
		{
			if (path == null || !File.Exists (path))
				throw new ArgumentException ($"Could not find required file `{name}` within `{HomePath}`; is this a valid JDK?", "homePath");
		}

		static  Regex   NonDigitMatcher     = new Regex (@"[^\d]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		Version? GetJavaVersion ()
		{
			string? version = null;
			if (ReleaseProperties.TryGetValue ("JAVA_VERSION", out version) && !string.IsNullOrEmpty (version)) {
				version = GetParsableVersion (version);
				if (ReleaseProperties.TryGetValue ("BUILD_NUMBER", out var build) && !string.IsNullOrEmpty (build))
					version += "." + build;
			}
			else if (GetJavaSettingsPropertyValue ("java.version", out version) && !string.IsNullOrEmpty (version)) {
				version = GetParsableVersion (version);
			}
			if (string.IsNullOrEmpty (version))
				throw new NotSupportedException ("Could not determine Java version.");
			var normalizedVersion   = NonDigitMatcher.Replace (version, ".");
			var versionParts        = normalizedVersion.Split (new[]{"."}, StringSplitOptions.RemoveEmptyEntries);

			try {
				if (versionParts.Length < 2)
					return null;
				if (versionParts.Length == 2)
					return new Version (major: int.Parse (versionParts [0]), minor: int.Parse (versionParts [1]));
				if (versionParts.Length == 3)
					return new Version (major: int.Parse (versionParts [0]), minor: int.Parse (versionParts [1]), build: int.Parse (versionParts [2]));
				// We just ignore elements 4+
				return new Version (major: int.Parse (versionParts [0]), minor: int.Parse (versionParts [1]), build: int.Parse (versionParts [2]), revision: int.Parse (versionParts [3]));
			}
			catch (Exception) {
				return null;
			}
		}

		static string GetParsableVersion (string version)
		{
			if (!version.Contains ("."))
				version += ".0";
			return version;
		}

		ReadOnlyDictionary<string, string> GetReleaseProperties ()
		{
			var props       = new Dictionary<string, string> ();
			var releasePath = Path.Combine (HomePath, "release");
			if (!File.Exists (releasePath))
				return new ReadOnlyDictionary<string, string>(props);

			using (var release = File.OpenText (releasePath)) {
				string? line;
				while ((line = release.ReadLine ()) != null) {
					line            = line.Trim ();
					const string PropertyDelim  = "=";
					int delim = line.IndexOf (PropertyDelim, StringComparison.Ordinal);
					if (delim < 0) {
						props [line] = "";
						continue;
					}
					string  key     = line.Substring (0, delim).Trim ();
					string  value   = line.Substring (delim + PropertyDelim.Length).Trim ();
					if (value.StartsWith ("\"", StringComparison.Ordinal) && value.EndsWith ("\"", StringComparison.Ordinal)) {
						value       = value.Substring (1, value.Length - 2);
					}
					props [key] = value;
				}
			}
			return new ReadOnlyDictionary<string, string>(props);
		}

		Dictionary<string, List<string>> GetJavaProperties (Action<TraceLevel, string> logger)
		{
			return GetJavaProperties (
					logger,
					ProcessUtils.FindExecutablesInDirectory (Path.Combine (HomePath, "bin"), "java").First ());
		}

		static bool AnySystemJavasInstalled ()
		{
			if (OS.IsMac) {
				string path = Path.Combine (Path.DirectorySeparatorChar + "System", "Library", "Java", "JavaVirtualMachines");
				if (!Directory.Exists (path)) {
					return false;
				}

				string[] dirs = Directory.GetDirectories (path);
				if (dirs == null || dirs.Length == 0) {
					return false;
				}
			}

			return true;
		}

		static Dictionary<string, List<string>> GetJavaProperties (Action<TraceLevel, string> logger, string java)
		{
			var javaProps   = new ProcessStartInfo {
				FileName    = java,
				Arguments   = "-XshowSettings:properties -version",
			};

			var     props   = new Dictionary<string, List<string>> ();
			string? curKey  = null;
			bool    foundPS = false;
			var     output  = new StringBuilder ();

			if (!AnySystemJavasInstalled () && (java == "/usr/bin/java" || java == "java"))
				return props;

			const string PropertySettings = "Property settings:";

			ProcessUtils.Exec (javaProps, (o, e) => {
					const string ContinuedValuePrefix   = "        ";
					const string NewValuePrefix         = "    ";
					const string NameValueDelim         = " = ";
					output.AppendLine (e.Data);
					if (string.IsNullOrEmpty (e.Data))
						return;
					if (e.Data.StartsWith (PropertySettings, StringComparison.Ordinal)) {
						foundPS = true;
						return;
					}
					if (!foundPS) {
						return;
					}
					if (e.Data.StartsWith (ContinuedValuePrefix, StringComparison.Ordinal)) {
						if (curKey == null) {
							logger (TraceLevel.Error, $"No Java property previously seen for continued value `{e.Data}`.");
							return;
						}
						props [curKey].Add (e.Data.Substring (ContinuedValuePrefix.Length));
						return;
					}
					if (e.Data.StartsWith (NewValuePrefix, StringComparison.Ordinal)) {
						var delim = e.Data.IndexOf (NameValueDelim, StringComparison.Ordinal);
						if (delim <= 0)
							return;
						curKey      = e.Data.Substring (NewValuePrefix.Length, delim - NewValuePrefix.Length);
						var value   = e.Data.Substring (delim + NameValueDelim.Length);
						List<string>? values;
						if (!props.TryGetValue (curKey!, out values))
							props.Add (curKey, values = new List<string> ());
						values.Add (value);
					}
			});
			if (!foundPS) {
				logger (TraceLevel.Warning, $"No Java properties found; did not find `{PropertySettings}` in `{java} -XshowSettings:properties -version` output: ```{output.ToString ()}```");
			}

			return props;
		}

		// Keep ordering in sync w/ GetSupportedJdkInfos
		public static IEnumerable<JdkInfo> GetKnownSystemJdkInfos (Action<TraceLevel, string>? logger = null)
		{
			logger  = logger ?? AndroidSdkInfo.DefaultConsoleLogger;

			return GetEnvironmentVariableJdks ("JI_JAVA_HOME", logger)
				.Concat (JdkLocations.GetPreferredJdks (logger))
				.Concat (XAPrepareJdkLocations.GetXAPrepareJdks (logger))
				.Concat (MicrosoftOpenJdkLocations.GetMicrosoftOpenJdks (logger))
				.Concat (EclipseAdoptiumJdkLocations.GetEclipseAdoptiumJdks (logger))
				.Concat (AzulJdkLocations.GetAzulJdks (logger))
				.Concat (OracleJdkLocations.GetOracleJdks (logger))
				.Concat (VSAndroidJdkLocations.GetVSAndroidJdks (logger))
				.Concat (MicrosoftDistJdkLocations.GetMicrosoftDistJdks (logger))
				.Concat (GetEnvironmentVariableJdks ("JAVA_HOME", logger))
				.Concat (GetPathEnvironmentJdks (logger))
				.Concat (GetLibexecJdks (logger))
				.Concat (GetJavaAlternativesJdks (logger))
				;
		}

		// Keep ordering in sync w/ GetKnownSystemJdkInfos
		public static IEnumerable<JdkInfo> GetSupportedJdkInfos (Action<TraceLevel, string>? logger = null)
		{
			logger = logger ?? AndroidSdkInfo.DefaultConsoleLogger;

			return MicrosoftOpenJdkLocations.GetMicrosoftOpenJdks (logger)
				.Concat (EclipseAdoptiumJdkLocations.GetEclipseAdoptiumJdks (logger))
				.Concat (MicrosoftDistJdkLocations.GetMicrosoftDistJdks (logger))
				;
		}

		internal static JdkInfo? TryGetJdkInfo (string path, Action<TraceLevel, string> logger, string locator)
		{
			JdkInfo? jdk = null;
			try {
				jdk = new JdkInfo (path, locator);
			}
			catch (Exception e) {
				logger (TraceLevel.Warning, string.Format (Resources.InvalidJdkDirectory_path_locator_message, path, locator, e.Message));
				logger (TraceLevel.Verbose, e.ToString ());
			}
			return jdk;
		}

		static IEnumerable<JdkInfo> GetEnvironmentVariableJdks (string envVar, Action<TraceLevel, string> logger)
		{
			var java_home = Environment.GetEnvironmentVariable (envVar);
			if (string.IsNullOrEmpty (java_home))
				yield break;
			var jdk = TryGetJdkInfo (java_home, logger, $"${envVar}");
			if (jdk != null)
				yield return jdk;
		}

		// macOS
		static IEnumerable<JdkInfo> GetLibexecJdks (Action<TraceLevel, string> logger)
		{
			return GetLibexecJdkPaths (logger)
				.Distinct ()
				.Select (p => TryGetJdkInfo (p, logger, "`/usr/libexec/java_home -X`"))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!)
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}

		static IEnumerable<string> GetLibexecJdkPaths (Action<TraceLevel, string> logger)
		{
			var java_home	= Path.GetFullPath ("/usr/libexec/java_home");
			if (!File.Exists (java_home)) {
				yield break;
			}
			var jhp = new ProcessStartInfo {
				FileName    = java_home,
				Arguments   = "-X",
			};
			var xml = new StringBuilder ();
			ProcessUtils.Exec (jhp, (o, e) => {
					if (string.IsNullOrEmpty (e.Data))
						return;
					xml.Append (e.Data);
			}, includeStderr: false);

			XElement plist;
			try {
				plist = XElement.Parse (xml.ToString ());
			} catch (XmlException e) {
				logger (TraceLevel.Warning, string.Format (Resources.InvalidXmlLibExecJdk_path_args_message, jhp.FileName, jhp.Arguments, e.Message));
				yield break;
			}
			foreach (var info in plist.Elements ("array").Elements ("dict")) {
				var JVMHomePath = (XNode) info.Elements ("key").FirstOrDefault (e => e.Value == "JVMHomePath");
				if (JVMHomePath == null)
					continue;
				while (JVMHomePath.NextNode.NodeType != XmlNodeType.Element)
					JVMHomePath = JVMHomePath.NextNode;
				var strElement  = (XElement) JVMHomePath.NextNode;
				var path        = strElement.Value;
				yield return path;
			}
		}

		// Linux; Ubuntu & Derivatives
		static IEnumerable<JdkInfo> GetJavaAlternativesJdks (Action<TraceLevel, string> logger)
		{
			return GetJavaAlternativesJdkPaths ()
				.Distinct ()
				.Select (p => TryGetJdkInfo (p, logger, "`/usr/sbin/update-java-alternatives -l`"))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!);
		}

		static IEnumerable<string> GetJavaAlternativesJdkPaths ()
		{
			var alternatives  = Path.GetFullPath ("/usr/sbin/update-java-alternatives");
			if (!File.Exists (alternatives))
				return Enumerable.Empty<string> ();

			var psi     = new ProcessStartInfo {
				FileName    = alternatives,
				Arguments   = "-l",
			};
			var paths   = new List<string> ();
			ProcessUtils.Exec (psi, (o, e) => {
					if (string.IsNullOrWhiteSpace (e.Data))
						return;
					// Example line:
					//  java-1.8.0-openjdk-amd64       1081       /usr/lib/jvm/java-1.8.0-openjdk-amd64
					var columns = e.Data.Split (new[]{ ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (columns.Length <= 2)
						return;
					paths.Add (columns [2]);
			});
			return paths;
		}

		// Linux; Fedora
		static IEnumerable<JdkInfo> GetLibJvmJdks (Action<TraceLevel, string> logger)
		{
			return GetLibJvmJdkPaths ()
				.Distinct ()
				.Select (p => TryGetJdkInfo (p, logger, "`ls /usr/lib/jvm/*`"))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!)
				.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
		}

		static IEnumerable<string> GetLibJvmJdkPaths ()
		{
			var jvm = "/usr/lib/jvm";
			if (!Directory.Exists (jvm))
				yield break;

			foreach (var jdk in Directory.EnumerateDirectories (jvm)) {
				var release = Path.Combine (jdk, "release");
				if (File.Exists (release))
					yield return jdk;
			}
		}

		// Last-ditch fallback!
		static IEnumerable<JdkInfo> GetPathEnvironmentJdks (Action<TraceLevel, string> logger)
		{
			return GetPathEnvironmentJdkPaths (logger)
				.Select (p => TryGetJdkInfo (p, logger, "$PATH"))
				.Where (jdk => jdk != null)
				.Select (jdk => jdk!);
		}

		static IEnumerable<string> GetPathEnvironmentJdkPaths (Action<TraceLevel, string> logger)
		{
			foreach (var java in ProcessUtils.FindExecutablesInPath ("java")) {
				var props   = GetJavaProperties (logger, java);
				if (props.TryGetValue ("java.home", out var java_homes)) {
					var java_home   = java_homes [0];
					// `java -XshowSettings:properties -version 2>&1 | grep java.home` ends with `/jre` on macOS.
					// We need the parent dir so we can properly lookup the `include` directories
					if (java_home.EndsWith ("jre", StringComparison.OrdinalIgnoreCase)) {
						java_home = Path.GetDirectoryName (java_home) ?? "";
					}
					yield return java_home;
				}
			}
		}
	}

	class JdkInfoVersionComparer : IComparer<JdkInfo>
	{
		public  static  readonly    IComparer<JdkInfo> Default = new JdkInfoVersionComparer ();

		public int Compare ([AllowNull]JdkInfo x, [AllowNull]JdkInfo y)
		{
			if (x?.Version != null && y?.Version != null)
				return x.Version.CompareTo (y.Version);
			return 0;
		}
	}
}
