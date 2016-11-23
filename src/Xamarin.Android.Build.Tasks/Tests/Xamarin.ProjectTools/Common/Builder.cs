using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class Builder : IDisposable
	{
		const string fixed_osx_xbuild_path = "/Library/Frameworks/Mono.framework/Commands/xbuild";
		string msbuildExe;

		public bool RunXBuild { get; set; }
		public LoggerVerbosity Verbosity { get; set; }
		public string LastBuildOutput { get; set; }
		public TimeSpan LastBuildTime { get; protected set; }
		public string BuildLogFile { get; set; }
		public bool ThrowOnBuildFailure { get; set; }
		public string MSBuildExe {
			get {
				return RunXBuild
					? File.Exists (fixed_osx_xbuild_path) ? fixed_osx_xbuild_path : "xbuild"
					: msbuildExe;
			}
		}

		public string FrameworkLibDirectory {
			get {
				if (RunXBuild) {
					var outdir = Environment.GetEnvironmentVariable ("XA_BUILD_OUTPUT_PATH");
					if (String.IsNullOrEmpty (outdir))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "bin", "Debug"));
					if (!Directory.Exists (Path.Combine (outdir, "lib")))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "bin", "Release"));
					if (!Directory.Exists (outdir))
						outdir = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current";
					return Path.Combine (outdir, "lib");
				}
				else {
					var x86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
					return Path.Combine (x86, "MSBuild", "Xamarin", "Android");
				}
			}
		}

		public string Root {
			get {
				return Path.GetDirectoryName (new Uri (typeof (XamarinProject).Assembly.CodeBase).LocalPath);
			}
		}

		public Builder ()
		{
			RunXBuild = Environment.OSVersion.Platform != PlatformID.Win32NT;
			BuildLogFile = "build.log";
			// Allow the override of the location of MSBuild and try a couple of backup paths for 
			// MSBuild 14.0 and 4.0 
			msbuildExe = Environment.GetEnvironmentVariable ("XA_MSBUILD_EXE");
			if (String.IsNullOrEmpty (msbuildExe) || !File.Exists (msbuildExe))
				msbuildExe = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "MSBuild", "14.0", "Bin", "MSBuild.exe");
			if (!File.Exists (msbuildExe))
				msbuildExe = string.Format ("{0}\\Microsoft.NET\\Framework\\v4.0.30319\\msbuild.exe", Environment.GetEnvironmentVariable ("WINDIR"));
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

		protected bool BuildInternal (string projectOrSolution, string target, string [] parameters = null)
		{
			string buildLogFullPath = (!string.IsNullOrEmpty (BuildLogFile))
				? Path.GetFullPath (Path.Combine (Root, Path.GetDirectoryName (projectOrSolution), BuildLogFile))
				: null;

			var logger = buildLogFullPath == null
				? string.Empty
				: string.Format ("/noconsolelogger /flp1:LogFile={0};Encoding=UTF-8;Verbosity={1}",
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
			if (Directory.Exists (sdkPath)) {
				args.AppendFormat ("/p:AndroidSdkDirectory=\"{0}\" ", sdkPath);
			}
			if (Directory.Exists (ndkPath)) {
				args.AppendFormat ("/p:AndroidNdkDirectory=\"{0}\" ", ndkPath);
			}
			if (RunXBuild) {
				var outdir = Environment.GetEnvironmentVariable ("XA_BUILD_OUTPUT_PATH");
				if (String.IsNullOrEmpty (outdir))
					outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Debug"));

				if (!Directory.Exists (Path.Combine (outdir, "lib", "xbuild")))
					outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Release"));

				var targetsdir = Path.Combine (outdir, "lib", "xbuild");
				args.AppendFormat (" {0} ", logger);

				if (Directory.Exists (targetsdir)) {
					psi.EnvironmentVariables ["TARGETS_DIR"] = targetsdir;
					psi.EnvironmentVariables ["MSBuildExtensionsPath"] = targetsdir;
				}
				if (Directory.Exists (outdir)) {
					psi.EnvironmentVariables ["MONO_ANDROID_PATH"] = outdir;
					psi.EnvironmentVariables ["XBUILD_FRAMEWORK_FOLDERS_PATH"] = Path.Combine (outdir, "lib", "xbuild-frameworks");
					args.AppendFormat ("/p:MonoDroidInstallDirectory=\"{0}\" ", outdir);
				}
				args.AppendFormat ("/t:{0} {1}", target, Path.Combine (Root, projectOrSolution));
			}
			else {
				args.AppendFormat ("{0} /t:{1} {2} /p:UseHostCompilerIfAvailable=false /p:BuildingInsideVisualStudio=true",
					Path.Combine (Root, projectOrSolution), target, logger);
			}
			if (parameters != null) {
				foreach (var param in parameters) {
					args.AppendFormat (" /p:{0}", param);
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
			p.WaitForExit ();
			var result = p.ExitCode == 0;

			LastBuildTime = DateTime.UtcNow - start;

			LastBuildOutput = String.Empty;
			if (buildLogFullPath != null)
				LastBuildOutput += File.ReadAllText (buildLogFullPath);
			LastBuildOutput += string.Format ("\n#stdout begin\n{0}\n#stdout end\n", p.StandardOutput.ReadToEnd ());
			LastBuildOutput += string.Format ("\n#stderr begin\n{0}\n#stderr end\n", p.StandardError.ReadToEnd ());

			if (buildLogFullPath != null)
				File.WriteAllText (buildLogFullPath, LastBuildOutput);
			if (!result && ThrowOnBuildFailure) {
				string message = "Build failure: " + Path.GetFileName (projectOrSolution) + (BuildLogFile != null && File.Exists (buildLogFullPath) ? "Build log recorded at " + buildLogFullPath : null);
				throw new FailedBuildException (message, null, LastBuildOutput);
			}

			return result;
		}
	}
}

