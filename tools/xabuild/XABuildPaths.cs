using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xamarin.Android.Tools.VSWhere;

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
		/// Temporary directory used for MSBUILD_EXE_PATH
		/// </summary>
		public string MSBuildTempPath { get; private set; }

		/// <summary>
		/// Temporary file used for MSBUILD_EXE_PATH
		/// </summary>
		public string MSBuildExeTempPath { get; private set; }

		/// <summary>
		/// Config file needed for Microsoft.Build.NuGetSdkResolver.dll to work
		/// </summary>
		public string SdkResolverConfigPath { get; private set; }

		/// <summary>
		/// Full path to the system Microsoft.Build.NuGetSdkResolver.dll
		/// </summary>
		public string NuGetSdkResolverPath { get; private set; }

		/// <summary>
		/// Path to MSBuild's App.config file
		/// </summary>
		public string MSBuildConfig { get; private set; }

		/// <summary>
		/// Path to the system directory containing .NETPortable and .NETFramework
		/// </summary>
		public string SystemFrameworks { get; private set; }

		/// <summary>
		/// Path to the system directory containing .NET Framework assembly directories (e.g. `4.7.2-api`) on macOS.
		/// The .NETFramework directories identified in <see cref="SystemFrameworks"/> redirect to this location.
		/// </summary>
		public string MonoSystemFrameworkRoot { get; private set; }

		/// <summary>
		/// Path to the system directories for MSBuild targets, such as 15.0 and Microsoft, under $(MSBuildExtensionsPath) to be merged with in-tree MSBuildExtensionsPath
		/// </summary>
		public string [] SystemTargetsDirectories { get; private set; }

		/// <summary>
		/// Used as the MSBuildSDKsPath environment variable, required for .NET standard projects to build
		/// </summary>
		public string MSBuildSdksPath { get; private set; }

		/// <summary>
		/// Our default $(MSBuildExtensionPath) which should be the "xbuild" directory in the Xamarin.Android build output
		/// </summary>
		public string MSBuildExtensionsPath { get; private set; }

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

		/// <summary>
		/// The directory containing Microsoft.CSharp.Core.Targets
		/// 
		/// In VS 2017 and 2019, this would be: %VsInstallDir%\MSBuild\15.0\Bin\Roslyn
		/// </summary>
		public string RoslynTargetsPath { get; private set; }

		public XABuildPaths ()
		{
			IsWindows                 = Environment.OSVersion.Platform == PlatformID.Win32NT;
			IsMacOS                   = !IsWindows && IsDarwin ();
			IsLinux                   = !IsWindows && !IsMacOS;
			XABuildDirectory          = Path.GetDirectoryName (GetType ().Assembly.Location);
			XamarinAndroidBuildOutput = Path.GetFullPath (Path.Combine (XABuildDirectory, ".."));

			string programFiles       = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
			string prefix             = Path.Combine (XamarinAndroidBuildOutput, "lib", "xamarin.android");

			string DOTNET_ROOT        = Environment.GetEnvironmentVariable ("DOTNET_ROOT");
			string dotnetRootSdkDir   = DOTNET_ROOT == null ? null : Path.Combine (DOTNET_ROOT, "sdk");

			if (IsWindows) {
				var instance = MSBuildLocator.QueryLatest (includePreRelease: true);
				VsInstallRoot = instance.VisualStudioRootPath;

				MSBuildPath              = Path.Combine (VsInstallRoot, "MSBuild");
				MSBuildBin               = Path.GetDirectoryName (instance.MSBuildPath);
				MSBuildConfig            = Path.Combine (MSBuildBin, "MSBuild.exe.config");
				DotNetSdkPath            = FindLatestDotNetSdk (Path.Combine (Environment.GetEnvironmentVariable ("ProgramW6432"), "dotnet", "sdk"), dotnetRootSdkDir);
				MSBuildSdksPath          = DotNetSdkPath ?? Path.Combine (MSBuildPath, "Sdks");
				SystemFrameworks         = Path.Combine (programFiles, "Reference Assemblies", "Microsoft", "Framework");
				string msbuildDir        = Path.GetDirectoryName (MSBuildBin);
				SystemTargetsDirectories = new [] {
					msbuildDir,
					Path.Combine (MSBuildPath, "Microsoft"),
					Path.Combine (programFiles, "MSBuild", "Microsoft"),
				};
				SearchPathsOS            = "windows";
				string nuget             = Path.Combine (MSBuildPath, "Microsoft", "NuGet", "17.0");
				if (!Directory.Exists (nuget)) {
					nuget = Path.Combine (MSBuildPath, "Microsoft", "NuGet", "16.0");
				}
				NuGetProps               = Path.Combine (nuget, "Microsoft.NuGet.props");
				NuGetTargets             = Path.Combine (nuget, "Microsoft.NuGet.targets");
				var nugetDirectory       = Path.Combine (VsInstallRoot, "Common7", "IDE", "CommonExtensions", "Microsoft", "NuGet");
				NuGetRestoreTargets      = Path.Combine (nugetDirectory, "NuGet.targets");
				NuGetSdkResolverPath     = Path.Combine (nugetDirectory, "Microsoft.Build.NuGetSdkResolver.dll");
			} else {
				string[] vsVersions       = new [] {"Current", "15.0"};
				string mono              = IsMacOS ? "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono" : "/usr/lib/mono";
				string monoExternal      = IsMacOS ? "/Library/Frameworks/Mono.framework/External/" : "/usr/lib/mono";
				MSBuildPath              = Path.Combine (mono, "msbuild");

				MSBuildBin = null;
				foreach (string vsVersion in vsVersions) {
					MSBuildBin = Path.Combine (MSBuildPath, vsVersion, "bin");
					if (Directory.Exists (MSBuildBin))
						break;
				}
				if (string.IsNullOrEmpty (MSBuildBin))
					throw new InvalidOperationException ("Unable to locate MSBuild binaries directory");

				MSBuildConfig            = Path.Combine (MSBuildBin, "MSBuild.dll.config");
				DotNetSdkPath            = FindLatestDotNetSdk ("/usr/local/share/dotnet/sdk", dotnetRootSdkDir);
				MSBuildSdksPath          = DotNetSdkPath ?? Path.Combine (MSBuildBin, "Sdks");
				SystemFrameworks         = Path.Combine (mono, "xbuild-frameworks");
				MonoSystemFrameworkRoot  = mono;

				var systemTargetDirs = new List <string> ();
				foreach (string vsVersion in vsVersions) {
					string xbuildDir = Path.Combine (mono, "xbuild", vsVersion);
					if (!Directory.Exists (xbuildDir))
						continue;
					systemTargetDirs.Add (xbuildDir);
				}
				if (systemTargetDirs.Count == 0)
					throw new InvalidOperationException ("Unable to locate xbuild directory");
				systemTargetDirs.Add (Path.Combine (mono, "xbuild", "Microsoft"));
				SystemTargetsDirectories = systemTargetDirs.ToArray ();
				SearchPathsOS            = IsMacOS ? "osx" : "unix";

				string nuget = Path.Combine (mono, "xbuild", "Microsoft", "NuGet");
				if (Directory.Exists (nuget)) {
					NuGetTargets = Path.Combine (nuget, "Microsoft.NuGet.targets");
					NuGetProps   = Path.Combine (nuget, "Microsoft.NuGet.props");
				}
				NuGetRestoreTargets = Path.Combine (MSBuildBin, "NuGet.targets");
				NuGetSdkResolverPath = Path.Combine (MSBuildBin, "Microsoft.Build.NuGetSdkResolver.dll");
				if (!File.Exists (NuGetRestoreTargets) && !string.IsNullOrEmpty (DotNetSdkPath)) {
					NuGetRestoreTargets = Path.Combine (DotNetSdkPath, "..", "NuGet.targets");
					NuGetSdkResolverPath = Path.Combine (DotNetSdkPath, "..", "Microsoft.Build.NuGetSdkResolver.dll");
				}
			}

			FrameworksDirectory       = Path.Combine (prefix, "xbuild-frameworks");
			MSBuildExtensionsPath     = Path.Combine (prefix, "xbuild");
			MonoAndroidToolsDirectory = Path.Combine (prefix, "xbuild", "Xamarin", "Android");
			MSBuildTempPath           = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			MSBuildExeTempPath        = Path.Combine (MSBuildTempPath, Path.GetRandomFileName ());
			SdkResolverConfigPath     = Path.Combine (MSBuildTempPath, "SdkResolvers", "Microsoft.Build.NuGetSdkResolver", "Microsoft.Build.NuGetSdkResolver.xml");
			XABuildConfig             = MSBuildExeTempPath + ".config";

			var roslyn = Path.Combine (MSBuildBin, "Roslyn");
			if (Directory.Exists (roslyn)) {
				RoslynTargetsPath = roslyn;
			} else {
				//NOTE: this codepath happens with VS 2019, Roslyn is located in a 15.0 directory...
				roslyn = Path.Combine (MSBuildPath, "15.0", "Bin", "Roslyn");
				if (Directory.Exists (roslyn))
					RoslynTargetsPath = roslyn;
			}

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

		string FindLatestDotNetSdk (params string[] dotNetPaths)
		{
			Version latest  = new Version (0,0);
			string Sdk      = null;

			foreach (var dotNetPath in dotNetPaths) {
				if (!Directory.Exists (dotNetPath))
					continue;
				foreach (var dir in Directory.EnumerateDirectories (dotNetPath)) {
					var version = GetVersionFromDirectory (dir);
					var sdksDir = Path.Combine (dir, "Sdks");
					if (!Directory.Exists (sdksDir))
						sdksDir = Path.Combine (dir, "bin", "Sdks");
					if (version != null && version > latest) {
						// Mono does not yet support MSBuild 16.8 and .NET 5+.  If we want xabuild to be aware of .NET 5+ in the future, 
						// we will need to workaround the fact that the .NET 5 targets now require a version of `NuGet.Frameworks.dll` next to `MSBuildExeTempPath`:
						// https://github.com/dotnet/msbuild/blob/755d4d1e3d2a89f72f659fc3d7d2933cab619828/src/Build/Utilities/NuGetFrameworkWrapper.cs#L32
						if (version >= new Version (5, 0, 100)) {
							continue;
						}
						if (Directory.Exists (sdksDir) && File.Exists (Path.Combine (sdksDir, "Microsoft.NET.Sdk", "Sdk", "Sdk.props"))) {
							latest = version;
							Sdk = sdksDir;
						}
					}
				}
			}
			return Sdk;
		}

		static Version GetVersionFromDirectory(string dir)
		{
			Version v;
			Version.TryParse (Path.GetFileName (dir), out v);
			return v;
		}
	}
}
