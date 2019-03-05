using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;

using XABuildPaths = Xamarin.Android.Build.Paths;

namespace Xamarin.ProjectTools
{
	public class Builder : IDisposable
	{
		const string SigSegvError = "Got a SIGSEGV while executing native code";
		const string ConsoleLoggerError = "[ERROR] FATAL UNHANDLED EXCEPTION: System.ArgumentException: is negative";
		static string frameworkSDKRoot = null;

		string root;
		string buildLogFullPath;
		public bool IsUnix { get; set; }
		public bool RunningMSBuild { get; set; }
		/// <summary>
		/// This passes /p:BuildingInsideVisualStudio=True, command-line to MSBuild
		/// </summary>
		public bool BuildingInsideVisualStudio { get; set; } = true;
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
		/// <summary>
		/// True if NuGet restore occurs automatically (default)
		/// </summary>
		public bool AutomaticNuGetRestore { get; set; } = true;

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
					path = FindLatestDotNetSdk (frameworkSDKRoot);
					if (!string.IsNullOrEmpty (path))
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

		public string FirstTargetFrameworkVersion ()
		{
			GetTargetFrameworkVersionRange (out string _, out string firstFrameworkVersion, out string _, out string _);
			return firstFrameworkVersion;
		}

		public string FirstTargetFrameworkVersion (out string apiLevel)
		{
			GetTargetFrameworkVersionRange (out apiLevel, out string firstFrameworkVersion, out string _, out string _);
			return firstFrameworkVersion;
		}

		public string LatestTargetFrameworkVersion () {
			GetTargetFrameworkVersionRange (out string _, out string _, out string _, out string lastFrameworkVersion);
			return lastFrameworkVersion;
		}

		public string LatestTargetFrameworkVersion (out string apiLevel) {
			GetTargetFrameworkVersionRange (out string _, out string _, out apiLevel, out string lastFrameworkVersion);
			return lastFrameworkVersion;
		}

		public void GetTargetFrameworkVersionRange (out string firstApiLevel, out string firstFrameworkVersion, out string lastApiLevel, out string lastFrameworkVersion)
		{
			firstApiLevel = firstFrameworkVersion = lastApiLevel = lastFrameworkVersion = null;

			Version firstVersion    = null;
			Version lastVersion     = null;

			var outdir = FrameworkLibDirectory;
			var path = Path.Combine (outdir, "xbuild-frameworks", "MonoAndroid");
			if (!Directory.Exists(path)) {
				path = outdir;
			}
			foreach (var dir in Directory.EnumerateDirectories (path, "v*", SearchOption.TopDirectoryOnly)) {
				// No binding assemblies in `v1.0`; don't process.
				if (Path.GetFileName (dir) == "v1.0")
					continue;
				Version version;
				string v = Path.GetFileName (dir).Replace ("v", "");
				if (!Version.TryParse (v, out version))
					continue;

				string frameworkVersion = "v" + version.ToString (2);
				string apiLevel         = GetApiLevelFromInfoPath (Path.Combine (dir, "AndroidApiInfo.xml"));
				if (firstVersion == null || version < firstVersion) {
					firstVersion            = version;
					firstFrameworkVersion   = frameworkVersion;
					firstApiLevel           = apiLevel;
				}
				if (lastVersion == null || version > lastVersion) {
					lastVersion             = version;
					lastFrameworkVersion    = frameworkVersion;
					lastApiLevel            = apiLevel;
				}
			}
		}

		static string GetApiLevelFromInfoPath (string androidApiInfo)
		{
			if (!File.Exists (androidApiInfo))
				return null;

			var doc = XDocument.Load (androidApiInfo);
			return doc.XPathSelectElement ("/AndroidApiInfo/Level")?.Value;
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
			if (IsUnix && string.IsNullOrEmpty (frameworkSDKRoot)) {
				var psi = new ProcessStartInfo ("msbuild") {
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					WorkingDirectory = Root,
					Arguments = $" {Path.Combine (Root, "FrameworkPath.targets")} /v:minimal /nologo",
				};
				using (var process = Process.Start (psi)) {
					process.WaitForExit ();
					frameworkSDKRoot = process.StandardOutput.ReadToEnd ().Trim ();
				}

				//NOTE: some machines aren't returning /msbuild/ on the end
				//      macOS should be /Library/Frameworks/Mono.framework/Versions/5.18.0/lib/mono/msbuild/
				var dir = Path.GetFileName (frameworkSDKRoot.TrimEnd (Path.DirectorySeparatorChar));
				if (dir != "msbuild") {
					var path = Path.Combine (frameworkSDKRoot, "msbuild");
					if (Directory.Exists (path))
						frameworkSDKRoot = path;
				}
			}
			if (!string.IsNullOrEmpty (frameworkSDKRoot))
				Console.WriteLine ($"Using $(FrameworkSDKRoot): {frameworkSDKRoot}");
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		public string AndroidSdkDirectory { get; private set; }

		public string AndroidNdkDirectory { get; private set; }

		/// <summary>
		/// Locates and sets AndroidSdkDirectory and AndroidNdkDirectory
		/// </summary>
		public void ResolveSdks ()
		{
			var homeDirectory = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
			var androidSdkToolPath = Path.Combine (homeDirectory, "android-toolchain");
			if (string.IsNullOrEmpty (AndroidSdkDirectory)) {
				var sdkPath = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
				if (String.IsNullOrEmpty (sdkPath))
					sdkPath = GetPathFromRegistry ("AndroidSdkDirectory");
				if (String.IsNullOrEmpty (sdkPath))
					sdkPath = Path.GetFullPath (Path.Combine (androidSdkToolPath, "sdk"));
				if (Directory.Exists (sdkPath)) {
					AndroidSdkDirectory = sdkPath;
				}
			}
			if (string.IsNullOrEmpty (AndroidNdkDirectory)) {
				var ndkPath = Environment.GetEnvironmentVariable ("ANDROID_NDK_PATH");
				if (String.IsNullOrEmpty (ndkPath))
					ndkPath = GetPathFromRegistry ("AndroidNdkDirectory");
				if (String.IsNullOrEmpty (ndkPath))
					ndkPath = Path.GetFullPath (Path.Combine (androidSdkToolPath, "ndk"));
				if (Directory.Exists (ndkPath)) {
					AndroidNdkDirectory = ndkPath;
				}
			}
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

		protected bool BuildInternal (string projectOrSolution, string target, string [] parameters = null, Dictionary<string, string> environmentVariables = null, bool restore = true)
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

			ResolveSdks ();

			var start = DateTime.UtcNow;
			var args  = new StringBuilder ();
			var psi   = new ProcessStartInfo (XABuildExe);
			var responseFile = Path.Combine (XABuildPaths.TestOutputDirectory, Path.GetDirectoryName (projectOrSolution), "project.rsp");
			args.AppendFormat ("{0} /t:{1} {2}",
					QuoteFileName (Path.Combine (XABuildPaths.TestOutputDirectory, projectOrSolution)), target, logger);
			if (AutomaticNuGetRestore && restore) {
				args.Append (" /restore");
			}
			args.Append ($" @\"{responseFile}\"");
			using (var sw = new StreamWriter (responseFile, append: false, encoding: Encoding.UTF8)) {
				sw.WriteLine ($" /p:BuildingInsideVisualStudio={BuildingInsideVisualStudio}");
				if (BuildingInsideVisualStudio && RunningMSBuild) {
					sw.WriteLine (" /p:BuildingOutOfProcess=true");
				}
				if (!string.IsNullOrEmpty (AndroidSdkDirectory)) {
					sw.WriteLine (" /p:AndroidSdkDirectory=\"{0}\" ", AndroidSdkDirectory);
				}
				if (!string.IsNullOrEmpty (AndroidNdkDirectory)) {
					sw.WriteLine (" /p:AndroidNdkDirectory=\"{0}\" ", AndroidNdkDirectory);
				}
				if (parameters != null) {
					foreach (var param in parameters) {
						sw.WriteLine (" /p:{0}", param);
					}
				}
				var msbuildArgs = Environment.GetEnvironmentVariable ("NUNIT_MSBUILD_ARGS");
				if (!string.IsNullOrEmpty (msbuildArgs)) {
					sw.WriteLine (msbuildArgs);
				}
				if (RunningMSBuild) {
					psi.EnvironmentVariables ["MSBUILD"] = "msbuild";
					sw.WriteLine ($" /bl:\"{Path.GetFullPath (Path.Combine (XABuildPaths.TestOutputDirectory, Path.GetDirectoryName (projectOrSolution), "msbuild.binlog"))}\"");
				}
				if (environmentVariables != null) {
					foreach (var kvp in environmentVariables) {
						psi.EnvironmentVariables [kvp.Key] = kvp.Value;
					}
				}
			}

			//NOTE: commit messages can "accidentally" cause test failures
			// Consider if you added an error message in a commit message, then wrote a test asserting the error no longer occurs.
			// Both Jenkins and VSTS have an environment variable containing the full commit message, which will inexplicably cause your test to fail...
			// For a Jenkins case, see https://github.com/xamarin/xamarin-android/pull/1049#issuecomment-347625456
			// For a VSTS case, see http://build.devdiv.io/1806783
			psi.EnvironmentVariables ["ghprbPullLongDescription"] =
				psi.EnvironmentVariables ["BUILD_SOURCEVERSIONMESSAGE"] = "";

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

		string FindLatestDotNetSdk (string dotNetPath)
		{
			if (Directory.Exists (dotNetPath)) {
				Version latest = new Version (0,0);
				string Sdk = null;
				foreach (var dir in Directory.EnumerateDirectories (dotNetPath)) {
					var version = GetVersionFromDirectory (dir);
					var sdksDir = Path.Combine (dir, "Sdks");
					if (!Directory.Exists (sdksDir))
						sdksDir = Path.Combine (dir, "bin", "Sdks");
					if (version != null && version > latest) {
						if (Directory.Exists (sdksDir) && File.Exists (Path.Combine (sdksDir, "Microsoft.NET.Sdk", "Sdk", "Sdk.props"))) {
							latest = version;
							Sdk = Path.Combine (sdksDir, "Microsoft.NET.Sdk");
						}
					}
				}
				return Sdk;
			}
			return null;
		}

		static Version GetVersionFromDirectory (string dir)
		{
			Version v;
			Version.TryParse (Path.GetFileName (dir), out v);
			return v;
		}
	}
}

