using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools.AndroidSdk.Properties;

namespace Xamarin.Android.Tools
{
	using static RegistryEx;

	class AndroidSdkWindows : AndroidSdkBase
	{
		const string MDREG_KEY = @"SOFTWARE\Novell\Mono for Android";
		const string MDREG_ANDROID_SDK = "AndroidSdkDirectory";
		const string MDREG_ANDROID_NDK = "AndroidNdkDirectory";
		internal const string MDREG_JAVA_SDK = "JavaSdkDirectory";
		const string ANDROID_INSTALLER_PATH = @"SOFTWARE\Android SDK Tools";
		const string ANDROID_INSTALLER_KEY = "Path";
		const string XAMARIN_ANDROID_INSTALLER_PATH = @"SOFTWARE\Xamarin\MonoAndroid";
		const string XAMARIN_ANDROID_INSTALLER_KEY = "PrivateAndroidSdkPath";
		const string MICROSOFT_OPENJDK_PATH         = @"SOFTWARE\Microsoft\JDK";

		public AndroidSdkWindows (Action<TraceLevel, string> logger, IDictionary<string, string>? environmentVariables = null)
			: base (logger, environmentVariables)
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
				var regKey = GetMDRegistryKey (EnvironmentVariables);
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, wow, "platform-tools", Adb, EnvironmentVariables))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_SDK, wow);
				return null;
			}
		}
		public override string? PreferedAndroidNdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey (EnvironmentVariables);
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, wow, ".", NdkStack, EnvironmentVariables))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_ANDROID_NDK, wow);
				return null;
			}
		}
		public override string? PreferedJavaSdkPath {
			get {
				var wow = RegistryEx.Wow64.Key32;
				var regKey = GetMDRegistryKey (EnvironmentVariables);
				if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, wow, "bin", JarSigner, EnvironmentVariables))
					return RegistryEx.GetValueString (RegistryEx.CurrentUser, regKey, MDREG_JAVA_SDK, wow);
				return null;
			}
		}

		internal static string GetMDRegistryKey ()
			=> GetMDRegistryKey (null);

		internal static string GetMDRegistryKey (IDictionary<string, string>? environmentVariables)
		{
			var regKey = ProcessUtils.GetEnvironmentVariable ("XAMARIN_ANDROID_REGKEY", environmentVariables);
			return regKey == null || string.IsNullOrWhiteSpace (regKey) ? MDREG_KEY : regKey;
		}

		protected override IEnumerable<string> GetAllAvailableAndroidSdks ()
		{
			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey (EnvironmentVariables);

			Logger (TraceLevel.Info, "Looking for Android SDK...");

			// Check for the key the user gave us in the VS/addin options
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_ANDROID_SDK, wow, "platform-tools", Adb, EnvironmentVariables))
					yield return RegistryEx.GetValueString (root, regKey, MDREG_ANDROID_SDK, wow) ?? "";

			// Check for the key written by the Xamarin installer
			if (CheckRegistryKeyForExecutable (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb, EnvironmentVariables))
				yield return RegistryEx.GetValueString (RegistryEx.CurrentUser, XAMARIN_ANDROID_INSTALLER_PATH, XAMARIN_ANDROID_INSTALLER_KEY, wow) ?? "";

			// Check for the key written by the Android SDK installer
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, ANDROID_INSTALLER_PATH, ANDROID_INSTALLER_KEY, wow, "platform-tools", Adb, EnvironmentVariables))
					yield return RegistryEx.GetValueString (root, ANDROID_INSTALLER_PATH, ANDROID_INSTALLER_KEY, wow) ?? "";

			foreach (string dir in GetSdkFromEnvironmentVariables ())
				yield return dir;

			// Check some hardcoded paths for good measure
			var paths = new string [] {
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin", "MonoAndroid", "android-sdk-windows"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk-windows"),
				!string.IsNullOrEmpty (GetEnvironmentVariable ("ProgramW6432"))
					? Path.Combine (GetEnvironmentVariable ("ProgramW6432") ?? "", "Android", "android-sdk")
					: Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData), "Android", "android-sdk"),
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk"),
			};
			foreach (var basePath in paths)
				if (Directory.Exists (basePath))
					yield return basePath;
		}

		protected override IEnumerable<string> GetAllAvailableAndroidNdks ()
		{

			var roots = new[] { RegistryEx.CurrentUser, RegistryEx.LocalMachine };
			var wow = RegistryEx.Wow64.Key32;
			var regKey = GetMDRegistryKey (EnvironmentVariables);

			Logger (TraceLevel.Info, "Looking for Android NDK...");

			// Check for the key the user gave us in the VS/addin options
			foreach (var root in roots)
				if (CheckRegistryKeyForExecutable (root, regKey, MDREG_ANDROID_NDK, wow, ".", NdkStack, EnvironmentVariables))
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
						if (ValidateAndroidNdkLocation ("Windows known NDK path", dir))
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

		public override void Initialize (string? androidSdkPath = null, string? androidNdkPath = null, string? javaSdkPath = null)
		{
			base.Initialize (androidSdkPath, androidNdkPath, javaSdkPath);

			// Snapshot-based discovery must not change the environment of other tasks.
			if (EnvironmentVariables != null)
				return;

			var jdkPath = JavaSdkPath;
			if (!string.IsNullOrEmpty (jdkPath)) {
				var cur = Environment.GetEnvironmentVariable (EnvironmentVariableNames.JavaHome);
				if (!string.IsNullOrEmpty (cur))
					Environment.SetEnvironmentVariable (EnvironmentVariableNames.JavaHome, jdkPath);

				var javaBinPath = this.JavaBinPath;
				if (!string.IsNullOrEmpty (javaBinPath)) {
					var environmentPath = Environment.GetEnvironmentVariable (EnvironmentVariableNames.Path) ?? "";
					if (!environmentPath.Contains (javaBinPath)) {
						var processPath = string.Concat (javaBinPath, Path.PathSeparator, environmentPath);
						Environment.SetEnvironmentVariable (EnvironmentVariableNames.Path, processPath);
					}
				}
			}
		}
	}
}
