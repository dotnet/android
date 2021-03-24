using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools.AndroidSdk.Properties;

namespace Xamarin.Android.Tools
{
	class AndroidSdkWindows : AndroidSdkBase
	{
		const string MDREG_KEY = @"SOFTWARE\Novell\Mono for Android";
		const string MDREG_ANDROID_SDK = "AndroidSdkDirectory";
		const string MDREG_ANDROID_NDK = "AndroidNdkDirectory";
		const string MDREG_JAVA_SDK = "JavaSdkDirectory";
		const string ANDROID_INSTALLER_PATH = @"SOFTWARE\Android SDK Tools";
		const string ANDROID_INSTALLER_KEY = "Path";
		const string XAMARIN_ANDROID_INSTALLER_PATH = @"SOFTWARE\Xamarin\MonoAndroid";
		const string XAMARIN_ANDROID_INSTALLER_KEY = "PrivateAndroidSdkPath";
		const string MICROSOFT_OPENJDK_PATH         = @"SOFTWARE\Microsoft\JDK";

		public AndroidSdkWindows (Action<TraceLevel, string> logger)
			: base (logger)
		{
		}

		static readonly string _JarSigner = "jarsigner.exe";

		public override string ZipAlign { get; protected set; } = "zipalign.exe";
		public override string JarSigner { get; protected set; } = _JarSigner;
		public override string KeyTool { get; protected set; } = "keytool.exe";

		public override string NdkHostPlatform32Bit { get { return "windows"; } }
		public override string NdkHostPlatform64Bit { get { return "windows-x86_64"; } }
		public override string Javac { get; protected set; } = "javac.exe";

		public override string? PreferedAndroidSdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey ();
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, wow, "platform-tools", Adb))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, wow);
				return null;
			}
		}
		public override string? PreferedAndroidNdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey ();
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, wow, ".", NdkStack))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, wow);
				return null;
			}
		}
		public override string? PreferedJavaSdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey ();
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, wow, "bin", JarSigner))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, wow);
				return null;
			}
		}

		static string GetMDRegistryKey ()
		{
			var regKey = Environment.GetEnvironmentVariable ("XAMARIN_ANDROID_REGKEY");
			return string.IsNullOrWhiteSpace (regKey) ? MDREG_KEY : regKey;
		}

		protected override IEnumerable<string> GetAllAvailableAndroidSdks ()
		{
			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			Logger (TraceLevel.Info, "Looking for Android SDK...");

			// Check for the key the user gave us in the VS/addin options
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_ANDROID_SDK, wow, "platform-tools", Adb))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_ANDROID_SDK, wow) ?? "";

			// Check for the key written by the Xamarin installer
			if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb))
				yield return RegistryEx.GetValueString (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow) ?? "";

			// Check for the key written by the Android SDK installer
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, ANDROID_INSTALLER_PATH, ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb))
					yield return RegistryEx.GetValueString (root, ANDROID_INSTALLER_PATH, ANDROID_INSTALLER_KEY, wow) ?? "";

			// Check some hardcoded paths for good measure
			var paths = new string [] {
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin", "MonoAndroid", "android-sdk-windows"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk-windows"),
				!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("ProgramW6432"))
					? Path.Combine (Environment.GetEnvironmentVariable ("ProgramW6432") ?? "", "Android", "android-sdk")
					: Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Android", "android-sdk"),
				@"C:\android-sdk-windows"
			};
			foreach (var basePath in paths)
				if (Directory.Exists (basePath))
					yield return basePath;
		}

		internal static IEnumerable<JdkInfo> GetJdkInfos (Action<TraceLevel, string> logger)
		{
			JdkInfo? TryGetJdkInfo (string path, string locator)
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

			IEnumerable<JdkInfo> ToJdkInfos (IEnumerable<string> paths, string locator)
			{
				return paths.Select (p => TryGetJdkInfo (p, locator))
					.Where (jdk => jdk != null)
					.Select(jdk => jdk!)
					.OrderByDescending (jdk => jdk, JdkInfoVersionComparer.Default);
			}

			return ToJdkInfos (GetPreferredJdkPaths (), "Preferred Registry")
				.Concat (ToJdkInfos (GetMicrosoftOpenJdkFilesystemPaths (), "Microsoft OpenJDK Filesystem"))
				.Concat (ToJdkInfos (GetMicrosoftOpenJdkRegistryPaths (), "Microsoft OpenJDK Registry"))
				.Concat (ToJdkInfos (GetVSAndroidJdkPaths (), @"HKLM\SOFTWARE\Microsoft\VisualStudio\Android@JavaHome"))
				.Concat (ToJdkInfos (GetOracleJdkPaths (), "Oracle JDK"))
				;
		}

		private static IEnumerable<string> GetPreferredJdkPaths ()
		{
			// check the user specified path
			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			const RegistryEx.Wow64 wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			foreach (var root in roots) {
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_JAVA_SDK, wow, "bin", _JarSigner))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_JAVA_SDK, wow) ?? "";
			}
		}

		private static IEnumerable<string> GetVSAndroidJdkPaths ()
		{
			var root = RegistryEx.LocalMachine;
			var wows = new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 };
			var subKey = @"SOFTWARE\Microsoft\VisualStudio\Android";
			var valueName = "JavaHome";

			foreach (var wow in wows) {
				if (CheckRegistryKeyForExecutable (root, subKey, valueName, wow, "bin", _JarSigner))
					yield return RegistryEx.GetValueString (root, subKey, valueName, wow) ?? "";
			}
		}

		static IEnumerable<string> GetMicrosoftOpenJdkFilesystemPaths ()
		{
			const string JdkFolderNamePrefix = "jdk-";

			var paths = new List<Tuple<string, Version>> ();
			var rootPaths = new List<string> {
				Path.Combine (Environment.ExpandEnvironmentVariables ("%ProgramW6432%"), "Microsoft"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "jdk"),
			};

			foreach (var rootPath in rootPaths) {
				if (!Directory.Exists (rootPath))
					continue;
				foreach (var directoryName in Directory.EnumerateDirectories (rootPath, $"{JdkFolderNamePrefix}*")) {
					var version = ExtractVersion (directoryName, JdkFolderNamePrefix);
					if (version == null)
						continue;
					paths.Add (Tuple.Create (directoryName, version));
				}
			}

			return paths.OrderByDescending (v => v.Item2)
				.Where (openJdk => ProcessUtils.FindExecutablesInDirectory (Path.Combine (openJdk.Item1, "bin"), _JarSigner).Any ())
				.Select (openJdk => openJdk.Item1);
		}

		static IEnumerable<string> GetMicrosoftOpenJdkRegistryPaths ()
		{
			var paths   = new List<(Version version, string path)> ();
			var roots   = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wows    = new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 };
			foreach (var root in roots)
			foreach (var wow in wows) {
				foreach (var subkeyName in RegistryEx.EnumerateSubkeys (root, MICROSOFT_OPENJDK_PATH, wow)) {
					if (!Version.TryParse (subkeyName, out var version))
						continue;
					var msiKey  = $@"{MICROSOFT_OPENJDK_PATH}\{subkeyName}\hotspot\MSI";
					var path    = RegistryEx.GetValueString (root, msiKey, "Path", wow);
					if (path == null)
						continue;
					paths.Add ((version, path));
				}
			}

			return paths.OrderByDescending (e => e.version)
				.Select (e => e.path);
		}

		internal static Version? ExtractVersion (string path, string prefix)
		{
			var name = Path.GetFileName (path);
			if (name.Length <= prefix.Length)
				return null;
			if (!name.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
				return null;

			var start   = prefix.Length;
			while (start < name.Length && !char.IsDigit (name, start)) {
				++start;
			}
			if (start == name.Length)
				return null;

			name    = name.Substring (start);
			int end = 0;
			while (end < name.Length &&
					(char.IsDigit (name [end]) || name [end] == '.')) {
				end++;
			}

			do {
				if (Version.TryParse (name.Substring (0, end), out var v))
					return v;
				end = name.LastIndexOf ('.', end-1);
			} while (end > 0);

			return null;
		}

		private static IEnumerable<string> GetOracleJdkPaths ()
		{ 
			string subkey = @"SOFTWARE\JavaSoft\Java Development Kit";

			foreach (var wow64 in new[] { RegistryEx.Wow64.Key32, RegistryEx.Wow64.Key64 }) {
				string key_name = string.Format (@"{0}\{1}\{2}", "HKLM", subkey, "CurrentVersion");
				var currentVersion = RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey, "CurrentVersion", wow64);

				if (!string.IsNullOrEmpty (currentVersion)) {

					// No matter what the CurrentVersion is, look for 1.6 or 1.7 or 1.8
					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64, "bin", _JarSigner))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.8", "JavaHome", wow64) ?? "";

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64, "bin", _JarSigner))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.7", "JavaHome", wow64) ?? "";

					if (CheckRegistryKeyForExecutable (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64, "bin", _JarSigner))
						yield return RegistryEx.GetValueString (RegistryEx.LocalMachine, subkey + "\\" + "1.6", "JavaHome", wow64) ?? "";
				}
			}
		}

		protected override IEnumerable<string> GetAllAvailableAndroidNdks ()
		{

			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey ();

			Logger (TraceLevel.Info, "Looking for Android NDK...");

			// Check for the key the user gave us in the VS/addin options
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_ANDROID_NDK, wow, ".", NdkStack))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_ANDROID_NDK, wow) ?? "";

			foreach (string dir in GetSdkFromEnvironmentVariables ()) {
				yield return dir;
			}

			/*
			// Check for the key written by the Xamarin installer
			if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb))
				yield return RegistryEx.GetValueString (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow);
			*/

			// Check some hardcoded paths for good measure
			var xamarin_private = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin", "MonoAndroid");
			var vs_default = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Microsoft", "AndroidNDK");
			var vs_default32bit = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Microsoft", "AndroidNDK32");
			var vs_2017_default = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Microsoft", "AndroidNDK64");
			var android_default = Path.Combine (OS.ProgramFilesX86 ?? "", "Android");
			var cdrive_default = @"C:\";

			foreach (var basePath in new string [] {xamarin_private, android_default, vs_default, vs_default32bit, vs_2017_default, cdrive_default})
				if (Directory.Exists (basePath))
					foreach (var dir in Directory.GetDirectories (basePath, "android-ndk-r*"))
						if (ValidateAndroidNdkLocation (dir))
							yield return dir;

			foreach (var dir in base.GetAllAvailableAndroidNdks ()) {
				yield return dir;
			}
		}

		protected override string GetShortFormPath (string path)
		{
			return KernelEx.GetShortPathName (path);
		}

		public override void SetPreferredAndroidSdkPath (string? path)
		{
			var regKey = GetMDRegistryKey ();
			RegistryEx.SetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, path ?? "", RegistryEx.Wow64.Key32);
		}

		public override void SetPreferredJavaSdkPath (string? path)
		{
			var regKey = GetMDRegistryKey ();
			RegistryEx.SetValueString (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, path ?? "", RegistryEx.Wow64.Key32);
		}

		public override void SetPreferredAndroidNdkPath (string? path)
		{
			var regKey = GetMDRegistryKey ();
			RegistryEx.SetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, path ?? "", RegistryEx.Wow64.Key32);
		}

		#region Helper Methods
		private static bool CheckRegistryKeyForExecutable (UIntPtr key, string subkey, string valueName, RegistryEx.Wow64 wow64, string subdir, string exe)
		{
			try {
				string key_name = string.Format (@"{0}\{1}\{2}", key == RegistryEx.CurrentUser ? "HKCU" : "HKLM", subkey, valueName);

				var path = NullIfEmpty (RegistryEx.GetValueString (key, subkey, valueName, wow64));

				if (path == null) {
					return false;
				}

				if (!ProcessUtils.FindExecutablesInDirectory (Path.Combine (path, subdir), exe).Any ()) {
					return false;
				}

				return true;
			} catch (Exception) {
				return false;
			}
		}
		#endregion

		public override void Initialize (string? androidSdkPath = null, string? androidNdkPath = null, string? javaSdkPath = null)
		{
			base.Initialize (androidSdkPath, androidNdkPath, javaSdkPath);

			var jdkPath = JavaSdkPath;
			if (!string.IsNullOrEmpty (jdkPath)) {
				var cur = Environment.GetEnvironmentVariable ("JAVA_HOME");
				if (!string.IsNullOrEmpty (cur))
					Environment.SetEnvironmentVariable ("JAVA_HOME", jdkPath);

				var javaBinPath = this.JavaBinPath;
				if (!string.IsNullOrEmpty (javaBinPath)) {
					var environmentPath = Environment.GetEnvironmentVariable ("PATH") ?? "";
					if (!environmentPath.Contains (javaBinPath)) {
						var processPath = string.Concat (javaBinPath, Path.PathSeparator, environmentPath);
						Environment.SetEnvironmentVariable ("PATH", processPath);
					}
				}
			}
		}
	}
}
