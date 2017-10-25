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
		public string LastBuildOutput { get; set; }
		public TimeSpan LastBuildTime { get; protected set; }
		public string BuildLogFile { get; set; }
		public bool ThrowOnBuildFailure { get; set; }

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
				if (IsUnix) {
					RunningMSBuild = true;
					var useMSBuild = Environment.GetEnvironmentVariable ("USE_MSBUILD");
					if (!string.IsNullOrEmpty (useMSBuild) && useMSBuild == "0") {
						RunningMSBuild = false;
					}
					#if DEBUG
					var xabuild = Path.GetFullPath (Path.Combine (Root, "..", "Debug", "bin", "xabuild"));
					#else
					var xabuild = Path.GetFullPath (Path.Combine (Root, "..", "Release", "bin", "xabuild"));
					#endif
					if (File.Exists (xabuild))
						return xabuild;
					xabuild = Path.GetFullPath (Path.Combine (Root, "..", "..", "..", "..", "..", "..", "..", "out", "bin", "xabuild"));
					if (File.Exists (xabuild))
						return xabuild;
					return Path.GetFullPath (Path.Combine (Root, "..", "..", "tools", "scripts", "xabuild"));
				}

				#if DEBUG
				return Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Debug", "bin", "xabuild.exe"));
				#else
				return Path.GetFullPath (Path.Combine (Root, "..", "..", "bin", "Release", "bin", "xabuild.exe"));
				#endif
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
				QuoteFileName(Path.Combine (Root, projectOrSolution)), target, logger);
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

			var match = timeElapsedRegEx.Match (LastBuildOutput);
			if (match.Success) {
				LastBuildTime = TimeSpan.Parse (match.Groups ["TimeSpan"].Value);
			}

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

