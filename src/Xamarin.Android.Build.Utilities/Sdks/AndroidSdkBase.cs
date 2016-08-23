using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Xamarin.Android.Build.Utilities
{
	abstract class AndroidSdkBase
	{
		string[] allAndroidSdks = null;
		string[] allAndroidNdks = null;

		public string[] AllAndroidSdks {
			get {
				if (allAndroidSdks == null)
					allAndroidSdks = GetAllAvailableAndroidSdks ().Distinct ().ToArray ();
				return allAndroidSdks;
			}
		}
		public string[] AllAndroidNdks {
			get {
				if (allAndroidNdks == null)
					allAndroidNdks = GetAllAvailableAndroidNdks ().Distinct ().ToArray ();
				return allAndroidNdks;
			}
		}

		public string AndroidSdkPath { get; private set; }
		public string AndroidNdkPath { get; private set; }
		public string JavaSdkPath { get; private set; }
		public string JavaBinPath { get; private set; }
		public string AndroidToolsPath { get; private set; }
		public string AndroidPlatformToolsPath { get; private set; }
		public string AndroidToolsPathShort { get; private set; }
		public string AndroidPlatformToolsPathShort { get; private set; }

		public abstract string Adb { get; }
		public abstract string Android { get; }
		public abstract string Emulator { get; }
		public abstract string Monitor { get; }
		public abstract string ZipAlign { get; }
		public abstract string JarSigner { get; }
		public abstract string KeyTool { get; }

		public abstract string NdkStack { get; }
		public abstract string NdkHostPlatform32Bit { get; }
		public abstract string NdkHostPlatform64Bit { get; }
		public abstract string Javac { get; }

		public abstract string PreferedAndroidSdkPath { get; }
		public abstract string PreferedAndroidNdkPath { get; }
		public abstract string PreferedJavaSdkPath { get; }

		public virtual void Initialize (string androidSdkPath = null, string androidNdkPath = null, string javaSdkPath = null)
		{
			AndroidSdkPath  = ValidateAndroidSdkLocation (androidSdkPath) ? androidSdkPath : AllAndroidSdks.FirstOrDefault ();
			AndroidNdkPath  = ValidateAndroidNdkLocation (androidNdkPath) ? androidNdkPath : AllAndroidNdks.FirstOrDefault ();
			JavaSdkPath     = ValidateJavaSdkLocation (javaSdkPath) ? javaSdkPath : GetJavaSdkPath ();

			if (!string.IsNullOrEmpty (JavaSdkPath)) {
				JavaBinPath = Path.Combine (JavaSdkPath, "bin");
			} else {
				JavaBinPath = null;
			}

			if (!string.IsNullOrEmpty (AndroidSdkPath)) {
				AndroidToolsPath = Path.Combine (AndroidSdkPath, "tools");
				AndroidToolsPathShort = GetShortFormPath (AndroidToolsPath);
				AndroidPlatformToolsPath = Path.Combine (AndroidSdkPath, "platform-tools");
				AndroidPlatformToolsPathShort = GetShortFormPath (AndroidPlatformToolsPath);
			} else {
				AndroidToolsPath = null;
				AndroidToolsPathShort = null;
				AndroidPlatformToolsPath = null;
				AndroidPlatformToolsPathShort = null;
			}

			if (!string.IsNullOrEmpty (AndroidNdkPath)) {
				// It would be nice if .NET had real globbing support in System.IO...
				string toolchainsDir = Path.Combine (AndroidNdkPath, "toolchains");
				IsNdk64Bit = Directory.EnumerateDirectories (toolchainsDir, "arm-linux-androideabi-*")
					.Any (dir => Directory.Exists (Path.Combine (dir, "prebuilt", NdkHostPlatform64Bit)));
			}
		}

		protected abstract IEnumerable<string> GetAllAvailableAndroidSdks ();
		protected abstract IEnumerable<string> GetAllAvailableAndroidNdks ();
		protected abstract string GetJavaSdkPath ();
		protected abstract string GetShortFormPath (string path);

		public abstract void SetPreferredAndroidSdkPath (string path);
		public abstract void SetPreferredJavaSdkPath (string path);
		public abstract void SetPreferredAndroidNdkPath (string path);

		public bool IsNdk64Bit { get; private set; }

		public string NdkHostPlatform {
			get { return IsNdk64Bit ? NdkHostPlatform64Bit : NdkHostPlatform32Bit; }
		}

		/// <summary>
		/// Checks that a value is the location of an Android SDK.
		/// </summary>
		public bool ValidateAndroidSdkLocation (string loc)
		{
			return !string.IsNullOrEmpty (loc) && File.Exists (Path.Combine (Path.Combine (loc, "platform-tools"), Adb));
		}

		/// <summary>
		/// Checks that a value is the location of a Java SDK.
		/// </summary>
		public virtual bool ValidateJavaSdkLocation (string loc)
		{
			return !string.IsNullOrEmpty (loc) && File.Exists (Path.Combine (Path.Combine (loc, "bin"), JarSigner));
		}

		/// <summary>
		/// Checks that a value is the location of an Android SDK.
		/// </summary>
		public bool ValidateAndroidNdkLocation (string loc)
		{
			return !string.IsNullOrEmpty (loc) && FindExecutableInDirectory(NdkStack, loc).Any();
		}

		protected IEnumerable<string> FindExecutableInPath (string executable)
		{
			var path = Environment.GetEnvironmentVariable ("PATH");
			var pathDirs = path.Split (new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var dir in pathDirs) {
				foreach (var directory in FindExecutableInDirectory(executable, dir)) {
					yield return directory;
				}
			}
		}
		
		protected IEnumerable<string> FindExecutableInDirectory(string executable, string dir)
		{			
			var pathExt = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
			
			if (File.Exists (Path.Combine (dir, (executable))))
				yield return dir;
			if (pathExts == null)
				yield break;
			foreach (var ext in pathExts)
				if (File.Exists (Path.Combine (dir, Path.ChangeExtension (executable, ext))))
					yield return dir;
		}

		protected string NullIfEmpty (string s)
		{
			if (s == null || s.Length != 0)
				return s;

			return null;
		}
	}
}

