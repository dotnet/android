using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xamarin.Android.Tools.VSWhere;

namespace Xamarin.ProjectTools
{
	public static class TestEnvironment
	{
		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		static bool IsDarwin ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					return os == "Darwin";
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}

		public static bool IsWindows {
			get {
				return Environment.OSVersion.Platform == PlatformID.Win32NT;
			}
		}

		public static bool IsMacOS {
			get {
				return IsDarwin ();
			}
		}

		public static bool IsLinux {
			get {
				return !IsWindows && !IsMacOS;
			}
		}

		static readonly string LocalMonoAndroidToolsDirectory = Path.Combine (XABuildPaths.PrefixDirectory, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");

		public static readonly string MacOSInstallationRoot = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current";

		static VisualStudioInstance visualStudioInstance;
		public static VisualStudioInstance GetVisualStudioInstance ()
		{
			//We should cache and reuse this value, so we don't run vswhere.exe so much
			if (visualStudioInstance != null && !string.IsNullOrEmpty (visualStudioInstance.VisualStudioRootPath))
				return visualStudioInstance;

			return visualStudioInstance = MSBuildLocator.QueryLatest ();
		}

		/// <summary>
		/// The MonoAndroid framework (and other reference assemblies) directory within a local build tree. Contains v1.0, v9.0, etc,
		/// e.g. xamarin-android/bin/Debug/lib/xamarin.android/xbuild-frameworks/MonoAndroid.<br/>
		/// If a local build tree can not be found, or if it is empty, this will return the system installation location instead:<br/>
		///	Windows:  C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\ReferenceAssemblies\Microsoft\Framework\MonoAndroid <br/>
		///	macOS:    Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/xamarin.android/xbuild-frameworks/MonoAndroid
		/// </summary>
		public static string MonoAndroidFrameworkDirectory {
			get {
				var frameworkLibDir = Path.Combine (XABuildPaths.PrefixDirectory, "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid");
				if (Directory.Exists (frameworkLibDir) && Directory.EnumerateDirectories (frameworkLibDir, "v*", SearchOption.TopDirectoryOnly).Any ())
					return frameworkLibDir;

				if (IsWindows) {
					VisualStudioInstance vs = GetVisualStudioInstance ();
					return Path.Combine (vs.VisualStudioRootPath, "Common7", "IDE", "ReferenceAssemblies", "Microsoft", "Framework", "MonoAndroid");
				} else {
					return Path.Combine (MacOSInstallationRoot, "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid");
				}
			}
		}

		/// <summary>
		/// The MonoAndroidTools directory within a local build tree, e.g. xamarin-android/bin/Debug/lib/xamarin.android/xbuild/Xamarin/Android.<br/>
		/// If a local build tree can not be found, or if it is empty, this will return the system installation or .NET sandbox location instead:<br/>
		///	Windows:  C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Xamarin\Android <br/>
		///	macOS:    /Library/Frameworks/Xamarin.Android.framework/Versions/Current/lib/xamarin.android/xbuild/Xamarin/Android<br/>
		///	Windows (dotnet):  bin\Debug\dotnet\packs\Microsoft.Android.Sdk.Windows\$(Latest)\tools<br/>
		///	macOS (dotnet):    bin/Debug/dotnet/packs/Microsoft.Android.Sdk.Darwin/$(Latest)/tools
		/// </summary>
		public static string AndroidMSBuildDirectory {
			get {
				if (Builder.UseDotNet) {
					if (!Directory.Exists (DotNetPreviewAndroidSdkDirectory)) {
						throw new DirectoryNotFoundException ($"Unable to locate a Microsoft.Android.Sdk in either '{DefaultPacksDir}' or '{LocalPacksDir}'.");
					}
					return Path.Combine (DotNetPreviewAndroidSdkDirectory, "tools");
				}

				if (UseLocalBuildOutput) {
					return LocalMonoAndroidToolsDirectory;
				} else {
					if (IsWindows) {
						VisualStudioInstance vs = GetVisualStudioInstance ();
						return Path.Combine (vs.VisualStudioRootPath, "MSBuild", "Xamarin", "Android");
					} else {
						return Path.Combine (MacOSInstallationRoot, "lib", "xamarin.android", "xbuild", "Xamarin", "Android");
					}
				}
			}
		}

		public static string DotNetPreviewDirectory => Path.Combine (XABuildPaths.PrefixDirectory, "dotnet");

		public static string DotNetPreviewPacksDirectory => UseLocalBuildOutput ? LocalPacksDir : DefaultPacksDir;

		public static string DotNetPreviewAndroidSdkDirectory => UseLocalBuildOutput ? LocalDotNetAndroidSdkDirectory : DefaultDotNetAndroidSdkDirectory;

		public static string WorkloadPackOverridePath => Path.Combine (XABuildPaths.PrefixDirectory, "lib");

		public static string WorkloadManifestOverridePath => Path.Combine (WorkloadPackOverridePath, "sdk-manifests");

		static string DefaultPacksDir => Path.Combine (DotNetPreviewDirectory, "packs");

		static string LocalPacksDir => Path.Combine (WorkloadPackOverridePath, "packs");

		static string _defaultDotNetAndroidSdkDirectory;
		static string DefaultDotNetAndroidSdkDirectory {
			get {
				if (!string.IsNullOrEmpty (_defaultDotNetAndroidSdkDirectory)) {
					return _defaultDotNetAndroidSdkDirectory;
				}
				return _defaultDotNetAndroidSdkDirectory = GetDotNetAndroidSdkDir (DefaultPacksDir);
			}
		}

		static string _localDotNetAndroidSdkDirectory;
		static string LocalDotNetAndroidSdkDirectory {
			get {
				if (!string.IsNullOrEmpty (_localDotNetAndroidSdkDirectory)) {
					return _localDotNetAndroidSdkDirectory;
				}
				return _localDotNetAndroidSdkDirectory = GetDotNetAndroidSdkDir (LocalPacksDir);
			}
		}

		static string GetDotNetAndroidSdkDir (string packsDirectory)
		{
			var sdkName = IsMacOS ? "Microsoft.Android.Sdk.Darwin" :
				IsWindows ? "Microsoft.Android.Sdk.Windows" :
				"Microsoft.Android.Sdk.Linux";

			var sdkDir = Path.Combine (packsDirectory, sdkName);
			if (!Directory.Exists (sdkDir))
				return string.Empty;

			var dirs = from d in Directory.GetDirectories (sdkDir)
				   let version = ParseVersion (d)
				   orderby version descending
				   select d;

			return dirs.FirstOrDefault ();
		}

		/// <summary>
		/// Tests will attempt to run against local build output directories by default,
		///  and fall back to `dotnet/packs` or a legacy system install location if a local build does not exist.
		/// This will always return false for our tests running in CI.
		/// </summary>
		public static bool UseLocalBuildOutput {
			get {
				var msbuildDir = Builder.UseDotNet ? Path.Combine (LocalDotNetAndroidSdkDirectory, "tools") : LocalMonoAndroidToolsDirectory;
				return Directory.Exists (msbuildDir) && File.Exists (Path.Combine (msbuildDir, "Xamarin.Android.Build.Tasks.dll"));
			}
		}

		public static bool CommercialBuildAvailable => File.Exists (Path.Combine (AndroidMSBuildDirectory, "Xamarin.Android.Common.Debugging.targets"));

		public static string OSBinDirectory {
			get {
				var osSubdirName = IsMacOS ? "Darwin" :
					IsLinux ? "Linux" :
					string.Empty;
				return Path.Combine (AndroidMSBuildDirectory, osSubdirName);
			}
		}

		static Version ParseVersion (string path)
		{
			var folderName = Path.GetFileName (path);
			var index = folderName.IndexOf ('-');
			if (index != -1) {
				folderName = folderName.Substring (0, index);
			}
			if (Version.TryParse (folderName, out var v))
				return v;
			return new Version (0, 0);
		}

		public static bool IsUsingJdk8 => AndroidSdkResolver.GetJavaSdkVersionString ().Contains ("1.8.0");

		public static bool IsUsingJdk11 => AndroidSdkResolver.GetJavaSdkVersionString ().Contains ("11.0");
	}
}

