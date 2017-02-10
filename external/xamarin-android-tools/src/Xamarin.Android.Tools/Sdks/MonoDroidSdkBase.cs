using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Xml;

namespace Xamarin.Android.Tools
{
	abstract class MonoDroidSdkBase
	{
		protected readonly static string DebugRuntime = "Mono.Android.DebugRuntime-debug.apk";
		protected readonly static string ClassParseExe = "class-parse.exe";
		protected readonly static string GeneratorScript = "generator";

		// I can never remember the difference between SdkPath and anything else...
		[Obsolete ("Do not use.")]
		public string SdkPath { get; private set; }

		// Contains mandroid
		public string BinPath { get; private set; }

		// Not actually shipped...
		public string IncludePath { get; private set; }

		// Contains Mono.Android.DebugRuntime-*.apk, platforms/*/*.apk.
		public string RuntimePath { get; private set; }

		// Root directory for XA libraries, contains designer dependencies
		public string LibrariesPath { get; private set; }

		// Contains mscorlib.dll
		public string BclPath { get; private set; }

		public int SharedRuntimeVersion { get; private set; }

		// expectedRuntimePath: contains Mono.Android.DebugRuntime-*.apk
		// binPath:     contains mandroid
		// mscorlibDir: contains mscorlib.dll
		public void Initialize (string expectedRuntimePath = null, string binPath = null, string bclPath = null)
		{
			var runtimePath = GetValidPath ("MonoAndroidToolsPath", expectedRuntimePath,  ValidateRuntime, () => FindRuntime ());
			if (runtimePath != null) {
				binPath = GetValidPath ("MonoAndroidBinPath", binPath, ValidateBin, () => FindBin (runtimePath));
				bclPath = GetValidPath ("mscorlib.dll", bclPath, ValidateFramework, () => FindFramework (runtimePath));
			} else {
				if (expectedRuntimePath != null)
					AndroidLogger.LogWarning (null, "Runtime was not found at {0}", expectedRuntimePath);
				binPath = bclPath = null;
			}

			if (runtimePath == null || binPath == null || bclPath == null) {
				Reset ();
				return;
			}

			RuntimePath = runtimePath;
			#pragma warning disable 0618
			SdkPath     = Path.GetFullPath (Path.Combine (runtimePath, "..", ".."));
			#pragma warning restore 0618
			BinPath     = binPath;
			BclPath     = bclPath;
			LibrariesPath = FindLibraries (runtimePath);

			IncludePath = FindInclude (runtimePath);
			if (IncludePath != null && !Directory.Exists (IncludePath))
				IncludePath = null;

			SharedRuntimeVersion = GetCurrentSharedRuntimeVersion ();
			FindSupportedFrameworks ();
		}

		static string GetValidPath (string description, string path, Func<string, bool> validator, Func<string> defaultPath)
		{
			if (!string.IsNullOrEmpty (path)) {
				if (Directory.Exists (path)) {
					if (validator (path))
						return path;
					AndroidLogger.LogWarning (null, "{0} path '{1}' is explicitly specified, but it was not valid; skipping.", description, path);
				} else
					AndroidLogger.LogWarning (null, "{0} path '{1}' is explicitly specified, but it was not found; skipping.", description, path);
			}
			path = defaultPath ();
			if (path != null && validator (path))
				return path;
			if (path != null)
				AndroidLogger.LogWarning (null, "{0} path is defaulted to '{1}', but it was not valid; skipping", description, path);
			else
				AndroidLogger.LogWarning (null, "{0} path is not found and no default location is provided; skipping", description);
			return null;
		}

		public void Reset ()
		{
			#pragma warning disable 0618
			SdkPath = BinPath = IncludePath = RuntimePath = BclPath = null;
			#pragma warning restore 0618
			SharedRuntimeVersion = 0;
		}

		protected abstract string FindRuntime ();
		protected abstract string FindFramework (string runtimePath);

		// Check for platform-specific `mandroid` name
		protected abstract bool ValidateBin (string binPath);

		protected static bool ValidateRuntime (string loc)
		{
			return !string.IsNullOrWhiteSpace (loc) &&
				(File.Exists (Path.Combine (loc, DebugRuntime)) ||    // Normal/expected
				 File.Exists (Path.Combine (loc, ClassParseExe)) ||    // Normal/expected
					File.Exists (Path.Combine (loc, "Ionic.Zip.dll")));  // Wrench builds
		}

		protected static bool ValidateFramework (string loc)
		{
			return loc != null && File.Exists (Path.Combine (loc, "mscorlib.dll"));
		}

		public string FindVersionFile ()
		{
			#pragma warning disable 0618
			if (string.IsNullOrEmpty (SdkPath))
				return null;
			#pragma warning restore 0618
			foreach (var loc in GetVersionFileLocations ()) {
				if (File.Exists (loc)) {
					return loc;
				}
			}
			return null;
		}

		protected virtual IEnumerable<string> GetVersionFileLocations ()
		{
			#pragma warning disable 0618
			yield return Path.Combine (SdkPath, "Version");
			#pragma warning restore 0618
		}

		protected abstract string FindBin (string runtimePath);

		protected abstract string FindInclude (string runtimePath);

		protected abstract string FindLibraries (string runtimePath);

		[Obsolete ("Do not use.")]
		public string GetPlatformNativeLibPath (string abi)
		{
			return FindPlatformNativeLibPath (SdkPath, abi);
		}

		[Obsolete ("Do not use.")]
		public string GetPlatformNativeLibPath (AndroidTargetArch arch)
		{
			return FindPlatformNativeLibPath (SdkPath, GetMonoDroidArchName (arch));
		}

		[Obsolete ("Do not use.")]
		static string GetMonoDroidArchName (AndroidTargetArch arch)
		{
			switch (arch) {
			case AndroidTargetArch.Arm:
				return "armeabi";
			case AndroidTargetArch.Mips:
				return "mips";
			case AndroidTargetArch.X86:
				return "x86";
			}
			return null;
		}

		[Obsolete]
		protected string FindPlatformNativeLibPath (string sdk, string arch)
		{
			return Path.Combine (sdk, "lib", arch);
		}

		static XmlReaderSettings GetSafeReaderSettings ()
		{
			//allow DTD but not try to resolve it from web
			return new XmlReaderSettings {
				CloseInput = true,
				DtdProcessing = DtdProcessing.Ignore,
				XmlResolver = null,
			};
		}

		int GetCurrentSharedRuntimeVersion ()
		{
			string file = Path.Combine (RuntimePath, "Mono.Android.DebugRuntime-debug.xml");

			return GetManifestVersion (file);
		}

		internal static int GetManifestVersion (string file)
		{
			// It seems that MfA 1.0 on Windows didn't include the xml files to get the runtime version.
			if (!File.Exists (file))
				return int.MaxValue;

			try {
				using (var r = XmlReader.Create (file, GetSafeReaderSettings())) {
					if (r.MoveToContent () == XmlNodeType.Element && r.MoveToAttribute ("android:versionCode")) {
						int value;
						if (int.TryParse (r.Value, out value))
							return value;
						AndroidLogger.LogInfo ("Cannot parse runtime version code: ({0})", r.Value);
					}
				}
			} catch (Exception ex) {
				AndroidLogger.LogError ("Error trying to find shared runtime version", ex);
			}
			return int.MaxValue;
		}

		internal static Version ToVersion (string frameworkDir)
		{
			string version = Path.GetFileName (frameworkDir);
			if (!version.StartsWith ("v", StringComparison.OrdinalIgnoreCase)) {
				// wat?
				return new Version ();
			}
			version = version.Substring (1);
			Version v;
			if (Version.TryParse (version, out v))
				return v;
			return new Version ();
		}

		void FindSupportedFrameworks ()
		{
			string bclDir           = MonoDroidSdk.FrameworkPath;
			string frameworksDir    = Path.GetDirectoryName (bclDir);
			foreach (var framework in Directory.EnumerateDirectories (frameworksDir).Select (ToVersion)) {
				if (framework.Major == 0)
					continue;
				string apiLevel;
				if (FrameworkToApiLevels.TryGetValue (framework, out apiLevel))
					SupportedFrameworks.Add (framework, apiLevel);
			}
		}

		readonly Dictionary<Version, string>  SupportedFrameworks = new Dictionary<Version, string> ();

		static readonly Dictionary<Version, string> FrameworkToApiLevels = new Dictionary<Version, string> (AndroidVersion.KnownVersions.ToDictionary<AndroidVersion, Version, string> (k => k.Version, v => v.ApiLevel.ToString ()));
		static readonly Dictionary<Version, string> LegacyFrameworkToApiLevels = new Dictionary<Version, string> {
			{ new Version (4, 5), "21" } // L Preview
		};

		public IEnumerable<string> GetSupportedApiLevels ()
		{
			return SupportedFrameworks.Select (e => e.Value);
		}

		public string GetApiLevelForFrameworkVersion (string framework)
		{
			Version v;
			if (!Version.TryParse (framework.TrimStart ('v'), out v))
				return null;
			string apiLevel;
			if (SupportedFrameworks.TryGetValue (v, out apiLevel)
				|| FrameworkToApiLevels.TryGetValue (v, out apiLevel)
				|| LegacyFrameworkToApiLevels.TryGetValue (v, out apiLevel))
				return apiLevel;
			return null;
		}

		public string GetFrameworkVersionForApiLevel (string apiLevel)
		{
			// API level 9 was discontinued immediately for 10, in the rare case we get it just upgrade the number
			if (apiLevel == "9")
				apiLevel = "10";
			var maxFrameworkVersion = SupportedFrameworks.Concat (FrameworkToApiLevels)
				.Where (e => e.Value == apiLevel)
				.OrderByDescending (e => e.Key, Comparer<Version>.Default)
				.Select (e => e.Key)
				.FirstOrDefault ();
			if (maxFrameworkVersion != null)
				return "v" + maxFrameworkVersion;
			return null;
		}

		/// <summary>
		/// Determines if the given apiLevel is supported by an installed Framework
		/// </summary>
		public bool IsSupportedFrameworkLevel (string apiLevel)
		{
			return SupportedFrameworks.Any ((sf => sf.Value == apiLevel)); 
		}
	}
}

