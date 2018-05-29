using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Xamarin.Android.Build
{
	/// <summary>
	/// Various paths needed by xabuild.exe
	/// </summary>
	class XABuildPaths
	{
		public bool IsWindows { get; private set; }

		public bool IsMacOS { get; private set; }

		public bool IsLinux { get; private set; }

		/// <summary>
		/// Directory to xabuild.exe
		/// </summary>
		public string XABuildDirectory { get; private set; }

		/// <summary>
		/// Path to xabuild.exe's config file, this is now a temporary file based on MSBuildExeTempPath
		/// </summary>
		public string XABuildConfig { get; private set; }

		/// <summary>
		/// The build output directory of Xamarin.Android, which is a submodule in this repo. Assumes it is already built.
		/// </summary>
		public string XamarinAndroidBuildOutput { get; private set; }

		/// <summary>
		/// $(VsInstallRoot), normally C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise
		/// </summary>
		public string VsInstallRoot { get; private set; }

		/// <summary>
		/// Path to MSBuild directory
		/// </summary>
		public string MSBuildPath { get; private set; }

		/// <summary>
		/// Path to directory of MSBuild.exe
		/// </summary>
		public string MSBuildBin { get; private set; }

		/// <summary>
		/// Temporary file used for MSBUILD_EXE_PATH
		/// </summary>
		public string MSBuildExeTempPath { get; private set; }

		/// <summary>
		/// Path to MSBuild's App.config file
		/// </summary>
		public string MSBuildConfig { get; private set; }

		/// <summary>
		/// Path to the system directory containing .NETPortable and .NETFramework
		/// </summary>
		public string SystemProfiles { get; private set; }

		/// <summary>
		/// Our default $(MSBuildExtensionPath) which should be the "xbuild" directory in the Xamarin.Android build output
		/// </summary>
		public string MSBuildExtensionsPath { get; private set; }

		/// <summary>
		/// Used as the MSBuildSDKsPath environment variable, required for .NET standard projects to build
		/// </summary>
		public string MSBuildSdksPath { get; private set; }

		/// <summary>
		/// Array of search paths for MSBuildExtensionsPath
		/// </summary>
		public string [] ProjectImportSearchPaths { get; private set; }

		/// <summary>
		/// The xbuild-frameworks directory inside the Xamarin.Android build output
		/// </summary>
		public string FrameworksDirectory { get; private set; }

		/// <summary>
		/// Search paths for MSBuildExtensionsPath are specified by an "os" attribute
		/// NOTE: Values are "windows", "osx", or "unix"
		/// </summary>
		public string SearchPathsOS { get; set; }

		public string MonoAndroidToolsDirectory { get; private set; }

		public string AndroidSdkDirectory { get; private set; }

		public string AndroidNdkDirectory { get; private set; }

		public string DotNetSdkPath { get; private set; }

		public string NuGetTargets { get; private set; }

		public string NuGetProps { get; private set; }

		public string NuGetRestoreTargets { get; private set; }

		public XABuildPaths ()
		{
			IsWindows                 = Environment.OSVersion.Platform == PlatformID.Win32NT;
			IsMacOS                   = !IsWindows && IsDarwin ();
			IsLinux                   = !IsWindows && !IsMacOS;
			XABuildDirectory          = Path.GetDirectoryName (GetType ().Assembly.Location);
			XamarinAndroidBuildOutput = Path.GetFullPath (Path.Combine (XABuildDirectory, ".."));

			const string vsVersion    = "15.0";
			string programFiles       = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
			string prefix             = Path.Combine (XamarinAndroidBuildOutput, "lib", "xamarin.android");

			if (IsWindows) {
				foreach (var edition in new [] { "Enterprise", "Professional", "Community", "BuildTools" }) {
					var vsInstall = Path.Combine (programFiles, "Microsoft Visual Studio", "2017", edition);
					if (Directory.Exists (vsInstall)) {
						VsInstallRoot = vsInstall;
						break;
					}
				}
				if (VsInstallRoot == null)
					VsInstallRoot = programFiles;

				MSBuildPath              = Path.Combine (VsInstallRoot, "MSBuild");
				MSBuildBin               = Path.Combine (MSBuildPath, vsVersion, "Bin");
				MSBuildConfig            = Path.Combine (MSBuildBin, "MSBuild.exe.config");
				DotNetSdkPath            = FindLatestDotNetSdk (Path.Combine (Environment.GetEnvironmentVariable ("ProgramW6432"), "dotnet", "sdk"));
				MSBuildSdksPath          = DotNetSdkPath ?? Path.Combine (MSBuildPath, "Sdks");
				ProjectImportSearchPaths = new [] { MSBuildPath, "$(MSBuildProgramFiles32)\\MSBuild" };
				SystemProfiles           = Path.Combine (programFiles, "Reference Assemblies", "Microsoft", "Framework");
				SearchPathsOS            = "windows";
				string nuget             = Path.Combine (MSBuildPath, "Microsoft", "NuGet", vsVersion);
				NuGetProps               = Path.Combine (nuget, "Microsoft.NuGet.props");
				NuGetTargets             = Path.Combine (nuget, "Microsoft.NuGet.targets");
				NuGetRestoreTargets      = Path.Combine (VsInstallRoot, "Common7", "IDE", "CommonExtensions", "Microsoft", "NuGet", "NuGet.targets");
			} else {
				string mono              = IsMacOS ? "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono" : "/usr/lib/mono";
				string monoExternal      = IsMacOS ? "/Library/Frameworks/Mono.framework/External/" : "/usr/lib/mono";
				MSBuildPath              = Path.Combine (mono, "msbuild");
				MSBuildBin               = Path.Combine (MSBuildPath, vsVersion, "bin");
				MSBuildConfig            = Path.Combine (MSBuildBin, "MSBuild.dll.config");
				DotNetSdkPath            = FindLatestDotNetSdk ("/usr/local/share/dotnet/sdk");
				MSBuildSdksPath          = DotNetSdkPath ?? Path.Combine (MSBuildBin, "Sdks");
				ProjectImportSearchPaths = new [] { MSBuildPath, Path.Combine (mono, "xbuild"), Path.Combine (monoExternal, "xbuild") };
				SystemProfiles           = Path.Combine (mono, "xbuild-frameworks");
				SearchPathsOS            = IsMacOS ? "osx" : "unix";

				string nuget = Path.Combine (mono, "xbuild", "Microsoft", "NuGet");
				if (Directory.Exists (nuget)) {
					NuGetTargets = Path.Combine (nuget, "Microsoft.NuGet.targets");
					NuGetProps   = Path.Combine (nuget, "Microsoft.NuGet.props");
				}
				NuGetRestoreTargets = Path.Combine (MSBuildBin, "NuGet.targets");
				if (!File.Exists (NuGetRestoreTargets) && !string.IsNullOrEmpty (DotNetSdkPath)) {
					NuGetRestoreTargets  = Path.Combine (DotNetSdkPath, "..", "NuGet.targets");
				}
			}

			FrameworksDirectory       = Path.Combine (prefix, "xbuild-frameworks");
			MSBuildExtensionsPath     = Path.Combine (prefix, "xbuild");
			MonoAndroidToolsDirectory = Path.Combine (prefix, "xbuild", "Xamarin", "Android");
			MSBuildExeTempPath        = Path.GetTempFileName ();
			XABuildConfig             = MSBuildExeTempPath + ".config";

			//Android SDK and NDK
			var pathsTargets = Path.Combine (XABuildDirectory, "..", "..", "..", "build-tools", "scripts", "Paths.targets");
			if (File.Exists (pathsTargets)) {
				var androidSdkPath  = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
				if (string.IsNullOrEmpty (androidSdkPath)) {
					androidSdkPath  = RunPathsTargets (pathsTargets, "GetAndroidSdkFullPath");
				}
				AndroidSdkDirectory = androidSdkPath;

				var androidNdkPath  = Environment.GetEnvironmentVariable ("ANDROID_NDK_PATH");
				if (string.IsNullOrEmpty (androidNdkPath)) {
					androidNdkPath  = RunPathsTargets (pathsTargets, "GetAndroidNdkFullPath");
				}
				AndroidNdkDirectory = androidNdkPath;
			}
		}

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

		string RunPathsTargets (string pathsTargets, string target)
		{
			var path = IsWindows ? Path.Combine(MSBuildBin, "MSBuild.exe") : "mono";
			var args = $"/nologo /v:minimal /t:{target} \"{pathsTargets}\"";
			if (!IsWindows) {
				args = $"\"{Path.Combine (MSBuildBin, "MSBuild.dll")}\" {args}";
			}

			var psi = new ProcessStartInfo (path, args) {
				CreateNoWindow         = true,
				RedirectStandardOutput = true,
				WindowStyle            = ProcessWindowStyle.Hidden,
				UseShellExecute        = false,
			};

			using (var p = Process.Start (psi)) {
				p.WaitForExit ();
				return p.StandardOutput.ReadToEnd ().Trim ();
			}
		}

		string FindLatestDotNetSdk(string dotNetPath)
		{
			if (Directory.Exists(dotNetPath)) {
				var directories = from dir in Directory.EnumerateDirectories (dotNetPath)
				                  let version = GetVersionFromDirectory (dir)
				                  where version != null
				                  orderby version descending
				                  select Path.Combine (dir, "Sdks");
				return directories.FirstOrDefault ();
			}
			return null;
		}

		static Version GetVersionFromDirectory(string dir)
		{
			Version v;
			Version.TryParse (Path.GetFileName (dir), out v);
			return v;
		}
	}
}
