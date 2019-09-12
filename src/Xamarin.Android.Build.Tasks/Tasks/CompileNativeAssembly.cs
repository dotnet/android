using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;
using Xamarin.Build;

namespace Xamarin.Android.Tasks
{
	public class CompileNativeAssembly : AndroidAsyncTask
	{
		public override string TaskPrefix => "CNA";

		sealed class Config
		{
			public string AssemblerPath;
			public string AssemblerOptions;
			public string InputSource;
		}

		[Required]
		public ITaskItem[] Sources { get; set; }

		[Required]
		public bool DebugBuild { get; set; }

		[Required]
		public string WorkingDirectory { get; set; }

		[Required]
		public string AndroidBinUtilsDirectory { get; set; }

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			return this.WhenAll (GetAssemblerConfigs (), RunAssembler);
		}

		void RunAssembler (Config config)
		{
			var stdout_completed = new ManualResetEvent (false);
			var stderr_completed = new ManualResetEvent (false);
			var psi = new ProcessStartInfo () {
				FileName = config.AssemblerPath,
				Arguments = config.AssemblerOptions,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				WorkingDirectory = WorkingDirectory,
			};

			string assemblerName = Path.GetFileName (config.AssemblerPath);
			LogDebugMessage ($"[Native Assembler] {psi.FileName} {psi.Arguments}");
			using (var proc = new Process ()) {
				proc.OutputDataReceived += (s, e) => {
					if (e.Data != null)
						OnOutputData (assemblerName, s, e);
					else
						stdout_completed.Set ();
				};

				proc.ErrorDataReceived += (s, e) => {
					if (e.Data != null)
						OnErrorData (assemblerName, s, e);
					else
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
					LogCodedError ("XA3001", $"Could not compile native assembly file: {Path.GetFileName (config.InputSource)}");
					Cancel ();
				}
			}
		}

		IEnumerable<Config> GetAssemblerConfigs ()
		{
			foreach (ITaskItem item in Sources) {
				string abi = item.GetMetadata ("abi")?.ToLowerInvariant ();
				string prefix = String.Empty;
				AndroidTargetArch arch;

				switch (abi) {
					case "armeabi-v7a":
						prefix = Path.Combine (AndroidBinUtilsDirectory, "arm-linux-androideabi");
						arch = AndroidTargetArch.Arm;
						break;

					case "arm64":
					case "arm64-v8a":
					case "aarch64":
						prefix = Path.Combine (AndroidBinUtilsDirectory, "aarch64-linux-android");
						arch = AndroidTargetArch.Arm64;
						break;

					case "x86":
						prefix = Path.Combine (AndroidBinUtilsDirectory, "i686-linux-android");
						arch = AndroidTargetArch.X86;
						break;

					case "x86_64":
						prefix = Path.Combine (AndroidBinUtilsDirectory, "x86_64-linux-android");
						arch = AndroidTargetArch.X86_64;
						break;

					default:
						throw new NotSupportedException ($"Unsupported Android target architecture ABI: {abi}");
				}

				// We don't need the directory since our WorkingDirectory is (and must be) where all the
				// sources are (because of the typemap.inc file being included by the other sources with
				// a relative path of `.`)
				string sourceFile = Path.GetFileName (item.ItemSpec);
				var assemblerOptions = new List<string> {
					"--warn",
					"-o",
					QuoteFileName (sourceFile.Replace (".s", ".o"))
				};

				if (DebugBuild)
					assemblerOptions.Add ("-g");

				assemblerOptions.Add (QuoteFileName (sourceFile));

				string baseExecutablePath = $"{prefix}-as";
				string executableDir = Path.GetDirectoryName (baseExecutablePath);
				string executableName = MonoAndroidHelper.GetExecutablePath (executableDir, Path.GetFileName (baseExecutablePath));

				yield return new Config {
					InputSource = item.ItemSpec,
					AssemblerPath = Path.Combine (executableDir, executableName),
					AssemblerOptions = String.Join (" ", assemblerOptions),
				};
			}
		}

		void OnOutputData (string assemblerName, object sender, DataReceivedEventArgs e)
		{
			LogDebugMessage ($"[{assemblerName} stdout] {e.Data}");
		}

		void OnErrorData (string assemblerName, object sender, DataReceivedEventArgs e)
		{
			LogMessage ($"[{assemblerName} stderr] {e.Data}", MessageImportance.High);
		}

		static string QuoteFileName (string fileName)
		{
			var builder = new CommandLineBuilder ();
			builder.AppendFileNameIfNotNull (fileName);
			return builder.ToString ();
		}
	}
}
