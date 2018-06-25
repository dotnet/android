using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

using XABuildPaths = Xamarin.Android.Build.Paths;

namespace Xamarin.ProjectTools
{
	public class Builder : IDisposable
	{
		const string SigSegvError = "Got a SIGSEGV while executing native code";
		const string ConsoleLoggerError = "[ERROR] FATAL UNHANDLED EXCEPTION: System.ArgumentException: is negative";

		string root;
		string buildLogFullPath;
		public bool IsUnix { get; set; }
		public bool RunningMSBuild { get; set; }
		public LoggerVerbosity Verbosity { get; set; }
		public IEnumerable<string> LastBuildOutput {
			get {
				if (!string.IsNullOrEmpty (buildLogFullPath) && File.Exists (buildLogFullPath)) {
					foreach (var line in File.ReadLines (buildLogFullPath, Encoding.UTF8)) {
						yield return line;
					}
				}
				yield return String.Empty;
			}
		}
		public TimeSpan LastBuildTime { get; protected set; }
		public string BuildLogFile { get; set; }
		public bool ThrowOnBuildFailure { get; set; }
		public bool RequiresMSBuild { get; set; }

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

		public string XABuildExe {
			get {
				RunningMSBuild = true;
				string xabuild;
				if (IsUnix) {
					var useMSBuild = Environment.GetEnvironmentVariable ("USE_MSBUILD");
					if (!string.IsNullOrEmpty (useMSBuild) && useMSBuild == "0" && !RequiresMSBuild) {
						RunningMSBuild = false;
					}

					xabuild = XABuildPaths.XABuildScript;
					if (File.Exists (xabuild))
						return xabuild;
					xabuild = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "..", "out", "bin", "xabuild"));
					if (File.Exists (xabuild))
						return xabuild;
					return RunningMSBuild ? "msbuild" : "xbuild";
				}

				xabuild = XABuildPaths.XABuildExe;
				if (File.Exists (xabuild))
					return xabuild;
				return "msbuild";
			}
		}

		public string AndroidMSBuildDirectory {
			get {
				var frameworkLibDir = FrameworkLibDirectory;
				var path = Path.Combine (frameworkLibDir, "xbuild", "Xamarin", "Android");
				if (Directory.Exists (path))
					return path;
				return frameworkLibDir;
			}
		}

		public string FrameworkLibDirectory {
			get {
				var outdir = Environment.GetEnvironmentVariable ("XA_BUILD_OUTPUT_PATH");
				string configuration = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? XABuildPaths.Configuration;
				var libmonodroidPath = Path.Combine ("lib", "xamarin.android", "xbuild", "Xamarin", "Android", "lib", "armeabi-v7a", "libmono-android.release.so");
				if (String.IsNullOrEmpty(outdir))
					outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "..", "out"));
				if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
					outdir = Path.Combine (XABuildPaths.TopDirectory, "bin", configuration);
				if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
					outdir = XABuildPaths.PrefixDirectory;
				if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
					outdir = Path.Combine (XABuildPaths.TopDirectory, "bin", "Debug");
				if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
					outdir = Path.Combine (XABuildPaths.TopDirectory, "bin", "Release");
				if (IsUnix) {
					if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
						outdir = "/Library/Frameworks/Xamarin.Android.framework/Versions/Current";
					return Path.Combine (outdir, "lib", "xamarin.android");
				}
				else {
					if (Directory.Exists (Path.Combine (outdir, "lib")) && File.Exists (Path.Combine (outdir, libmonodroidPath)))
						return Path.Combine (outdir, "lib", "xamarin.android");

					var visualStudioDirectory = GetVisualStudio2017Directory ();
					if (!string.IsNullOrEmpty (visualStudioDirectory))
						return Path.Combine (visualStudioDirectory, "MSBuild", "Xamarin", "Android");

					var x86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
					return Path.Combine (x86, "MSBuild", "Xamarin", "Android");
				}
			}
		}

		public string MicrosoftNetSdkDirectory {
			get {
				string path;
				if (IsUnix) {
					path = Path.Combine ("/usr", "local", "share", "dotnet", "sdk", "2.0.0", "Sdks", "Microsoft.NET.Sdk");
					if (File.Exists (Path.Combine (path, "Sdk", "Sdk.props")))
						return path;
					return string.Empty;
				}
				var visualStudioDirectory = GetVisualStudio2017Directory ();
				if (!string.IsNullOrEmpty (visualStudioDirectory)) {
					path = Path.Combine (visualStudioDirectory, "MSBuild", "Sdks", "Microsoft.NET.Sdk");
					if (File.Exists (Path.Combine (path, "Sdk", "Sdk.props")))
						return path;
				}
				var x86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
				path = Path.Combine (x86, "MSBuild", "Sdks", "Microsoft.NET.Sdk");
				if (File.Exists (Path.Combine (path, "Sdk", "Sdk.props")))
					return path;
				return string.Empty;
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
				var path = AndroidMSBuildDirectory;
				if (IsUnix) {
					result &= (File.Exists (Path.Combine (path, "Darwin", fileName)) ||
						File.Exists (Path.Combine (path, "Linux", fileName)));
				} else {
					result &= File.Exists (Path.Combine (path, fileName + ".exe"));
				}
			}
			return result;
		}

		public string LatestTargetFrameworkVersion () {
			Version latest = new Version (1, 0);
			var outdir = FrameworkLibDirectory;
			var path = Path.Combine (outdir, "xbuild-frameworks", "MonoAndroid");
			if (!Directory.Exists(path)) {
				path = outdir;
			}
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

		public bool TargetFrameworkExists (string targetFramework)
		{
			var path = Path.Combine (FrameworkLibDirectory, "xbuild-frameworks", "MonoAndroid", targetFramework);
			if (!Directory.Exists (path)) {
				return false;
			}
			return true;
		}


		public string Root {
			get {
				return String.IsNullOrEmpty (root) ? Path.GetDirectoryName (new Uri (typeof (XamarinProject).Assembly.CodeBase).LocalPath) : root;
			}
			set { root = value; }
		}

		public Builder ()
		{
			IsUnix = Environment.OSVersion.Platform != PlatformID.Win32NT;
			BuildLogFile = "build.log";
			Console.WriteLine ($"Using {XABuildExe}");
			Console.WriteLine ($"Using {(RunningMSBuild ? "msbuild" : "xbuild")}");
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

		Regex timeElapsedRegEx = new Regex (
			@"^Time Elapsed([\s])(?<TimeSpan>(\d+):(\d\d):(\d\d)\.(\d+))$",
			RegexOptions.Multiline | RegexOptions.Compiled
		);

		protected bool BuildInternal (string projectOrSolution, string target, string [] parameters = null, Dictionary<string, string> environmentVariables = null)
		{
			buildLogFullPath = (!string.IsNullOrEmpty (BuildLogFile))
				? Path.GetFullPath (Path.Combine (XABuildPaths.TestOutputDirectory, Path.GetDirectoryName (projectOrSolution), BuildLogFile))
				: null;
			string processLog = !string.IsNullOrEmpty (BuildLogFile)
				? Path.Combine (Path.GetDirectoryName (buildLogFullPath), "process.log")
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

			var args  = new StringBuilder ();
			var psi   = new ProcessStartInfo (XABuildExe);
			args.AppendFormat ("{0} /t:{1} {2}",
				QuoteFileName(Path.Combine (XABuildPaths.TestOutputDirectory, projectOrSolution)), target, logger);
			if (RunningMSBuild)
				args.Append (" /p:BuildingOutOfProcess=true");
			else
				args.Append (" /p:UseHostCompilerIfAvailable=false /p:BuildingInsideVisualStudio=true");
			if (Directory.Exists (sdkPath)) {
				args.AppendFormat (" /p:AndroidSdkDirectory=\"{0}\" ", sdkPath);
			}
			if (Directory.Exists (ndkPath)) {
				args.AppendFormat (" /p:AndroidNdkDirectory=\"{0}\" ", ndkPath);
			}
			if (parameters != null) {
				foreach (var param in parameters) {
					args.AppendFormat (" /p:{0}", param);
				}
			}
			var msbuildArgs = Environment.GetEnvironmentVariable ("NUNIT_MSBUILD_ARGS");
			if (!string.IsNullOrEmpty (msbuildArgs)) {
				args.Append (msbuildArgs);
			}
			if (RunningMSBuild) {
				psi.EnvironmentVariables ["MSBUILD"] = "msbuild";
				args.Append ($" /bl:\"{Path.GetFullPath (Path.Combine (XABuildPaths.TestOutputDirectory, Path.GetDirectoryName (projectOrSolution), "msbuild.binlog"))}\"");
			}
			if (environmentVariables != null) {
				foreach (var kvp in environmentVariables) {
					psi.EnvironmentVariables [kvp.Key] = kvp.Value;
				}
			}
			//NOTE: fix for Jenkins, see https://github.com/xamarin/xamarin-android/pull/1049#issuecomment-347625456
			psi.EnvironmentVariables ["ghprbPullLongDescription"] = "";

			psi.Arguments = args.ToString ();
			
			psi.CreateNoWindow = true;
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			psi.StandardErrorEncoding = Encoding.UTF8;
			psi.StandardOutputEncoding = Encoding.UTF8;

			bool nativeCrashDetected = false;
			bool result = false;
			bool ranToCompletion = false;
			int attempts = 1;
			ManualResetEvent err = new ManualResetEvent (false);
			ManualResetEvent stdout = new ManualResetEvent (false);
			for (int attempt = 0; attempt < attempts; attempt++) {
				if (processLog != null)
					File.AppendAllText (processLog, psi.FileName + " " + args.ToString () + Environment.NewLine);
				using (var p = new Process ()) {
					p.ErrorDataReceived += (sender, e) => {
						if (e.Data != null && !string.IsNullOrEmpty (processLog)) {
							File.AppendAllText (processLog, e.Data + Environment.NewLine);
							if (e.Data.StartsWith (SigSegvError, StringComparison.OrdinalIgnoreCase)) {
								nativeCrashDetected = true;
							}
							if (e.Data.StartsWith (ConsoleLoggerError, StringComparison.OrdinalIgnoreCase)) {
								nativeCrashDetected = true;
							}
						}
						if (e.Data == null)
							err.Set ();
					};
					p.OutputDataReceived += (sender, e) => {
						if (e.Data != null && !string.IsNullOrEmpty (processLog)) {
							File.AppendAllText (processLog, e.Data + Environment.NewLine);
							if (e.Data.StartsWith (SigSegvError, StringComparison.OrdinalIgnoreCase)) {
								nativeCrashDetected = true;
							}
							if (e.Data.StartsWith (ConsoleLoggerError, StringComparison.OrdinalIgnoreCase)) {
								nativeCrashDetected = true;
							}
						}
						if (e.Data == null)
							stdout.Set ();
					};
					p.StartInfo = psi;
					p.Start ();
					p.BeginOutputReadLine ();
					p.BeginErrorReadLine ();
					ranToCompletion = p.WaitForExit ((int)new TimeSpan (0, 10, 0).TotalMilliseconds);
					if (psi.RedirectStandardOutput)
						stdout.WaitOne ();
					if (psi.RedirectStandardError)
						err.WaitOne ();
					result = ranToCompletion && p.ExitCode == 0;
				}

				LastBuildTime = DateTime.UtcNow - start;

				if (processLog != null && !ranToCompletion)
					File.AppendAllText (processLog, "Build Timed Out!");
				if (buildLogFullPath != null && File.Exists (buildLogFullPath)) {
					foreach (var line in LastBuildOutput) {
						if (line.StartsWith ("Time Elapsed", StringComparison.OrdinalIgnoreCase)) {
							var match = timeElapsedRegEx.Match (line);
							if (match.Success) {
								LastBuildTime = TimeSpan.Parse (match.Groups ["TimeSpan"].Value);
								Console.WriteLine ($"Found Time Elapsed {LastBuildTime}");
							}
						}
					}
				}

				if (nativeCrashDetected) {
					Console.WriteLine ($"Native crash detected! Running the build for {projectOrSolution} again.");
					if (attempt == 0)
						File.Move (processLog, processLog + ".bak");
					nativeCrashDetected = false;
					continue;
				} else {
					break;
				}
			}


			if (buildLogFullPath != null && processLog != null) {
				Directory.CreateDirectory (Path.GetDirectoryName (buildLogFullPath));
				if (File.Exists (processLog))
					File.AppendAllText (buildLogFullPath, File.ReadAllText (processLog));
			}
			if (!result && ThrowOnBuildFailure) {
				string message = "Build failure: " + Path.GetFileName (projectOrSolution) + (BuildLogFile != null && File.Exists (buildLogFullPath) ? "Build log recorded at " + buildLogFullPath : null);
				//NOTE: enormous logs will lock up IDE's UI
				if (IsRunningInIDE) {
					throw new FailedBuildException (message);
				} else {
					throw new FailedBuildException (message, null, File.ReadAllText (buildLogFullPath));
				}
			}

			return result;
		}

		bool IsRunningInIDE {
			get {
				//Check for Windows, process is testhost.x86 in VS 2017
				using (var p = Process.GetCurrentProcess ()) {
					if (p.ProcessName.IndexOf ("testhost", StringComparison.OrdinalIgnoreCase) != -1) {
						return true;
					}
				}

				//Check for macOS, value is normally /Applications/Visual Studio.app/Contents/Resources
				var gac_prefix = Environment.GetEnvironmentVariable ("MONO_GAC_PREFIX", EnvironmentVariableTarget.Process);
				if (!string.IsNullOrEmpty (gac_prefix) && gac_prefix.IndexOf ("Visual Studio", StringComparison.OrdinalIgnoreCase) != -1) {
					return true;
				}

				return false;
			}
		}

		string QuoteFileName (string fileName)
		{
			return fileName.Contains (" ") ? $"\"{fileName}\"" : fileName;
		}
	}
}

