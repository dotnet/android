using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xamarin.ProjectTools
{
	public class Builder : IDisposable
	{
		public bool IsUnix { get; set; }
		public bool RunningMSBuild { get; set; }
		public LoggerVerbosity Verbosity { get; set; }
		public TimeSpan LastBuildTime { get; protected set; }
		public string BuildLogFile { get; set; }
		public bool ThrowOnBuildFailure { get; set; }

		//TODO: remove
		public string LastBuildOutput {
			get { throw new NotImplementedException (); }
		}

		public List<Assertion> Assertions { get; set; } = new List<Assertion> ();

		public void AssertTargetSkipped (string target)
		{
			Assertions.Add (new Assertion (o => o.Contains (target), $"Target '{target}' is not even in the build output."));

			Assertions.Add (new Assertion (o => o.Contains ($"Target {target} skipped due to ") ||
				o.Contains ($"Skipping target \"{target}\" because it has no outputs.") ||
				o.Contains ($"Target \"{target}\" skipped, due to") ||
				o.Contains ($"Skipping target \"{target}\" because its outputs are up-to-date") ||
				o.Contains ($"target {target}, skipping") ||
				o.Contains ($"Skipping target \"{target}\" because all output files are up-to-date"), $"Target {target} was not skipped."));
		}

		public void AssertApkInstalled ()
		{
			Assertions.Add (new Assertion (o => o.Contains (" pm install ")));
		}

		public void AssertAllTargetsSkipped (params string [] targets)
		{
			foreach (var t in targets) {
				AssertTargetSkipped (t);
			}
		}

		public void AssertTargetIsBuilt (string target)
		{
			Assertions.Add (new Assertion (o => o.Contains (target), $"Target '{target}' is not even in the build output."));

			Assertions.Add (new Assertion (o => !o.Contains ($"Target {target} skipped due to ") &&
				!o.Contains ($"Skipping target \"{target}\" because it has no outputs.") &&
				!o.Contains ($"Target \"{target}\" skipped, due to") &&
				!o.Contains ($"Skipping target \"{target}\" because its outputs are up-to-date") &&
				!o.Contains ($"target {target}, skipping") &&
				!o.Contains ($"Skipping target \"{target}\" because all output files are up-to-date"), $"Target {target} was not built."));
		}

		public void AssertAllTargetsBuilt (params string [] targets)
		{
			foreach (var t in targets) {
				AssertTargetIsBuilt (t);
			}
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

		public string XABuildExe {
			get {
				string xabuild;
				if (IsUnix) {
					RunningMSBuild = true;
					var useMSBuild = Environment.GetEnvironmentVariable ("USE_MSBUILD");
					if (!string.IsNullOrEmpty (useMSBuild) && useMSBuild == "0") {
						RunningMSBuild = false;
					}
					#if DEBUG
					xabuild = Path.GetFullPath (Path.Combine (Root, "..", "Debug", "bin", "xabuild"));
					#else
					xabuild = Path.GetFullPath (Path.Combine (Root, "..", "Release", "bin", "xabuild"));
					#endif
					if (File.Exists (xabuild))
						return xabuild;
					xabuild = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "..", "out", "bin", "xabuild"));
					if (File.Exists (xabuild))
						return xabuild;
					return RunningMSBuild ? "msbuild" : "xbuild";
				}

				#if DEBUG
				xabuild = Path.GetFullPath (Path.Combine (Root, "..", "Debug", "bin", "xabuild.exe"));
				#else
				xabuild =  Path.GetFullPath (Path.Combine (Root, "..", "Release", "bin", "xabuild.exe"));
				#endif
				if (File.Exists (xabuild))
					return xabuild;
				return "msbuild";
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
					#if DEBUG
					var configuraton = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? "Debug";
					#else
					var configuraton = Environment.GetEnvironmentVariable ("CONFIGURATION") ?? "Release";
					#endif
					var libmonodroidPath = Path.Combine ("lib", "xamarin.android", "xbuild", "Xamarin", "Android", "lib", "armeabi-v7a", "libmono-android.release.so");
					if (String.IsNullOrEmpty(outdir))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "..", "out"));
					if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", configuraton));
					if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Debug"));
					if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
						outdir = Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Release"));
					if (!Directory.Exists (Path.Combine (outdir, "lib")) || !File.Exists (Path.Combine (outdir, libmonodroidPath)))
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
			string buildLogFullPath = (!string.IsNullOrEmpty (BuildLogFile))
				? Path.GetFullPath (Path.Combine (Root, Path.GetDirectoryName (projectOrSolution), BuildLogFile))
				: null;

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
			args.AppendFormat ("{0} /t:{1} /v:{2}",
				QuoteFileName(Path.Combine (Root, projectOrSolution)), target, Verbosity);
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
			if (RunningMSBuild) {
				psi.EnvironmentVariables ["MSBUILD"] = "msbuild";
			}
			if (environmentVariables != null) {
				foreach (var kvp in environmentVariables) {
					psi.EnvironmentVariables [kvp.Key] = kvp.Value;
				}
			}
			psi.Arguments = args.ToString ();
			
			psi.CreateNoWindow = true;
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			psi.StandardErrorEncoding = Encoding.UTF8;
			psi.StandardOutputEncoding = Encoding.UTF8;

			bool nativeCrashDetected = false;
			bool result = false;
			bool lastBuildTimeSet = false;
			int attempts = 1;
			for (int attempt = 0; attempt < attempts; attempt++) {
				using (var p = new Process { StartInfo = psi }) {
					StreamWriter file = null;
					if (buildLogFullPath != null) {
						Directory.CreateDirectory (Path.GetDirectoryName (buildLogFullPath));
						file = File.CreateText (buildLogFullPath);
					}
					try {
						var standardError = new StringBuilder ();
						file?.WriteLine ("#stdout begin");

						p.OutputDataReceived += (sender, e) => {
							if (e.Data == null)
								return;
							if (e.Data.StartsWith ("Time Elapsed", StringComparison.OrdinalIgnoreCase)) {
								var match = timeElapsedRegEx.Match (e.Data);
								if (match.Success) {
									LastBuildTime = TimeSpan.Parse (match.Groups ["TimeSpan"].Value);
									lastBuildTimeSet = true;
									Console.WriteLine ($"Found Time Elapsed {LastBuildTime}");
								}
							}

							if (e.Data.StartsWith ("Got a SIGSEGV while executing native code", StringComparison.OrdinalIgnoreCase)) {
								nativeCrashDetected = true;
							}

							foreach (var assertion in Assertions) {
								assertion.Assert (e.Data);
							}

							file?.WriteLine (e.Data);
						};
						p.ErrorDataReceived += (sender, e) => standardError.AppendLine (e.Data);

						p.Start ();
						p.BeginOutputReadLine ();
						p.BeginErrorReadLine ();

						var ranToCompletion = p.WaitForExit ((int)new TimeSpan (0, 10, 0).TotalMilliseconds);
						if (nativeCrashDetected) {
							Console.WriteLine ($"Native crash detected! Running the build for {projectOrSolution} again.");
							continue;
						}
						result = ranToCompletion && p.ExitCode == 0;
						if (!lastBuildTimeSet)
							LastBuildTime = DateTime.UtcNow - start;

						if (file != null) {
							file.WriteLine ();
							file.WriteLine ("#stdout end");
							file.WriteLine ();
							file.WriteLine ("#stderr begin");
							file.WriteLine (standardError.ToString ());
							file.WriteLine ("#stderr end");
						}
					} finally {
						file?.Dispose ();
					}
				}
			}

			if (!result && ThrowOnBuildFailure) {
				string message = "Build failure: " + Path.GetFileName (projectOrSolution) + (BuildLogFile != null && File.Exists (buildLogFullPath) ? "Build log recorded at " + buildLogFullPath : null);

				//TODO: do we really need the full build log here? It seems to lock up my VS test runner
				throw new FailedBuildException (message, null);
			}

			return result;
		}

		string QuoteFileName (string fileName)
		{
			return fileName.Contains (" ") ? $"\"{fileName}\"" : fileName;
		}
	}
}

