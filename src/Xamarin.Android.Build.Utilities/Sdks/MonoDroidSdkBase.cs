using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Xml;

namespace Xamarin.Android.Build.Utilities
{
	abstract class MonoDroidSdkBase
	{
		protected readonly static string ClassParseExe = "class-parse.exe";

		// Root directory for XA libraries, contains designer dependencies
		public string LibrariesPath { get; private set; }

		// Contains mscorlib.dll
		public string BclPath { get; private set; }

		// expectedRuntimePath: contains class-parse.exe
		// binPath: ignored; present for compatibility
		// bclPath: contains mscorlib.dll
		public void Initialize (string expectedRuntimePath = null, string binPath = null, string bclPath = null)
		{
			var runtimePath = GetValidPath ("MonoAndroidToolsPath", expectedRuntimePath,  ValidateRuntime, () => FindRuntime ())
				?? Path.GetFullPath (Path.GetDirectoryName (GetType ().Assembly.Location));
			bclPath = GetValidPath ("mscorlib.dll", bclPath, ValidateFramework, () => FindFramework (runtimePath));

			if (runtimePath == null || bclPath == null) {
				Reset ();
				return;
			}

			BclPath     = bclPath;
			LibrariesPath = FindLibraries (runtimePath);

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
			BclPath = LibrariesPath = null;
			#pragma warning restore 0618
		}

		protected virtual string FindRuntime ()
		{
			string monoAndroidPath = Environment.GetEnvironmentVariable ("MONO_ANDROID_PATH");
			if (!string.IsNullOrEmpty (monoAndroidPath)) {
				string msbuildDir = Path.Combine (monoAndroidPath, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
				if (Directory.Exists (msbuildDir)) {
					if (ValidateRuntime (msbuildDir))
						return msbuildDir;
					AndroidLogger.LogInfo (null, $"MONO_ANDROID_PATH points to {monoAndroidPath}, but `{msbuildDir}{Path.DirectorySeparatorChar}class-parse.exe` does not exist.");
				}
				else
					AndroidLogger.LogInfo (null, $"MONO_ANDROID_PATH points to {monoAndroidPath}, but it does not exist.");
			}
			return null;
		}

		protected abstract string FindFramework (string runtimePath);

		protected static bool ValidateRuntime (string loc)
		{
			return !string.IsNullOrWhiteSpace (loc) &&
				 File.Exists (Path.Combine (loc, ClassParseExe));
		}

		protected static bool ValidateFramework (string loc)
		{
			return loc != null && File.Exists (Path.Combine (loc, "mscorlib.dll"));
		}

		protected abstract string FindLibraries (string runtimePath);

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

