//
// MonoDroidSdkBase.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
//       Andreia Gaita <andreia@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.AndroidTools;
using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.AndroidTools
{
	abstract class MonoDroidSdkBase
	{
		// I can never remember the difference between SdkPath and anything else...
		[Obsolete ("Do not use.")]
		public string SdkPath { get; private set; }

		// Contains mono, *-as, cross-arm, etc.
		public string BinPath { get; private set; }

		// Not actually shipped...
		public string IncludePath { get; private set; }

		// Contains Mono.Android.DebugRuntime-*.apk, platforms/*/*.apk.
		public string RuntimePath { get; private set; }

		// Root directory for XA libraries, contains designer dependencies
		public string LibrariesPath { get; private set; }

		// Contains mscorlib.dll
		public string BclPath { get; private set; }

		public AndroidVersions AndroidVersions { get; private set; }

		public int SharedRuntimeVersion { get; private set; }

		// runtimePath: contains Mono.Android.DebugRuntime-*.apk
		// binPath:     contains aapt2, ndk/*-as, etc.
		// bclPath:     contains mscorlib.dll
		public void Initialize (string runtimePath = null, string binPath = null, string bclPath = null)
		{
			runtimePath = GetValidPath ("$(MonoAndroidToolsDirectory)", runtimePath,  ValidateRuntime, () => FindRuntime ());
			if (runtimePath != null) {
				binPath = GetValidPath ("$(MonoAndroidBinDirectory)", binPath, ValidateBin, () => FindBin (runtimePath));
				bclPath = GetValidPath ("mscorlib.dll", bclPath, ValidateFramework, () => FindFramework (runtimePath));
			} else {
				binPath = bclPath = null;
			}

			if (runtimePath == null || binPath == null || bclPath == null) {
				Reset ();
				return;
			}

			RuntimePath = runtimePath;
#pragma warning disable 0618
			SdkPath     = GetSdkPath (runtimePath);
#pragma warning restore 0618
			BinPath     = binPath;
			BclPath     = bclPath;
			LibrariesPath = FindLibraries (runtimePath);

			IncludePath = FindInclude (runtimePath);
			if (IncludePath != null && !Directory.Exists (IncludePath))
				IncludePath = null;

			SharedRuntimeVersion = GetCurrentSharedRuntimeVersion (runtimePath);
			AndroidVersions      = new AndroidVersions (new [] { BclPath });
		}

		static string GetValidPath (string description, string path, Func<string, bool> validator, Func<string> defaultPath)
		{
			if (!string.IsNullOrEmpty (path)) {
				if (Directory.Exists (path)) {
					if (validator (path)) {
						AndroidLogger.LogDebug ($"{description} path `{path}` is valid");
						return path;
					}
					AndroidLogger.LogInfo ($"{description} path `{path}` is not valid; skipping.");
				} else {
					AndroidLogger.LogDebug ($"{description} path `{path}` did not exist");
				}
			}
			path = defaultPath ();
			if (path != null && validator (path)) {
				AndroidLogger.LogDebug ($"{description} defaultPath `{path}` is valid");
				return path;
			}
			AndroidLogger.LogInfo ($"{description} defaultPath `{path}` is not valid; skipping.");
			return null;
		}

		public void Reset ()
		{
#pragma warning disable 0618
			SdkPath = BinPath = IncludePath = RuntimePath = BclPath = null;
#pragma warning restore 0618
			SharedRuntimeVersion = 0;

			AndroidVersions = null;
		}

		string GetSdkPath (string runtimePath)
		{
			var sdkPaths = new[]{
				// runtimePath=$prefix/lib/mandroid
				Path.GetFullPath (Path.Combine (runtimePath, "..", "..")),
				// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android
				//   we prefer $prefix/Version* over $prefix/lib/xamarin.android/xbuild/Xamarin/Android/Version*
				Path.GetFullPath (Path.Combine (runtimePath, "..", "..", "..", "..", "..")),
				// runtimePath=$prefix/lib/xamarin.android/xbuild/Xamarin/Android
				Path.GetFullPath (runtimePath),
				// runtimePath=/Library/Frameworks/Xamarin.Android.framework/Libraries/xbuild/Xamarin/Android
				//   Can possibly happen if we hit the `finalSearchPaths` value in MonoDroidSdkUnix.FindRuntime()
				//   See https://bugzilla.xamarin.com/show_bug.cgi?id=58776
				Path.GetFullPath (Path.Combine (runtimePath, "..", "..", "..", "..", "Versions", "Current")),
			};
			var versionFiles = new[]{
				"Version.txt",
				"Version",
			};
			foreach (var sdkPath in sdkPaths) {
				foreach (var version in versionFiles) {
					var path    = Path.Combine (sdkPath, version);
					bool exists = File.Exists (path);
					AndroidLogger.LogDebug ($"{nameof (GetSdkPath)} `{path}` exists={exists} ");
					if (exists)
						return sdkPath;
				}
			}
			return null;
		}

		protected abstract string FindRuntime ();
		protected abstract string FindFramework (string runtimePath);

		// Check for platform-specific `mandroid` name
		protected abstract bool ValidateBin (string binPath);

		protected static bool ValidateRuntime (string loc)
		{
			return !string.IsNullOrWhiteSpace (loc) &&
				File.Exists (Path.Combine (loc, "Xamarin.AndroidTools.dll"));
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
				bool exists = File.Exists (loc);
				AndroidLogger.LogDebug (null, $"FindVersionFile: {loc}, result={exists}");
				if (exists) {
					return loc;
				}
			}
			return null;
		}

		protected virtual IEnumerable<string> GetVersionFileLocations ()
		{
#pragma warning disable 0618
			yield return Path.Combine (SdkPath, "Version.txt");
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

		int GetCurrentSharedRuntimeVersion (string runtimePath)
		{
			string file = Path.Combine (runtimePath, "Mono.Android.DebugRuntime-debug.xml");

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
						AndroidLogger.LogInfo ($"Cannot parse runtime version code: ({r.Value})");
					}
				}
			} catch (Exception ex) {
				AndroidLogger.LogError ("Error trying to find shared runtime version", ex);
			}
			return int.MaxValue;
		}

		public IEnumerable<string> GetSupportedApiLevels ()
		{
			if (AndroidVersions != null) {
				return AndroidVersions.InstalledBindingVersions.Select (v => v.Id);
			}
			return new string [0];
		}

		public string GetApiLevelForFrameworkVersion (string framework)
		{
			return AndroidVersions?.GetIdFromFrameworkVersion (framework);
		}

		public string GetFrameworkVersionForApiLevel (string apiLevel)
		{
			// API level 9 was discontinued immediately for 10, in the rare case we get it just upgrade the number
			if (apiLevel == "9")
				apiLevel = "10";
			return AndroidVersions?.GetFrameworkVersionFromId (apiLevel);
		}

		/// <summary>
		/// Determines if the given apiLevel is supported by an installed Framework
		/// </summary>
		public bool IsSupportedFrameworkLevel (string apiLevel)
		{
			var id  = AndroidVersions?.GetIdFromApiLevel (apiLevel);
			return id != null && AndroidVersions.InstalledBindingVersions.Any (v => v.Id == id);
		}
	}
}
