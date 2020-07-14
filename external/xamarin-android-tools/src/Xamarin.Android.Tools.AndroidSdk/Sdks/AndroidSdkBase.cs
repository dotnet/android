using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Xamarin.Android.Tools
{
	abstract class AndroidSdkBase
	{
		string[]? allAndroidSdks;

		public string[] AllAndroidSdks {
			get {
				if (allAndroidSdks == null) {
					var dirs = new List<string?> ();
					dirs.Add (AndroidSdkPath);
					dirs.AddRange (GetAllAvailableAndroidSdks ());
					allAndroidSdks = dirs.Where (d => ValidateAndroidSdkLocation (d))
						.Select (d => d!)
						.Distinct ()
						.ToArray ();
				}
				return allAndroidSdks;
			}
		}

		public readonly Action<TraceLevel, string> Logger;

		public AndroidSdkBase (Action<TraceLevel, string> logger)
		{
			Logger  = logger;
		}

		public  string?     AndroidSdkPath                  { get; private set; }
		public  string?     AndroidNdkPath                  { get; private set; }
		public  string?     JavaSdkPath                     { get; private set; }
		public  string?     JavaBinPath                     { get; private set; }
		public  string?     AndroidPlatformToolsPath        { get; private set; }
		public  string?	    AndroidPlatformToolsPathShort   { get; private set; }

		public virtual string Adb { get; protected set; } = "adb";
		public virtual string ZipAlign { get; protected set; } = "zipalign";
		public virtual string JarSigner { get; protected set; } = "jarsigner";
		public virtual string KeyTool { get; protected set; } = "keytool";

		public virtual string NdkStack { get; protected set; } = "ndk-stack";
		public abstract string NdkHostPlatform32Bit { get; }
		public abstract string NdkHostPlatform64Bit { get; }
		public virtual string Javac { get; protected set; } = "javac";

		public  abstract    string?     PreferedAndroidSdkPath      { get; }
		public  abstract    string?     PreferedAndroidNdkPath      { get; }
		public  abstract    string?     PreferedJavaSdkPath         { get; }

		public virtual void Initialize (string? androidSdkPath = null, string? androidNdkPath = null, string? javaSdkPath = null)
		{
			AndroidSdkPath = GetValidPath (ValidateAndroidSdkLocation,  androidSdkPath, () => PreferedAndroidSdkPath,   () => GetAllAvailableAndroidSdks ());
			JavaSdkPath    = GetValidPath (ValidateJavaSdkLocation,     javaSdkPath,    () => PreferedJavaSdkPath,      () => GetJavaSdkPaths ());

			AndroidNdkPath = GetValidNdkPath (androidNdkPath);

			if (!string.IsNullOrEmpty (JavaSdkPath)) {
				JavaBinPath = Path.Combine (JavaSdkPath, "bin");
			} else {
				JavaBinPath = null;
			}

			if (!string.IsNullOrEmpty (AndroidSdkPath)) {
				AndroidPlatformToolsPath = Path.Combine (AndroidSdkPath, "platform-tools");
				AndroidPlatformToolsPathShort = GetShortFormPath (AndroidPlatformToolsPath);
			} else {
				AndroidPlatformToolsPath = null;
				AndroidPlatformToolsPathShort = null;
			}

			if (!string.IsNullOrEmpty (AndroidNdkPath)) {
				// It would be nice if .NET had real globbing support in System.IO...
				string toolchainsDir = Path.Combine (AndroidNdkPath, "toolchains");
				if (Directory.Exists (toolchainsDir)) {
					IsNdk64Bit = Directory.EnumerateDirectories (toolchainsDir, "arm-linux-androideabi-*")
						.Any (dir => Directory.Exists (Path.Combine (dir, "prebuilt", NdkHostPlatform64Bit)));
				}
			}
			// we need to look for extensions other than the default .exe|.bat
			// google have a habbit of changing them.
			Adb = GetExecutablePath (AndroidPlatformToolsPath, Adb);
			NdkStack = GetExecutablePath (AndroidNdkPath, NdkStack);
		}

		static string? GetValidPath (Func<string?, bool> pathValidator, string? ctorParam, Func<string?> getPreferredPath, Func<IEnumerable<string>> getAllPaths)
		{
			if (pathValidator (ctorParam))
				return ctorParam;
			ctorParam = getPreferredPath ();
			if (pathValidator (ctorParam))
				return ctorParam;
			foreach (var path in getAllPaths ()) {
				if (pathValidator (path))
					return path;
			}
			return null;
		}

		string? GetValidNdkPath (string? ctorParam)
		{
			if (ValidateAndroidNdkLocation (ctorParam))
				return ctorParam;
			if (AndroidSdkPath != null) {
				string bundle = Path.Combine (AndroidSdkPath, "ndk-bundle");
				if (Directory.Exists (bundle) && ValidateAndroidNdkLocation (bundle))
					return bundle;
			}
			ctorParam = PreferedAndroidNdkPath;
			if (ValidateAndroidNdkLocation (ctorParam))
				return ctorParam;
			foreach (var path in GetAllAvailableAndroidNdks ()) {
				if (ValidateAndroidNdkLocation (path))
					return path;
			}
			return null;
		}

		protected abstract IEnumerable<string> GetAllAvailableAndroidSdks ();
		protected abstract string GetShortFormPath (string path);

		protected virtual IEnumerable<string> GetAllAvailableAndroidNdks ()
		{
			// Look in PATH
			foreach (var ndkStack in ProcessUtils.FindExecutablesInPath (NdkStack)) {
				var ndkDir  = Path.GetDirectoryName (ndkStack);
				if (ndkDir == null)
					continue;
				yield return ndkDir;
			}

			// Check for the "ndk-bundle" directory inside other SDK directories
			foreach (var sdk in GetAllAvailableAndroidSdks ()) {
				if (sdk == AndroidSdkPath)
					continue;
				yield return Path.Combine (sdk, "ndk-bundle");
			}
		}

		public abstract void SetPreferredAndroidSdkPath (string? path);
		public abstract void SetPreferredJavaSdkPath (string? path);
		public abstract void SetPreferredAndroidNdkPath (string? path);

		public bool IsNdk64Bit { get; private set; }

		public string NdkHostPlatform {
			get { return IsNdk64Bit ? NdkHostPlatform64Bit : NdkHostPlatform32Bit; }
		}

		IEnumerable<string> GetJavaSdkPaths ()
		{
			return JdkInfo.GetKnownSystemJdkInfos (Logger)
				.Select (jdk => jdk.HomePath);
		}

		/// <summary>
		/// Checks that a value is the location of an Android SDK.
		/// </summary>
		public bool ValidateAndroidSdkLocation ([NotNullWhen (true)] string? loc)
		{
			bool result = !string.IsNullOrEmpty (loc) && ProcessUtils.FindExecutablesInDirectory (Path.Combine (loc, "platform-tools"), Adb).Any ();
			Logger (TraceLevel.Verbose, $"{nameof (ValidateAndroidSdkLocation)}: `{loc}`, result={result}");
			return result;
		}

		/// <summary>
		/// Checks that a value is the location of a Java SDK.
		/// </summary>
		public virtual bool ValidateJavaSdkLocation ([NotNullWhen (true)] string? loc)
		{
			bool result = !string.IsNullOrEmpty (loc) && ProcessUtils.FindExecutablesInDirectory (Path.Combine (loc, "bin"), JarSigner).Any ();
			Logger (TraceLevel.Verbose, $"{nameof (ValidateJavaSdkLocation)}: `{loc}`, result={result}");
			return result;
		}

		/// <summary>
		/// Checks that a value is the location of an Android SDK.
		/// </summary>
		public bool ValidateAndroidNdkLocation ([NotNullWhen (true)] string? loc)
		{
			bool result = !string.IsNullOrEmpty (loc) &&
				ProcessUtils.FindExecutablesInDirectory (loc!, NdkStack).Any ();
			Logger (TraceLevel.Verbose, $"{nameof (ValidateAndroidNdkLocation)}: `{loc}`, result={result}");
			return result;
		}

		protected static string? NullIfEmpty (string? s)
		{
			if (s == null || s.Length != 0)
				return s;

			return null;
		}

		static string GetExecutablePath (string? dir, string exe)
		{
			if (string.IsNullOrEmpty (dir))
				return exe;

			foreach (var e in ProcessUtils.ExecutableFiles (exe))
				if (File.Exists (Path.Combine (dir, e)))
					return e;
			return exe;
		}
	}
}

