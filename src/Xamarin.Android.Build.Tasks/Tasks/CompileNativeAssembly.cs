using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class CompileNativeAssembly : AsyncTask
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
		public new string WorkingDirectory { get; set; }

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
			LogDebugMessage ($"[LLVM llc] {psi.FileName} {psi.Arguments}");

			var stdoutLines = new List<string> ();
			var stderrLines = new List<string> ();

			using (var proc = new Process ()) {
				proc.OutputDataReceived += (s, e) => {
					if (e.Data != null) {
						OnOutputData (assemblerName, s, e);
						stdoutLines.Add (e.Data);
					} else
						stdout_completed.Set ();
				};

				proc.ErrorDataReceived += (s, e) => {
					if (e.Data != null) {
						OnErrorData (assemblerName, s, e);
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
					LogCodedError ("XA3006", Properties.Resources.XA3006, Path.GetFileName (config.InputSource), sb.ToString ());
					Cancel ();
				}
			}
		}

		IEnumerable<Config> GetAssemblerConfigs ()
		{
			const string assemblerOptions =
				"-O2 " +
				"--debugger-tune=lldb " + // NDK uses lldb now
				"--debugify-level=location+variables " +
				"--fatal-warnings " +
				"--filetype=obj " +
				"--relocation-model=pic";
			string llcPath = Path.Combine (AndroidBinUtilsDirectory, "llc");

			foreach (ITaskItem item in Sources) {
				// We don't need the directory since our WorkingDirectory is where all the sources are
				string sourceFile = Path.GetFileName (item.ItemSpec);
				string outputFile = QuoteFileName (sourceFile.Replace (".ll", ".o"));
				string executableDir = Path.GetDirectoryName (llcPath);
				string executableName = MonoAndroidHelper.GetExecutablePath (executableDir, Path.GetFileName (llcPath));

				yield return new Config {
					InputSource = item.ItemSpec,
					AssemblerPath = Path.Combine (executableDir, executableName),
					AssemblerOptions = $"{assemblerOptions} -o={outputFile} {QuoteFileName (sourceFile)}",
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
