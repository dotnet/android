using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Text;
using System.Collections.Generic;

namespace Xamarin.ProjectTools
{
	public class Builder : IDisposable
	{
		const string fixed_osx_xbuild_path = "/Library/Frameworks/Mono.framework/Commands";
		const string fixed_linux_xbuild_path = "/usr/bin";
		const string xbuildapp = "xbuild";
		const string msbuildapp = "msbuild";

		public bool IsUnix { get; set; }
		public bool RunningMSBuild { get; set; }
		public LoggerVerbosity Verbosity { get; set; }
		public string LastBuildOutput { get; set; }
		public TimeSpan LastBuildTime { get; protected set; }
		public string BuildLogFile { get; set; }
		public bool ThrowOnBuildFailure { get; set; }

		string GetUnixBuildExe ()
		{
			RunningMSBuild = false;
			var tooldir = Directory.Exists (fixed_osx_xbuild_path) ? fixed_osx_xbuild_path : fixed_linux_xbuild_path;
			string path = Path.Combine (tooldir, xbuildapp);
			if (!string.IsNullOrEmpty (Environment.GetEnvironmentVariable ("USE_MSBUILD"))) {
				path = Path.Combine (tooldir, msbuildapp);
				RunningMSBuild = true;
			}
			return File.Exists (path) ? path : msbuildapp;
		}

		string GetVisualStudio2017Directory ()
		{
			var editions = new [] {
				"Enterprise",
				"Professional",
				"Community",
				"BuildTools"
			};

			var x86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
			foreach (var edition in editions) {
				var dir = Path.Combine (x86, "Microsoft Visual Studio", "2017", edition);
				if (Directory.Exists (dir))
					return dir;
			}

			return null;
		}

		string GetWindowsBuildExe ()
		{
			RunningMSBuild = true;

			//First try environment variable
			string msbuildExe = Environment.GetEnvironmentVariable ("XA_MSBUILD_EXE");
			if (!string.IsNullOrEmpty (msbuildExe) && File.Exists (msbuildExe))
				return msbuildExe;

			//Next try VS 2017, MSBuild 15.0
			var visualStudioDirectory = GetVisualStudio2017Directory ();
			if (!string.IsNullOrEmpty(visualStudioDirectory)) {
				msbuildExe = Path.Combine (visualStudioDirectory, "MSBuild", "15.0", "Bin", "MSBuild.exe");

				if (File.Exists (msbuildExe))
					return msbuildExe;
			}

			//Try older than VS 2017, MSBuild 14.0
			msbuildExe = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "MSBuild", "14.0", "Bin", "MSBuild.exe");
			if (File.Exists (msbuildExe))
				return msbuildExe;
			
			//MSBuild 4.0 last resort
			return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "MSBuild.exe");
		}

		public string MSBuildExe {
			get {
				return IsUnix
					? GetUnixBuildExe ()
					: GetWindowsBuildExe ();
			}
		}

		public string AndroidMSBuildDirectory {
			get {
				return IsUnix ? Path.Combine (FrameworkLibDirectory, "xbuild", "Xamarin", "Android") : FrameworkLibDirectory;
			}
		}

		public string FrameworkLibDirectory {
			get {
				if (IsUnix) {
					var outdir = Environment.GetEnvironmentVariable ("XA_BUILD_OUTPUT_PATH");
					if (String.IsNullOrEmpty(outdir))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "..", "out"));
					if (!Directory.Exists (Path.Combine (outdir, "lib")))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Debug"));
					if (!Directory.Exists (Path.Combine (outdir, "lib")))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Release"));
					if (!Directory.Exists (outdir))
						outdir = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current";
					return Path.Combine (outdir, "lib", "xamarin.android");
				}
				else {
					var visualStudioDirectory = GetVisualStudio2017Directory ();
					if (!string.IsNullOrEmpty (visualStudioDirectory))
						return Path.Combine (visualStudioDirectory, "MSBuild", "Xamarin", "Android");

					var x86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
					return Path.Combine (x86, "MSBuild", "Xamarin", "Android");
				}
			}
		}

		public bool CrossCompilerAvailable (string supportedAbis)
		{
			var crossCompilerLookup = new Dictionary<string, string> {
				{ "armeabi-v7a", "cross-arm" },
				{ "armeabi", "cross-arm" },
				{ "x86", "cross-x86" },
				{ "x86_64", "cross-x86_64" },
				{ "arm64-v8a", "cross-arm64" },
			};
			bool result = true;
			foreach (var abi in supportedAbis.Split (';')) {
				var fileName = crossCompilerLookup [abi];
				if (IsUnix) {
					result &= (File.Exists (Path.Combine (FrameworkLibDirectory, "xbuild", "Xamarin", "Android", "Darwin", fileName)) ||
						File.Exists (Path.Combine (FrameworkLibDirectory, "xbuild", "Xamarin", "Android", "Linux", fileName)));
				} else {
					result &= File.Exists (Path.Combine (FrameworkLibDirectory, ".exe"));
				}
			}
			return result;
		}

		public string LatestTargetFrameworkVersion () {
			Version latest = new Version (1, 0);
			var outdir = FrameworkLibDirectory;
			var path = Path.Combine (outdir, IsUnix ? Path.Combine ("xbuild-frameworks", "MonoAndroid") : "");
			foreach (var dir in Directory.EnumerateDirectories (path, "v*", SearchOption.TopDirectoryOnly)) {
				Version version;
				string v = Path.GetFileName (dir).Replace ("v", "");
				if (!Version.TryParse (v, out version))
					continue;
				if (latest.Major < version.Major && latest.Minor <= version.Minor)
					latest = version;
			}
			return "v" + latest.ToString (2);
		}


		public string Root {
			get {
				return Path.GetDirectoryName (new Uri (typeof (XamarinProject).Assembly.CodeBase).LocalPath);
			}
		}

		public Builder ()
		{
			IsUnix = Environment.OSVersion.Platform != PlatformID.Win32NT;
			BuildLogFile = "build.log";
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		string GetPathFromRegistry (string valueName)
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				return (string)Microsoft.Win32.Registry.GetValue ("HKEY_CURRENT_USER\\SOFTWARE\\Novell\\Mono for Android", valueName, null);
			}
			return null;
		}

		protected bool BuildInternal (string projectOrSolution, string target, string [] parameters = null, Dictionary<string, string> environmentVariables = null)
		{
			string buildLogFullPath = (!string.IsNullOrEmpty (BuildLogFile))
				? Path.GetFullPath (Path.Combine (Root, Path.GetDirectoryName (projectOrSolution), BuildLogFile))
				: null;

			var logger = buildLogFullPath == null
				? string.Empty
				: string.Format ("/noconsolelogger \"/flp1:LogFile={0};Encoding=UTF-8;Verbosity={1}\"",
					buildLogFullPath, Verbosity.ToString ().ToLower ());

			var start = DateTime.UtcNow;
			var homeDirectory = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			var androidSdkToolPath = Path.Combine (homeDirectory, "android-toolchain");
			var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = GetPathFromRegistry ("AndroidSdkDirectory");
			if (String.IsNullOrEmpty (sdkPath))
				sdkPath = Path.GetFullPath (Path.Combine (androidSdkToolPath, "sdk"));
			var ndkPath = Environment.GetEnvironmentVariable ("ANDROID_NDK_PATH");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = GetPathFromRegistry ("AndroidNdkDirectory");
			if (String.IsNullOrEmpty (ndkPath))
				ndkPath = Path.GetFullPath (Path.Combine (androidSdkToolPath, "ndk"));
			StringBuilder args = new StringBuilder ();
			var psi = new ProcessStartInfo (MSBuildExe);
			if (IsUnix) {
				if (Directory.Exists (sdkPath)) {
					args.AppendFormat ("/p:AndroidSdkDirectory=\"{0}\" ", sdkPath);
				}
				if (Directory.Exists (ndkPath)) {
					args.AppendFormat ("/p:AndroidNdkDirectory=\"{0}\" ", ndkPath);
				}
				var outdir = Path.GetFullPath (Path.Combine (FrameworkLibDirectory, "..", ".."));
				var targetsdir = Path.Combine (FrameworkLibDirectory, "xbuild");
				args.AppendFormat (" {0} ", logger);

				if (Directory.Exists (targetsdir)) {
					psi.EnvironmentVariables ["TARGETS_DIR"] = targetsdir;
					psi.EnvironmentVariables ["MSBuildExtensionsPath"] = targetsdir;
				}
				if (Directory.Exists (outdir)) {
					var frameworksPath = Path.Combine (outdir, "lib", "xamarin.android", "xbuild-frameworks");
					psi.EnvironmentVariables ["MONO_ANDROID_PATH"] = outdir;
					args.AppendFormat ("/p:MonoDroidInstallDirectory=\"{0}\" ", outdir);
					psi.EnvironmentVariables ["XBUILD_FRAMEWORK_FOLDERS_PATH"] = frameworksPath;
					if (RunningMSBuild)
						args.AppendFormat ($"/p:TargetFrameworkRootPath={frameworksPath} ");
				}
				args.AppendFormat ("/t:{0} {1} /p:UseHostCompilerIfAvailable=false /p:BuildingInsideVisualStudio=true", target, QuoteFileName (Path.Combine (Root, projectOrSolution)));
			}
			else {
				args.AppendFormat ("{0} /t:{1} {2} /p:UseHostCompilerIfAvailable=false /p:BuildingInsideVisualStudio=true",
					QuoteFileName(Path.Combine (Root, projectOrSolution)), target, logger);
			}
			if (parameters != null) {
				foreach (var param in parameters) {
					args.AppendFormat (" /p:{0}", param);
				}
			}
			if (environmentVariables != null) {
				foreach (var kvp in environmentVariables) {
					psi.EnvironmentVariables[kvp.Key] = kvp.Value;
				}
			}
			psi.Arguments = args.ToString ();
			
			psi.CreateNoWindow = true;
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			psi.StandardErrorEncoding = Encoding.UTF8;
			psi.StandardOutputEncoding = Encoding.UTF8;
			var p = Process.Start (psi);
			var ranToCompletion = p.WaitForExit ((int)new TimeSpan (0,10,0).TotalMilliseconds);
			var result = ranToCompletion && p.ExitCode == 0;

			LastBuildTime = DateTime.UtcNow - start;

			LastBuildOutput = psi.FileName + " " + args.ToString () + Environment.NewLine;
			if (!ranToCompletion)
				LastBuildOutput += "Build Timed Out!";
			if (buildLogFullPath != null && File.Exists (buildLogFullPath))
				LastBuildOutput += File.ReadAllText (buildLogFullPath);
			LastBuildOutput += string.Format ("\n#stdout begin\n{0}\n#stdout end\n", p.StandardOutput.ReadToEnd ());
			LastBuildOutput += string.Format ("\n#stderr begin\n{0}\n#stderr end\n", p.StandardError.ReadToEnd ());

			if (buildLogFullPath != null) {
				Directory.CreateDirectory (Path.GetDirectoryName (buildLogFullPath));
				File.WriteAllText (buildLogFullPath, LastBuildOutput);
			}
			if (!result && ThrowOnBuildFailure) {
				string message = "Build failure: " + Path.GetFileName (projectOrSolution) + (BuildLogFile != null && File.Exists (buildLogFullPath) ? "Build log recorded at " + buildLogFullPath : null);
				throw new FailedBuildException (message, null, LastBuildOutput);
			}

			return result;
		}

		string QuoteFileName (string fileName)
		{
			return fileName.Contains (" ") ? $"\"{fileName}\"" : fileName;
		}
	}
}

