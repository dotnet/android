using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xamarin.Android.Tools;

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

		/// <summary>
		/// The MonoAndroid reference assemblies directory within a local build tree, e.g. bin/Debug/lib/packs/Microsoft.Android.Ref.34/34.99.0/ref/net8.0/<br/>
		/// If a local build tree can not be found, or if it is empty, this will return the system installation location instead:<br/>
		///    bin/Debug/dotnet/packs/Microsoft.Android.Ref.34/$(Latest)/ref/net$(Latest)/
		/// </summary>
		public static string MonoAndroidFrameworkDirectory {
			get {
				var rootRefDir = Directory.GetDirectories (Path.Combine (DotNetPreviewPacksDirectory, $"Microsoft.Android.Ref.{XABuildConfig.AndroidDefaultTargetDotnetApiLevel}")).LastOrDefault ();
				if (!Directory.Exists (rootRefDir)) {
					throw new DirectoryNotFoundException ($"Unable to locate Microsoft.Android.Ref.");
				}

				var maDll = Directory.GetFiles (rootRefDir, "Mono.Android.dll", SearchOption.AllDirectories).LastOrDefault ();
				var refDir = Path.GetDirectoryName(maDll);
				if (!Directory.Exists (refDir)) {
					throw new DirectoryNotFoundException ($"Unable to locate Mono.Android.dll inside Microsoft.Android.Ref.");
				}

				return refDir;
			}
		}

		/// <summary>
		/// The MonoAndroidTools directory within a local build tree, e.g. bin/Debug/lib/packs/Microsoft.Android.Sdk.Darwin/34.99.0/tools/<br/>
		/// If a local build tree can not be found, or if it is empty, this will return the .NET sandbox location instead:<br/>
		///	Windows:  bin\Debug\dotnet\packs\Microsoft.Android.Sdk.Windows\$(Latest)\tools<br/>
		///	macOS:    bin/Debug/dotnet/packs/Microsoft.Android.Sdk.Darwin/$(Latest)/tools
		/// </summary>
		public static string AndroidMSBuildDirectory {
			get {
				if (!Directory.Exists (DotNetPreviewAndroidSdkDirectory)) {
					throw new DirectoryNotFoundException ($"Unable to locate a Microsoft.Android.Sdk in either '{DefaultPacksDir}' or '{LocalPacksDir}'.");
				}
				return Path.Combine (DotNetPreviewAndroidSdkDirectory, "tools");
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
		///  and fall back to `dotnet/packs` if a local build does not exist.
		/// This will always return false for our tests running in CI.
		/// </summary>
		public static bool UseLocalBuildOutput {
			get {
				var msbuildDir = Path.Combine (LocalDotNetAndroidSdkDirectory, "tools");
				return Directory.Exists (msbuildDir) && File.Exists (Path.Combine (msbuildDir, "Xamarin.Android.Build.Tasks.dll"));
			}
		}

		public static bool CommercialBuildAvailable {
			get {
				Console.WriteLine ("DEBUG! checking `{AndroidMSBuildDirectory}` for `Xamarin.Android.Common.Debugging.targets`.")
				return File.Exists (Path.Combine (AndroidMSBuildDirectory, "Xamarin.Android.Common.Debugging.targets"));
			}
		}

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

