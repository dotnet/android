using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

class NativeCompilationHelper
{
	public sealed class AssemblerConfig
	{
		public readonly string AssemblerPath;
		public readonly List<string> AssemblerOptions;
		public readonly string InputSource;
		public readonly string OutputFile;
		public readonly string WorkingDirectory;
		public readonly TaskLoggingHelper Log;

		public CancellationToken? CancellationToken { get; set; }
		public Action? Cancel { get; set; }

		public AssemblerConfig (TaskLoggingHelper log, string assemblerPath, string inputSource, string workingDirectory, List<string>? assemblerOptions = null, string? outputFile = null)
		{
			Log = log;
			AssemblerPath = assemblerPath;
			InputSource = inputSource;
			WorkingDirectory = workingDirectory;

			if (String.IsNullOrEmpty (outputFile)) {
				OutputFile = Path.ChangeExtension (inputSource, ".o");
			} else {
				OutputFile = outputFile;
			}

			if (assemblerOptions != null) {
				AssemblerOptions = new List<string> (assemblerOptions);
			} else {
				AssemblerOptions = new List<string> (NativeCompilationHelper.DefaultAssemblerOptions);
			}

			AssemblerOptions.Add ($"-o={QuoteFileName(OutputFile)}");
			AssemblerOptions.Add (QuoteFileName (InputSource));
		}
	}

	public static readonly List<string> DefaultAssemblerOptions = new List<string> {
		"-O2",
		"--debugger-tune=lldb",
		"--debugify-level=location+variables",
		"--fatal-warnings",
		"--filetype=obj",
		"--relocation-model=pic",
	};

	public static string GetAssemblerPath (string androidBinUtilsDirectory)
	{
		string llcPath = Path.Combine (androidBinUtilsDirectory, "llc");
		string executableDir = Path.GetDirectoryName (llcPath);
		string executableName = MonoAndroidHelper.GetExecutablePath (executableDir, Path.GetFileName (llcPath));

		return Path.Combine (executableDir, executableName);
	}

	static string QuoteFileName (string fileName)
	{
		var builder = new CommandLineBuilder ();
		builder.AppendFileNameIfNotNull (fileName);
		return builder.ToString ();
	}

	public static void RunAssembler (AssemblerConfig config)
	{
		var stdout_completed = new ManualResetEvent (false);
		var stderr_completed = new ManualResetEvent (false);
		var psi = new ProcessStartInfo () {
			FileName = config.AssemblerPath,
			Arguments = String.Join (" ", config.AssemblerOptions),
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
			WorkingDirectory = config.WorkingDirectory,
		};

		string assemblerName = Path.GetFileName (config.AssemblerPath);
		config.Log.LogDebugMessage ($"[LLVM llc] {psi.FileName} {psi.Arguments}");

		var stdoutLines = new List<string> ();
		var stderrLines = new List<string> ();

		using (var proc = new Process ()) {
			proc.OutputDataReceived += (s, e) => {
				if (e.Data != null) {
					OnOutputData (config.Log, assemblerName, s, e);
					stdoutLines.Add (e.Data);
				} else
				stdout_completed.Set ();
			                                     };

			proc.ErrorDataReceived += (s, e) => {
				if (e.Data != null) {
					OnErrorData (config.Log, assemblerName, s, e);
					stderrLines.Add (e.Data);
				} else
					stderr_completed.Set ();
			};

			proc.StartInfo = psi;
			proc.Start ();
			proc.BeginOutputReadLine ();
			proc.BeginErrorReadLine ();
			config.CancellationToken?.Register (() => { try { proc.Kill (); } catch (Exception) { } });
			proc.WaitForExit ();

			if (psi.RedirectStandardError)
				stderr_completed.WaitOne (TimeSpan.FromSeconds (30));

			if (psi.RedirectStandardOutput)
				stdout_completed.WaitOne (TimeSpan.FromSeconds (30));

			if (proc.ExitCode != 0) {
				var sb = MonoAndroidHelper.MergeStdoutAndStderrMessages (stdoutLines, stderrLines);
				config.Log.LogCodedError ("XA3006", Properties.Resources.XA3006, Path.GetFileName (config.InputSource), sb.ToString ());
				if (config.Cancel != null) {
					config.Cancel ();
				}
			}
		}
	}

	static void OnOutputData (TaskLoggingHelper log, string assemblerName, object sender, DataReceivedEventArgs e)
	{
		log.LogDebugMessage ($"[{assemblerName} stdout] {e.Data}");
	}

	static void OnErrorData (TaskLoggingHelper log, string assemblerName, object sender, DataReceivedEventArgs e)
	{
		log.LogMessage ($"[{assemblerName} stderr] {e.Data}", MessageImportance.High);
	}
}
