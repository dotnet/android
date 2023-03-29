using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;
using Xamarin.Build;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class LinkApplicationSharedLibraries : AndroidAsyncTask
	{
		const int NDK_API_LEVEL = 21; // TODO: don't hardcode the API level

		public override string TaskPrefix => "LAS";

		sealed class Config
		{
			public string LinkerPath;
			public string LinkerOptions;
			public string OutputSharedLibrary;
		}

		sealed class InputFiles
		{
			public List<string> ObjectFiles;
			public string OutputSharedLibrary;
			public List<string> ExtraLibraries;
		}

		[Required]
		public ITaskItem[] ObjectFiles { get; set; }

		[Required]
		public ITaskItem[] ApplicationSharedLibraries { get; set; }

		[Required]
		public bool DebugBuild { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		public string AndroidNdkDirectory { get; set; }

		public bool EnableMarshalMethodTracing { get; set; }

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			return this.WhenAll (GetLinkerConfigs (), RunLinker);
		}

		void RunLinker (Config config)
		{
			var stdout_completed = new ManualResetEvent (false);
			var stderr_completed = new ManualResetEvent (false);
			var psi = new ProcessStartInfo () {
				FileName = config.LinkerPath,
				Arguments = config.LinkerOptions,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};

			string targetDir = Path.GetDirectoryName (config.OutputSharedLibrary);
			if (!Directory.Exists (targetDir))
				Directory.CreateDirectory (targetDir);

			string linkerName = Path.GetFileName (config.LinkerPath);
			LogDebugMessage ($"[Native Linker] {psi.FileName} {psi.Arguments}");

			var stdoutLines = new List<string> ();
			var stderrLines = new List<string> ();

			using (var proc = new Process ()) {
				proc.OutputDataReceived += (s, e) => {
					if (e.Data != null) {
						OnOutputData (linkerName, s, e);
						stdoutLines.Add (e.Data);
					} else
						stdout_completed.Set ();
				};

				proc.ErrorDataReceived += (s, e) => {
					if (e.Data != null) {
						OnErrorData (linkerName, s, e);
						stderrLines.Add (e.Data);
					} else
						stderr_completed.Set ();
				};

				proc.StartInfo = psi;
				proc.Start ();
				proc.BeginOutputReadLine ();
				proc.BeginErrorReadLine ();
				CancellationToken.Register (() => { try { proc.Kill (); } catch (Exception) { } });
				proc.WaitForExit ();

				if (psi.RedirectStandardError)
					stderr_completed.WaitOne (TimeSpan.FromSeconds (30));

				if (psi.RedirectStandardOutput)
					stdout_completed.WaitOne (TimeSpan.FromSeconds (30));

				if (proc.ExitCode != 0) {
					var sb = MonoAndroidHelper.MergeStdoutAndStderrMessages (stdoutLines, stderrLines);
					LogCodedError ("XA3007", Properties.Resources.XA3007, Path.GetFileName (config.OutputSharedLibrary), sb.ToString ());
					Cancel ();
				}
			}
		}

		IEnumerable<Config> GetLinkerConfigs ()
		{
			NdkTools ndk = null;
			string clangRuntimeDirTop = null;

			if (EnableMarshalMethodTracing) {
				ndk = NdkTools.Create (AndroidNdkDirectory, logErrors: false, log: Log);

				// Doesn't matter for which arch we run the compiler, they all share the same topmost runtime dir
				string clangPath = ndk.GetToolPath (NdkToolKind.CompilerCPlusPlus, AndroidTargetArch.Arm64, NDK_API_LEVEL);
				int ret = MonoAndroidHelper.RunProcess (
					clangPath, "-print-runtime-dir",
					(object sender, DataReceivedEventArgs e) => { // stdout
						if (clangRuntimeDirTop == null && !String.IsNullOrEmpty (e.Data)) {
							clangRuntimeDirTop = e.Data;
						}
					},
					(object sender, DataReceivedEventArgs e) => { // stderr
						if (!String.IsNullOrEmpty (e.Data)) {
							Log.LogError (e.Data);
						}
					}
				);

				if (ret != 0) {
					Log.LogError ($"Failed to obtain clang runtime path from {clangPath}");
				}
			}

			string runtimeNativeLibsDir = Path.GetFullPath (Path.Combine (AndroidBinUtilsDirectory, "..", "..", "..", "lib"));
			var abis = new Dictionary <string, InputFiles> (StringComparer.Ordinal);
			ITaskItem[] dsos = ApplicationSharedLibraries;
			foreach (ITaskItem item in dsos) {
				string abi = item.GetMetadata ("abi");
				abis [abi] = GatherFilesForABI (item.ItemSpec, abi, ObjectFiles, runtimeNativeLibsDir, ndk, clangRuntimeDirTop);
			}

			const string commonLinkerArgs =
				"--shared " +
				"--allow-shlib-undefined " +
				"--unresolved-symbols=ignore-in-shared-libs " +
				"--export-dynamic " +
				"-soname libxamarin-app.so " +
				"-z relro " +
				"-z noexecstack " +
				"--enable-new-dtags " +
				"--eh-frame-hdr " +
				"-shared " +
				"--build-id " +
				"--warn-shared-textrel " +
				"--fatal-warnings";

			string stripSymbolsArg = DebugBuild || EnableMarshalMethodTracing ? String.Empty : " -s";

			string ld = Path.Combine (AndroidBinUtilsDirectory, MonoAndroidHelper.GetExecutablePath (AndroidBinUtilsDirectory, "ld"));
			var targetLinkerArgs = new List<string> ();
			foreach (var kvp in abis) {
				string abi = kvp.Key;
				InputFiles inputs = kvp.Value;

				targetLinkerArgs.Clear ();
				string elf_arch;
				switch (abi) {
					case "armeabi-v7a":
						targetLinkerArgs.Add ("-X");
						elf_arch = "armelf_linux_eabi";
						break;

					case "arm64":
					case "arm64-v8a":
					case "aarch64":
						targetLinkerArgs.Add ("--fix-cortex-a53-843419");
						elf_arch = "aarch64linux";
						break;

					case "x86":
						elf_arch = "elf_i386";
						break;

					case "x86_64":
						elf_arch = "elf_x86_64";
						break;

					default:
						throw new NotSupportedException ($"Unsupported Android target architecture ABI: {abi}");
				}

				targetLinkerArgs.Add ("-m");
				targetLinkerArgs.Add (elf_arch);

				foreach (string file in inputs.ObjectFiles) {
					targetLinkerArgs.Add (QuoteFileName (file));
				}

				targetLinkerArgs.Add ("-o");
				targetLinkerArgs.Add (QuoteFileName (inputs.OutputSharedLibrary));

				if (inputs.ExtraLibraries != null) {
					foreach (string lib in inputs.ExtraLibraries) {
						targetLinkerArgs.Add (lib);
					}
				}

				string targetArgs = String.Join (" ", targetLinkerArgs);
				yield return new Config {
					LinkerPath = ld,
					LinkerOptions = $"{commonLinkerArgs}{stripSymbolsArg} {targetArgs}",
					OutputSharedLibrary = inputs.OutputSharedLibrary,
				};
			}
		}

		InputFiles GatherFilesForABI (string runtimeSharedLibrary, string abi, ITaskItem[] objectFiles, string runtimeNativeLibsDir, NdkTools ndk, string clangRuntimeDirTop)
		{
			List<string> extraLibraries = null;

			if (EnableMarshalMethodTracing) {
				if (ndk == null) {
					throw new ArgumentNullException (nameof (ndk));
				}

				AndroidTargetArch targetArch = MonoAndroidHelper.AbiToTargetArch (abi);
				string clangRuntimeAbi = MonoAndroidHelper.ArchToClangRuntimeAbi (targetArch);
				string clangLibraryAbi = MonoAndroidHelper.ArchToClangLibraryAbi (targetArch);
				string unwindLibPath = Path.GetFullPath (Path.Combine (clangRuntimeDirTop, clangRuntimeAbi, "libunwind.a"));
				string builtinsLibPath = Path.GetFullPath (Path.Combine (clangRuntimeDirTop, $"libclang_rt.builtins-{clangLibraryAbi}-android.a"));
				string libPath = ndk.GetDirectoryPath (NdkToolchainDir.PlatformLib, targetArch, NDK_API_LEVEL);
				string cxxAbiLibPath = Path.GetFullPath (Path.Combine (libPath, "..", "libc++abi.a"));

				extraLibraries = new List<string> {
					Path.Combine (runtimeNativeLibsDir, MonoAndroidHelper.AbiToRid (abi), "libmarshal-methods-tracing.a"),
					$"-L \"{libPath}\"",
					$"\"{builtinsLibPath}\"",
					$"\"{cxxAbiLibPath}\"",
					$"\"{unwindLibPath}\"",
					"-lc",
					"-ldl",
					"-llog", // tracing uses android logger
				};
			}

			return new InputFiles {
				OutputSharedLibrary = runtimeSharedLibrary,
				ObjectFiles = GetItemsForABI (abi, objectFiles),
				ExtraLibraries = extraLibraries,
			};
		}

		List<string> GetItemsForABI (string abi, ITaskItem[] items)
		{
			var ret = new List <string> ();
			foreach (ITaskItem item in items) {
				if (String.Compare (abi, item.GetMetadata ("abi"), StringComparison.Ordinal) != 0)
					continue;
				ret.Add (item.ItemSpec);
			}

			return ret;
		}

		void OnOutputData (string linkerName, object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ($"[{linkerName} stdout] {e.Data}");
		}

		void OnErrorData (string linkerName, object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
				LogMessage ($"[{linkerName} stderr] {e.Data}");
		}

		static string QuoteFileName (string fileName)
		{
			var builder = new CommandLineBuilder ();
			builder.AppendFileNameIfNotNull (fileName);
			return builder.ToString ();
		}
	}
}
