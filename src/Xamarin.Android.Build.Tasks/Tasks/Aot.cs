using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
// using System.Threading.Tasks conflicts with Microsoft.Build.Utilities because of the Task type
using ThreadingTasks = System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Diagnostics;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public enum AotMode
	{
		Normal,
		Hybrid,
		Full
	}

	public enum SequencePointsMode {
		None,
		Normal,
		Offline,
	}

	// can't be a single ToolTask, because it has to run mkbundle many times for each arch.
	public class Aot : AsyncTask
	{
		[Required]
		public string AndroidAotMode { get; set; }

		[Required]
		public string AndroidNdkDirectory { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; }

		[Required]
		public ITaskItem ManifestFile { get; set; }

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		// Which ABIs to include native libs for
		[Required]
		public string SupportedAbis { get; set; }

		[Required]
		public string AotOutputDirectory { get; set; }

		[Required]
		public string IntermediateAssemblyDir { get; set; }

		public string LinkMode { get; set; }

		public bool EnableLLVM { get; set; }

		public string AndroidSequencePointsMode { get; set; }

		public string AotAdditionalArguments { get; set; }

		public ITaskItem[] AdditionalNativeLibraryReferences { get; set; }

		public string ExtraAotOptions { get; set; }

		[Output]
		public string[] NativeLibrariesReferences { get; set; }

		AotMode AotMode;
		SequencePointsMode sequencePointsMode;

		public Aot ()
		{
		}

		public override bool Execute ()
		{
			if (!NdkUtil.Init (Log, AndroidNdkDirectory))
				return false;

			try {
				return DoExecute ();
			} catch (Exception e) {
				Log.LogCodedError ("XA3001", "{0}", e);
				return false;
			}
		}

		public static bool GetAndroidAotMode(string androidAotMode, out AotMode aotMode)
		{
			aotMode = AotMode.Normal;

			switch ((androidAotMode ?? string.Empty).ToLowerInvariant().Trim())
			{
			case "normal":
				aotMode = AotMode.Normal;
				return true;
			case "hybrid":
				aotMode = AotMode.Hybrid;
				return true;
			case "full":
				aotMode = AotMode.Full;
				return true;
			}

			return false;
		}

		public static bool TryGetSequencePointsMode (string value, out SequencePointsMode mode)
		{
			mode = SequencePointsMode.None;
			switch ((value ?? string.Empty).ToLowerInvariant().Trim ()) {
			case "none":
				mode = SequencePointsMode.None;
				return true;
			case "normal":
				mode = SequencePointsMode.Normal;
				return true;
			case "offline":
				mode = SequencePointsMode.Offline;
				return true;
			}
			return false;
		}

		static string GetNdkToolchainLibraryDir(string binDir, string archDir = null)
		{
			var baseDir = Path.GetFullPath(Path.Combine(binDir, ".."));

			string libDir = Path.Combine (baseDir, "lib", "gcc");
			if (!String.IsNullOrEmpty (archDir))
				libDir = Path.Combine (libDir, archDir);

			var gccLibDir = Directory.EnumerateDirectories (libDir).ToList();
			gccLibDir.Sort();

			var libPath = gccLibDir.LastOrDefault();
			if (libPath == null) {
				goto no_toolchain_error;
			}

			if (NdkUtil.UsingClangNDK)
				return libPath;

			gccLibDir = Directory.EnumerateDirectories(libPath).ToList();
			gccLibDir.Sort();

			libPath = gccLibDir.LastOrDefault();
			if (libPath == null) {
				goto no_toolchain_error;
			}

			return libPath;

		  no_toolchain_error:
			throw new Exception("Could not find a valid NDK compiler toolchain library path");
		}

		static string GetNdkToolchainLibraryDir (string binDir, AndroidTargetArch arch)
		{
			return GetNdkToolchainLibraryDir (binDir, NdkUtil.GetArchDirName (arch));
		}

		static string GetShortPath (string path)
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				return QuoteFileName (path);
			var shortPath = KernelEx.GetShortPathName (Path.GetDirectoryName (path));
			return Path.Combine (shortPath, Path.GetFileName (path));
		}

		static string QuoteFileName(string fileName)
		{
			var builder = new CommandLineBuilder();
			builder.AppendFileNameIfNotNull(fileName);
			return builder.ToString();
		}

		static bool ValidateAotConfiguration (TaskLoggingHelper log, AndroidTargetArch arch, bool enableLLVM)
		{
			return true;
		}

		int GetNdkApiLevel(string androidNdkPath, string androidApiLevel, AndroidTargetArch arch)
		{
			var manifest    = AndroidAppManifest.Load (ManifestFile.ItemSpec, MonoAndroidHelper.SupportedVersions);

			int level;
			if (manifest.MinSdkVersion.HasValue) {
				level       = manifest.MinSdkVersion.Value;
			}
			else if (int.TryParse (androidApiLevel, out level)) {
				// level already set
			}
			else {
				// Probably not ideal!
				level       = MonoAndroidHelper.SupportedVersions.MaxStableVersion.ApiLevel;
			}

			// Some Android API levels do not exist on the NDK level. Workaround this my mapping them to the
			// most appropriate API level that does exist.
			if (level == 6 || level == 7) level = 5;
			else if (level == 10) level = 9;
			else if (level == 11) level = 12;
			else if (level == 20) level = 19;
			else if (level == 22) level = 21;
			else if (level == 23) level = 21;

			// API levels below level 21 do not provide support for 64-bit architectures.
			if (NdkUtil.IsNdk64BitArch(arch) && level < 21) {
				level = 21;
			}

			// We perform a downwards API level lookup search since we might not have hardcoded the correct API
			// mapping above and we do not want to crash needlessly.
			for (; level >= 5; level--) {
				try {
					NdkUtil.GetNdkPlatformLibPath (androidNdkPath, arch, level);
					break;
				} catch (InvalidOperationException ex) {
					// Path not found, continue searching...
					continue;
				}
			}

			return level;
		}

		bool DoExecute () {
			LogDebugMessage ("Aot:", AndroidAotMode);
			LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			LogDebugMessage ("  AndroidAotMode: {0}", AndroidAotMode);
			LogDebugMessage ("  AndroidSequencePointsMode: {0}", AndroidSequencePointsMode);
			LogDebugMessage ("  AndroidNdkDirectory: {0}", AndroidNdkDirectory);
			LogDebugMessage ("  AotOutputDirectory: {0}", AotOutputDirectory);
			LogDebugMessage ("  EnableLLVM: {0}", EnableLLVM);
			LogDebugMessage ("  IntermediateAssemblyDir: {0}", IntermediateAssemblyDir);
			LogDebugMessage ("  LinkMode: {0}", LinkMode);
			LogDebugMessage ("  SupportedAbis: {0}", SupportedAbis);
			LogDebugTaskItems ("  ResolvedAssemblies:", ResolvedAssemblies);
			LogDebugTaskItems ("  AdditionalNativeLibraryReferences:", AdditionalNativeLibraryReferences);

			bool hasValidAotMode = GetAndroidAotMode (AndroidAotMode, out AotMode);
			if (!hasValidAotMode) {
				LogCodedError ("XA3001", "Invalid AOT mode: {0}", AndroidAotMode);
				return false;
			}

			TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode);

			var nativeLibs = new List<string> ();

			Yield ();
			try {
				var task = ThreadingTasks.Task.Run ( () => {
					return RunParallelAotCompiler (nativeLibs);
				}, Token);

				task.ContinueWith (Complete);

				base.Execute ();

				if (!task.Result)
					return false;
			} finally {
				Reacquire ();
			}

			NativeLibrariesReferences = nativeLibs.ToArray ();

			LogDebugMessage ("Aot Outputs:");
			LogDebugTaskItems ("  NativeLibrariesReferences: ", NativeLibrariesReferences);

			return !Log.HasLoggedErrors;
		}

		bool RunParallelAotCompiler (List<string> nativeLibs)
		{
			try {
				ThreadingTasks.ParallelOptions options = new ThreadingTasks.ParallelOptions {
					CancellationToken = Token,
					TaskScheduler = ThreadingTasks.TaskScheduler.Default,
				};

				ThreadingTasks.Parallel.ForEach (GetAotConfigs (), options,
					config => {
						if (!config.Valid) {
							Cancel ();
							return;
						}

						if (!RunAotCompiler (config.AssembliesPath, config.AotCompiler, config.AotOptions, config.AssemblyPath)) {
							LogCodedError ("XA3001", "Could not AOT the assembly: {0}", Path.GetFileName (config.AssemblyPath));
							Cancel ();
							return;
						}

						lock (nativeLibs)
							nativeLibs.Add (config.OutputFile);
					}
				);
			} catch (OperationCanceledException) {
				return false;
			}

			return true;
		}

		IEnumerable<Config> GetAotConfigs ()
		{
			if (!Directory.Exists (AotOutputDirectory))
				Directory.CreateDirectory (AotOutputDirectory);

			var sdkBinDirectory = MonoAndroidHelper.GetOSBinPath ();
			var abis = SupportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var abi in abis) {
				string aotCompiler = "";
				string outdir = "";
				string mtriple = "";
				AndroidTargetArch arch;

				switch (abi) {
				case "armeabi-v7a":
					aotCompiler = Path.Combine (sdkBinDirectory, "cross-arm");
					outdir = Path.Combine (AotOutputDirectory, "armeabi-v7a");
					mtriple = "armv7-linux-gnueabi";
					arch = AndroidTargetArch.Arm;
					break;

				case "arm64":
				case "arm64-v8a":
				case "aarch64":
					aotCompiler = Path.Combine (sdkBinDirectory, "cross-arm64");
					outdir = Path.Combine (AotOutputDirectory, "arm64-v8a");
					mtriple = "aarch64-linux-android";
					arch = AndroidTargetArch.Arm64;
					break;			

				case "x86":
					aotCompiler = Path.Combine (sdkBinDirectory, "cross-x86");
					outdir = Path.Combine (AotOutputDirectory, "x86");
					mtriple = "i686-linux-android";
					arch = AndroidTargetArch.X86;
					break;

				case "x86_64":
					aotCompiler = Path.Combine (sdkBinDirectory, "cross-x86_64");
					outdir = Path.Combine (AotOutputDirectory, "x86_64");
					mtriple = "x86_64-linux-android";
					arch = AndroidTargetArch.X86_64;
					break;

				// case "mips":
				default:
					throw new Exception ("Unsupported Android target architecture ABI: " + abi);
				}

				if (!NdkUtil.ValidateNdkPlatform (Log, AndroidNdkDirectory, arch, enableLLVM:EnableLLVM)) {
					yield return Config.Invalid;
					yield break;
				}

				if (!ValidateAotConfiguration(Log, arch, EnableLLVM)) {
					yield return Config.Invalid;
					yield break;
				}

				outdir = Path.GetFullPath (outdir);
				if (!Directory.Exists (outdir))
					Directory.CreateDirectory (outdir);

				int level = GetNdkApiLevel (AndroidNdkDirectory, AndroidApiLevel, arch);
				string toolPrefix = NdkUtil.GetNdkToolPrefix (AndroidNdkDirectory, arch, level);
				var toolchainPath = toolPrefix.Substring(0, toolPrefix.LastIndexOf(Path.DirectorySeparatorChar));
				var ldFlags = string.Empty;
				if (EnableLLVM) {
					string androidLibPath = string.Empty;
					try {
						androidLibPath = NdkUtil.GetNdkPlatformLibPath(AndroidNdkDirectory, arch, level);
					} catch (InvalidOperationException ex) {
						Diagnostic.Error (5101, ex.Message);
					}

					string toolchainLibDir;
					if (NdkUtil.UsingClangNDK)
						toolchainLibDir = GetNdkToolchainLibraryDir (toolchainPath, arch);
					else
						toolchainLibDir = GetNdkToolchainLibraryDir (toolchainPath);

					var libs = new List<string>();
					if (NdkUtil.UsingClangNDK) {
						libs.Add ($"-L{GetShortPath (toolchainLibDir)}");
						libs.Add ($"-L{GetShortPath (androidLibPath)}");

						if (arch == AndroidTargetArch.Arm) {
							// Needed for -lunwind to work
							string compilerLibDir = Path.Combine (toolchainPath, "..", "sysroot", "usr", "lib", NdkUtil.GetArchDirName (arch));
							libs.Add ($"-L{GetShortPath (compilerLibDir)}");
						}
					}

					libs.Add (GetShortPath (Path.Combine (toolchainLibDir, "libgcc.a")));
					libs.Add (GetShortPath (Path.Combine (androidLibPath, "libc.so")));
					libs.Add (GetShortPath (Path.Combine (androidLibPath, "libm.so")));

					ldFlags = string.Join(";", libs);
				}

				foreach (var assembly in ResolvedAssemblies) {
					string outputFile = Path.Combine(outdir, string.Format ("libaot-{0}.so",
						Path.GetFileName (assembly.ItemSpec)));

					string seqpointsFile = Path.Combine(outdir, string.Format ("{0}.msym",
						Path.GetFileName (assembly.ItemSpec)));

					string tempDir = Path.Combine (outdir, Path.GetFileName (assembly.ItemSpec));
					if (!Directory.Exists (tempDir))
						Directory.CreateDirectory (tempDir);

					List<string> aotOptions = new List<string> ();

					if (!string.IsNullOrEmpty (AotAdditionalArguments))
						aotOptions.Add (AotAdditionalArguments);
					if (sequencePointsMode == SequencePointsMode.Offline)
						aotOptions.Add ("msym-dir=" + GetShortPath (outdir));
					if (AotMode != AotMode.Normal)
						aotOptions.Add (AotMode.ToString ().ToLowerInvariant ());

					aotOptions.Add ("outfile="     + GetShortPath (outputFile));
					aotOptions.Add ("asmwriter");
					aotOptions.Add ("mtriple="     + mtriple);
					aotOptions.Add ("tool-prefix=" + GetShortPath (toolPrefix));
					aotOptions.Add ("ld-flags="    + ldFlags);
					aotOptions.Add ("llvm-path="   + GetShortPath (sdkBinDirectory));
					aotOptions.Add ("temp-path="   + GetShortPath (tempDir));

					string aotOptionsStr = (EnableLLVM ? "--llvm " : "") + "--aot=" + string.Join (",", aotOptions);

					if (!string.IsNullOrEmpty (ExtraAotOptions)) {
						aotOptionsStr += (aotOptions.Count > 0 ? "," : "") + ExtraAotOptions;
					}

					// Due to a Monodroid MSBuild bug we can end up with paths to assemblies that are not in the intermediate
					// assembly directory (typically obj/assemblies). This can lead to problems with the Mono loader not being
					// able to find their dependency laters, since framework assemblies are stored in different directories.
					// This can happen when linking is disabled (AndroidLinkMode=None). Workaround this problem by resolving
					// the paths to the right assemblies manually.
					var resolvedPath = Path.GetFullPath (assembly.ItemSpec);
					var intermediateAssemblyPath = Path.Combine (IntermediateAssemblyDir, Path.GetFileName (assembly.ItemSpec));

					if (LinkMode.ToLowerInvariant () == "none") {
						if (!resolvedPath.Contains (IntermediateAssemblyDir) && File.Exists (intermediateAssemblyPath))
							resolvedPath = intermediateAssemblyPath;
					}

					var assembliesPath = Path.GetFullPath (Path.GetDirectoryName (resolvedPath));
					var assemblyPath = QuoteFileName (Path.GetFullPath (resolvedPath));

					yield return new Config (assembliesPath, QuoteFileName (aotCompiler), aotOptionsStr, assemblyPath, outputFile);
				}
			}
		}
			
		bool RunAotCompiler (string assembliesPath, string aotCompiler, string aotOptions, string assembly)
		{
			var stdout_completed = new ManualResetEvent (false);
			var stderr_completed = new ManualResetEvent (false);
			var psi = new ProcessStartInfo () {
				FileName = aotCompiler,
				Arguments = aotOptions + " " + assembly,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow=true,
				WindowStyle=ProcessWindowStyle.Hidden,
				WorkingDirectory = WorkingDirectory,
			};
			
			// we do not want options to be provided out of band to the cross compilers
			psi.EnvironmentVariables ["MONO_ENV_OPTIONS"] = String.Empty;
			// the C code cannot parse all the license details, including the activation code that tell us which license level is allowed
			// so we provide this out-of-band to the cross-compilers - this can be extended to communicate a few others bits as well
			psi.EnvironmentVariables ["MONO_PATH"] = assembliesPath;

			LogDebugMessage ("[AOT] MONO_PATH=\"{0}\" MONO_ENV_OPTIONS=\"{1}\" {2} {3}",
				psi.EnvironmentVariables ["MONO_PATH"], psi.EnvironmentVariables ["MONO_ENV_OPTIONS"], psi.FileName, psi.Arguments);

			using (var proc = new Process ()) {
				proc.OutputDataReceived += (s, e) => {
					if (e.Data != null)
						OnAotOutputData (s, e);
					else
						stdout_completed.Set ();
				};
				proc.ErrorDataReceived += (s, e) => {
					if (e.Data != null)
						OnAotErrorData (s, e);
					else
						stderr_completed.Set ();
				};
				proc.StartInfo = psi;
				proc.Start ();
				proc.BeginOutputReadLine ();
				proc.BeginErrorReadLine ();
				Token.Register (() => { try { proc.Kill (); } catch (Exception) { } });
				proc.WaitForExit ();
				if (psi.RedirectStandardError)
					stderr_completed.WaitOne (TimeSpan.FromSeconds (30));
				if (psi.RedirectStandardOutput)
					stdout_completed.WaitOne (TimeSpan.FromSeconds (30));
				return proc.ExitCode == 0;
			}
		}

		void OnAotOutputData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ("[aot-compiler stdout] {0}", e.Data);
		}

		void OnAotErrorData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ("[aot-compiler stderr] {0}", e.Data);
		}

		struct Config {
			public string AssembliesPath { get; }
			public string AotCompiler { get; }
			public string AotOptions { get; }
			public string AssemblyPath { get; }
			public string OutputFile { get; }

			public bool Valid { get; private set; }

			public Config (string assembliesPath, string aotCompiler, string aotOptions, string assemblyPath, string outputFile)
			{
				AssembliesPath = assembliesPath;
				AotCompiler = aotCompiler;
				AotOptions = aotOptions;
				AssemblyPath = assemblyPath;
				OutputFile = outputFile;
				Valid = true;
			}

			public static Config Invalid {
				get { return new Config { Valid = false }; }
			}
		}
	}
}
