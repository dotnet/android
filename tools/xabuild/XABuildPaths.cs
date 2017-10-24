using System;
using System.IO;
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

		public XABuildPaths ()
		{
			IsWindows                 = Environment.OSVersion.Platform == PlatformID.Win32NT;
			IsMacOS                   = !IsWindows && IsDarwin ();
			IsLinux                   = !IsWindows && !IsMacOS;
			XABuildDirectory          = Path.GetDirectoryName (GetType ().Assembly.Location);
			XamarinAndroidBuildOutput = Path.GetFullPath (Path.Combine (XABuildDirectory, ".."));

			string programFiles       = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
			string userProfile        = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
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
				MSBuildBin               = Path.Combine (MSBuildPath, "15.0", "Bin");
				MSBuildConfig            = Path.Combine (MSBuildBin, "MSBuild.exe.config");
				ProjectImportSearchPaths = new [] { MSBuildPath, "$(MSBuildProgramFiles32)\\MSBuild" };
				SystemProfiles           = Path.Combine (programFiles, "Reference Assemblies", "Microsoft", "Framework");
				SearchPathsOS            = "windows";
			} else {
				string mono              = IsMacOS ? "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono" : "/usr/lib/mono";
				string monoExternal      = IsMacOS ? "/Library/Frameworks/Mono.framework/External/" : "/usr/lib/mono";
				MSBuildPath              = Path.Combine (mono, "msbuild");
				MSBuildBin               = Path.Combine (MSBuildPath, "15.0", "bin");
				MSBuildConfig            = Path.Combine (MSBuildBin, "MSBuild.dll.config");
				ProjectImportSearchPaths = new [] { MSBuildPath, Path.Combine (mono, "xbuild"), Path.Combine (monoExternal, "xbuild") };
				SystemProfiles           = Path.Combine (mono, "xbuild-frameworks");
				SearchPathsOS            = IsMacOS ? "osx" : "unix";
			}

			FrameworksDirectory       = Path.Combine (prefix, "xbuild-frameworks");
			MSBuildExtensionsPath     = Path.Combine (prefix, "xbuild");
			MonoAndroidToolsDirectory = Path.Combine (prefix, "xbuild", "Xamarin", "Android");
			AndroidSdkDirectory       = Path.Combine (userProfile, "android-toolchain", "sdk");
			AndroidNdkDirectory       = Path.Combine (userProfile, "android-toolchain", "ndk");
			MSBuildExeTempPath        = Path.GetTempFileName ();
			XABuildConfig             = MSBuildExeTempPath + ".config";
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
	}
}
