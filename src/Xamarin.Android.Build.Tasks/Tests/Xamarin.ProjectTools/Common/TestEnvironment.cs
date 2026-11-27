using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Provides utility methods and properties for detecting the test environment
	/// and locating build tools, directories, and platform-specific settings.
	/// </summary>
	/// <remarks>
	/// This class centralizes environment detection logic used throughout the test
	/// framework to adapt behavior based on the operating system, locate build tools,
	/// and configure platform-specific paths and settings.
	/// </remarks>
	/// <seealso cref="XABuildPaths"/>
	/// <seealso cref="FileSystemUtils"/>
	public static class TestEnvironment
	{
		/// <summary>
		/// The dnceng <c>dotnet-public-maven</c> feed, which mirrors both Maven Central and
		/// Google Maven and is readable anonymously.
		/// </summary>
		/// <remarks>
		/// Tests download through this feed unconditionally, including locally, so that a local
		/// run exercises exactly the same URLs as CI. CI agents are network-isolated and can only
		/// reach this feed, so an artifact the mirror does not have must fail everywhere rather
		/// than only on CI.
		/// </remarks>
		public const string DotNetPublicMaven = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public-maven/maven/v1";

		/// <safety>
		/// The caller must supply writable storage large enough for the native utsname structure.
		/// </safety>
		[DllImport ("libc")]
		static unsafe extern int uname (IntPtr buf);

		static bool IsDarwin ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// SAFETY: The live 8192-byte allocation exceeds utsname on the supported Unix platforms.
				if (unsafe (uname (buf)) == 0) {
					// SAFETY: Successful uname writes a NUL-terminated system name into the live allocation.
					string os = unsafe (Marshal.PtrToStringAnsi (buf));
					return os == "Darwin";
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero) {
					// SAFETY: This is the allocation owned by this invocation, and it has not been freed.
					unsafe {
						Marshal.FreeHGlobal (buf);
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Gets a value indicating whether the current platform is Windows.
		/// </summary>
		/// <remarks>
		/// <see cref="SupportedOSPlatformGuardAttribute"/> teaches the platform-compatibility
		/// analyzer (CA1416) that this property is a <c>windows</c> guard, so callers can use it
		/// instead of <see cref="OperatingSystem.IsWindows"/>.
		/// </remarks>
		[SupportedOSPlatformGuard ("windows")]
		public static bool IsWindows {
			get {
				return Environment.OSVersion.Platform == PlatformID.Win32NT;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current platform is macOS.
		/// </summary>
		[SupportedOSPlatformGuard ("macos")]
		public static bool IsMacOS {
			get {
				return IsDarwin ();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current platform is Linux.
		/// </summary>
		[SupportedOSPlatformGuard ("linux")]
		public static bool IsLinux {
			get {
				return !IsWindows && !IsMacOS;
			}
		}

		public static bool IsRunningOnCI =>
			string.Equals (Environment.GetEnvironmentVariable ("RUNNINGONCI"), "true", StringComparison.OrdinalIgnoreCase);

		/// <summary>
		/// The MonoAndroid reference assemblies directory in the installed Microsoft.Android.[net].Ref.[API] library pack, e.g.
		///    microsoft.android.net12.0.ref.37.0.37.99.0/ref/net12.0-android37.0/
		/// </summary>
		public static string MonoAndroidFrameworkDirectory {
			get {
				var targetPlatform = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;
				var platformId = $"{targetPlatform.Major}.{targetPlatform.Minor}";
				var targetFramework = XABuildConfig.LatestDotNetTargetFramework;
				var refDir = Path.Combine (GetLibraryPackDirectory ($"Microsoft.Android.{targetFramework}.Ref.{platformId}"), "ref", $"{targetFramework}-android{platformId}");
				if (!File.Exists (Path.Combine (refDir, "Mono.Android.dll"))) {
					throw new DirectoryNotFoundException ($"Unable to locate Mono.Android.dll in '{refDir}'.");
				}
				return refDir;
			}
		}

		/// <summary>
		/// The tools directory of the installed Microsoft.Android.[net] library pack, which contains the MSBuild tasks.
		/// </summary>
		public static string AndroidMSBuildDirectory => Path.Combine (DotNetPreviewAndroidSdkDirectory, "tools");

		public static string DotNetPreviewDirectory => Path.Combine (XABuildPaths.PrefixDirectory, "dotnet");

		/// <summary>
		/// The installed Microsoft.Android.[net] library pack, which contains the MSBuild targets, tasks, and native runtimes.
		/// </summary>
		public static string DotNetPreviewAndroidSdkDirectory => GetLibraryPackDirectory ($"Microsoft.Android.{XABuildConfig.LatestDotNetTargetFramework}");

		public static string WorkloadPackOverridePath => Path.Combine (XABuildPaths.PrefixDirectory, "lib");

		public static string WorkloadManifestOverridePath => Path.Combine (WorkloadPackOverridePath, "sdk-manifests");

		static string LibraryPacksDirectory => Path.Combine (DotNetPreviewDirectory, "library-packs");

		static readonly object libraryPacksLock = new object ();

		/// <summary>
		/// Extracts an installed library pack, dotnet/library-packs/[id].[version].nupkg, and returns its directory.
		/// </summary>
		public static string GetLibraryPackDirectory (string packageId)
		{
			var prefix = packageId.ToLowerInvariant () + ".";
			var nupkg = Directory.EnumerateFiles (LibraryPacksDirectory, prefix + "*.nupkg")
				.SingleOrDefault (path => {
					var fileName = Path.GetFileName (path);
					return fileName.Length > prefix.Length && char.IsDigit (fileName [prefix.Length]);
				});
			if (nupkg == null) {
				throw new FileNotFoundException ($"Unable to locate the '{packageId}' library pack in '{LibraryPacksDirectory}'.");
			}
			// Local builds reinstall library packs with the same version, so include the file time.
			var directory = Path.Combine (XABuildPaths.TestOutputDirectory, "library-packs", $"{Path.GetFileNameWithoutExtension (nupkg)}-{File.GetLastWriteTimeUtc (nupkg).Ticks}");
			var marker = Path.Combine (directory, ".extracted");
			lock (libraryPacksLock) {
				if (!File.Exists (marker)) {
					if (Directory.Exists (directory)) {
						FileSystemUtils.DeleteDirectoryWithRetry (directory);
					}
					ZipFile.ExtractToDirectory (nupkg, directory);
					// ZipFile does not keep the execute permission (for example tools/aapt2), so set it like NuGet does
					if (!OperatingSystem.IsWindows ()) {
						foreach (var file in Directory.EnumerateFiles (directory, "*", SearchOption.AllDirectories)) {
							File.SetUnixFileMode (file, File.GetUnixFileMode (file) | UnixFileMode.UserExecute);
						}
					}
					File.WriteAllText (marker, "");
				}
			}
			return directory;
		}

		/// <summary>
		/// Tests use the workload manifest from a local build tree by default, see dotnet-local.sh.
		/// This will always return false for our tests running in CI.
		/// </summary>
		public static bool UseLocalBuildOutput =>
			Directory.Exists (WorkloadManifestOverridePath) &&
			Directory.EnumerateDirectories (WorkloadManifestOverridePath, "microsoft.net.sdk.android", SearchOption.AllDirectories).Any ();

		/// <summary>
		/// The directory with aapt2, in the installed Microsoft.Android.Tools.[OS] library pack.
		/// </summary>
		public static string OSBinDirectory {
			get {
				var hostOS = IsMacOS ? "Darwin" :
					IsLinux ? "Linux" :
					"Windows";
				return Path.Combine (GetLibraryPackDirectory ($"Microsoft.Android.Tools.{hostOS}"), "tools");
			}
		}

		public static bool IsUsingJdk8 => AndroidSdkResolver.GetJavaSdkVersionString ().Contains ("1.8.0");

		public static bool IsUsingJdk11 => AndroidSdkResolver.GetJavaSdkVersionString ().Contains ("11.0");
	}
}
