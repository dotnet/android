using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Diagnostics;
using Xamarin.Android.Build.Utilities;

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
	public class Aot : Task
	{
		[Required]
		public string AndroidAotMode { get; set; }

		[Required]
		public string AndroidNdkDirectory { get; set; }

		[Required]
		public string AndroidApiLevel { get; set; }

		[Required]
		public string SdkBinDirectory { get; set; }

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

		[Output]
		public string[] NativeLibrariesReferences { get; set; }

		AotMode AotMode;
		SequencePointsMode sequencePointsMode;

		public Aot ()
		{
		}

		public override bool Execute ()
		{
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

		static string GetNdkToolchainLibraryDir(string binDir)
		{
			var baseDir = Path.GetFullPath(Path.Combine(binDir, ".."));

			var gccLibDir = Directory.EnumerateDirectories(
			Path.Combine(baseDir, "lib", "gcc")).ToList();
			gccLibDir.Sort();

			var libPath = gccLibDir.LastOrDefault();
			if (libPath == null)
				throw new Exception("Could not find a valid NDK GCC toolchain library path");

			gccLibDir = Directory.EnumerateDirectories(libPath).ToList();
			gccLibDir.Sort();

			libPath = gccLibDir.LastOrDefault();
			if (libPath == null)
				throw new Exception("Could not find a valid NDK GCC toolchain library path");

			return libPath;
		}

		static string QuoteFileName(string fileName)
		{
			var builder = new CommandLineBuilder();
			builder.AppendFileNameIfNotNull(fileName);
			return builder.ToString();
		}

		static bool ValidateAotConfiguration (TaskLoggingHelper log, AndroidTargetArch arch, bool enableLLVM)
		{
			if (arch == AndroidTargetArch.X86_64) {
				log.LogCodedError ("XA3004", "x86_64 architecture is not currently supported on AOT mode.");
				return false;
			}			

			return true;
		}

		static int GetNdkApiLevel(string androidNdkPath, string androidApiLevel, AndroidTargetArch arch)
		{
			int level = int.Parse(androidApiLevel);
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
			Log.LogDebugMessage ("Aot:", AndroidAotMode);
			Log.LogDebugMessage ("  AndroidApiLevel: {0}", AndroidApiLevel);
			Log.LogDebugMessage ("  AndroidAotMode: {0}", AndroidAotMode);
			Log.LogDebugMessage ("  AndroidSequencePointsMode: {0}", AndroidSequencePointsMode);
			Log.LogDebugMessage ("  AndroidNdkDirectory: {0}", AndroidNdkDirectory);
			Log.LogDebugMessage ("  AotOutputDirectory: {0}", AotOutputDirectory);
			Log.LogDebugMessage ("  EnableLLVM: {0}", EnableLLVM);
			Log.LogDebugMessage ("  IntermediateAssemblyDir: {0}", IntermediateAssemblyDir);
			Log.LogDebugMessage ("  LinkMode: {0}", LinkMode);
			Log.LogDebugMessage ("  SdkBinDirectory: {0}", SdkBinDirectory);
			Log.LogDebugMessage ("  SupportedAbis: {0}", SupportedAbis);
			Log.LogDebugTaskItems ("  ResolvedAssemblies:", ResolvedAssemblies);
			Log.LogDebugTaskItems ("  AdditionalNativeLibraryReferences:", AdditionalNativeLibraryReferences);

			bool hasValidAotMode = GetAndroidAotMode (AndroidAotMode, out AotMode);
			if (!hasValidAotMode) {
				Log.LogCodedError ("XA3001", "Invalid AOT mode: {0}", AndroidAotMode);
				return false;
			}

			TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode);

			var nativeLibs = new List<string> ();

			if (!Directory.Exists (AotOutputDirectory))
				Directory.CreateDirectory (AotOutputDirectory);

			// Check that we have a compatible NDK version for the targeted ABIs.
			NdkUtil.NdkVersion ndkVersion;
			bool hasNdkVersion = NdkUtil.GetNdkToolchainRelease (AndroidNdkDirectory, out ndkVersion);

			var abis = SupportedAbis.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var abi in abis) {
				string aotCompiler = "";
				string outdir = "";
				string mtriple = "";
				AndroidTargetArch arch;

				switch (abi) {
				case "armeabi":
					aotCompiler = Path.Combine (SdkBinDirectory, "cross-arm");
					outdir = Path.Combine (AotOutputDirectory, "armeabi");
					mtriple = "armv5-linux-gnueabi";
					arch = AndroidTargetArch.Arm;
					break;

				case "armeabi-v7a":
					aotCompiler = Path.Combine (SdkBinDirectory, "cross-arm");
					outdir = Path.Combine (AotOutputDirectory, "armeabi-v7a");
					mtriple = "armv7-linux-gnueabi";
					arch = AndroidTargetArch.Arm;
					break;

				case "arm64":
				case "arm64-v8a":
				case "aarch64":
					aotCompiler = Path.Combine (SdkBinDirectory, "cross-arm64");
					outdir = Path.Combine (AotOutputDirectory, "arm64-v8a");
					mtriple = "aarch64-linux-android";
					arch = AndroidTargetArch.Arm64;
					break;			

				case "x86":
					aotCompiler = Path.Combine (SdkBinDirectory, "cross-x86");
					outdir = Path.Combine (AotOutputDirectory, "x86");
					mtriple = "i686-linux-android";
					arch = AndroidTargetArch.X86;
					break;

				case "x86_64":
					aotCompiler = Path.Combine (SdkBinDirectory, "cross-x86_64");
					outdir = Path.Combine (AotOutputDirectory, "x86_64");
					mtriple = "x86_64-linux-android";
					arch = AndroidTargetArch.X86_64;
					break;

				// case "mips":
				default:
					throw new Exception ("Unsupported Android target architecture ABI: " + abi);
				}

				if (!NdkUtil.ValidateNdkPlatform (Log, AndroidNdkDirectory, arch, enableLLVM:EnableLLVM)) {
					return false;
				}

				if (!ValidateAotConfiguration(Log, arch, EnableLLVM)) {
					return false;
				}

				outdir = Path.GetFullPath (outdir);
				if (!Directory.Exists (outdir))
					Directory.CreateDirectory (outdir);

				var toolPrefix = NdkUtil.GetNdkToolPrefix (AndroidNdkDirectory, arch);
				var toolchainPath = toolPrefix.Substring(0, toolPrefix.LastIndexOf(Path.DirectorySeparatorChar));
				var ldFlags = string.Empty;
				if (EnableLLVM) {
					int level = GetNdkApiLevel (AndroidNdkDirectory, AndroidApiLevel, arch);

					string androidLibPath = string.Empty;
					try {
						androidLibPath = NdkUtil.GetNdkPlatformLibPath(AndroidNdkDirectory, arch, level);
					} catch (InvalidOperationException ex) {
						Diagnostic.Error (5101, ex.Message);
					}
					var libs = new List<string>() {
						QuoteFileName (Path.Combine(GetNdkToolchainLibraryDir(toolchainPath), "libgcc.a")),
						QuoteFileName (Path.Combine(androidLibPath, "libc.so")),
						QuoteFileName (Path.Combine(androidLibPath, "libm.so"))
					};
					ldFlags = string.Join(";", libs);
				}

				foreach (var assembly in ResolvedAssemblies) {
					string outputFile = Path.Combine(outdir, string.Format ("libaot-{0}.so",
						Path.GetFileName (assembly.ItemSpec)));

					string seqpointsFile = Path.Combine(outdir, string.Format ("{0}.msym",
						Path.GetFileName (assembly.ItemSpec)));

					string aotOptions = string.Format (
						"{0}--aot={9}{8}{1}outfile={2},asmwriter,mtriple={3},tool-prefix={4},ld-flags={5},llvm-path={6},temp-path={7}",
						EnableLLVM ? "--llvm " : string.Empty,
						AotMode != AotMode.Normal ? string.Format("{0},", AotMode.ToString().ToLowerInvariant()) : string.Empty,
						QuoteFileName (outputFile),
						mtriple,
						QuoteFileName (toolPrefix),
						ldFlags,
						QuoteFileName (SdkBinDirectory),
						QuoteFileName (outdir),
						sequencePointsMode == SequencePointsMode.Offline ? string.Format("msym-dir={0},", QuoteFileName(outdir)) : string.Empty,
						AotAdditionalArguments != string.Empty ? string.Format ("{0},", AotAdditionalArguments) : string.Empty
					);

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
					
					if (!RunAotCompiler (assembliesPath, aotCompiler, aotOptions, assemblyPath)) {
						Log.LogCodedError ("XA3001", "Could not AOT the assembly: {0}", assembly.ItemSpec);
						return false;
					}
					nativeLibs.Add (outputFile);
				}
			}

			NativeLibrariesReferences = nativeLibs.ToArray ();

			Log.LogDebugTaskItems ("Aot Outputs:");
			Log.LogDebugTaskItems ("  NativeLibrariesReferences: ", NativeLibrariesReferences);

			return true;
		}
			
		bool RunAotCompiler (string assembliesPath, string aotCompiler, string aotOptions, string assembly)
		{
			var arguments = aotOptions + " " + assembly;

			Log.LogMessage (MessageImportance.High, "[AOT] " + assembly);
			Log.LogMessage (MessageImportance.Low, "Mono arguments: " + arguments);
			Log.LogMessage (MessageImportance.Low, "MONO_PATH=" + assembliesPath);

			var psi = new ProcessStartInfo () {
				FileName = aotCompiler,
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow=true,
				WindowStyle=ProcessWindowStyle.Hidden,
			};
			
			// we do not want options to be provided out of band to the cross compilers
			psi.EnvironmentVariables ["MONO_ENV_OPTIONS"] = String.Empty;
			// the C code cannot parse all the license details, including the activation code that tell us which license level is allowed
			// so we provide this out-of-band to the cross-compilers - this can be extended to communicate a few others bits as well
			psi.EnvironmentVariables ["MONO_PATH"] = assembliesPath;

			var proc = new Process ();
			proc.OutputDataReceived += OnAotOutputData;
			proc.ErrorDataReceived += OnAotErrorData;
			proc.StartInfo = psi;
			proc.Start ();
			proc.BeginOutputReadLine ();
			proc.BeginErrorReadLine ();
			proc.WaitForExit ();
			return proc.ExitCode == 0;
		}

		void OnAotOutputData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				Log.LogMessage ("[aot-compiler stdout] {0}", e.Data);
		}

		void OnAotErrorData (object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				Log.LogMessage ("[aot-compiler stderr] {0}", e.Data);
		}

	}
}
