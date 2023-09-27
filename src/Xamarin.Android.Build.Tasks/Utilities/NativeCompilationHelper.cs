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
	public sealed class Config
	{
		public readonly string AssemblerPath;
		public readonly string AssemblerOptions;
		public readonly string InputSource;
		public readonly string WorkingDirectory;
		public readonly TaskLoggingHelper Log;

		public CancellationToken? CancellationToken { get; set; }
		public Action? Cancel { get; set; }

		public Config (TaskLoggingHelper log, string assemblerPath, string assemblerOptions, string inputSource, string workingDirectory)
		{
			Log = log;
			AssemblerPath = assemblerPath;
			AssemblerOptions = assemblerOptions;
			InputSource = inputSource;
			WorkingDirectory = workingDirectory;
		}
	}

	public const string DefaultAssemblerOptions =
		"-O2 " +
		"--debugger-tune=lldb " + // NDK uses lldb now
		"--debugify-level=location+variables " +
		"--fatal-warnings " +
		"--filetype=obj " +
		"--relocation-model=pic";

	public static string GetAssemblerPath (string androidBinUtilsDirectory)
	{
		string llcPath = Path.Combine (androidBinUtilsDirectory, "llc");
		string executableDir = Path.GetDirectoryName (llcPath);
		string executableName = MonoAndroidHelper.GetExecutablePath (executableDir, Path.GetFileName (llcPath));

		return Path.Combine (executableDir, executableName);
	}

	/// <summary>
	/// Construct native assembler (`llc`) parameters from the provided components.  The only required parameter is <paramref name="sourceFile"/>,
	/// which is used to construct the output file path by replacing its extension with `.o`, unless <paramref name="outputFile"/> is provided.
	///
	/// If <paramref name="commonOptions"/>, then the <see cref="DefaultAssemblerOptions"/> constant will be used.  If, on the other hand, it **is**
	/// provided, it's the caller's responsibility to provide all the necessary options.
	/// </summary>
	public static string MakeAssemblerOptions (string sourceFile, string? commonOptions = null, string? outputFile = null)
	{
		if (String.IsNullOrEmpty (outputFile)) {
			outputFile = Path.ChangeExtension (sourceFile, ".o");
		}

		string standardOptions = String.IsNullOrEmpty (commonOptions) ? DefaultAssemblerOptions : commonOptions;
		return $"{standardOptions} -o={QuoteFileName (outputFile)} {QuoteFileName (sourceFile)}";
	}

	static string QuoteFileName (string fileName)
	{
		var builder = new CommandLineBuilder ();
		builder.AppendFileNameIfNotNull (fileName);
		return builder.ToString ();
	}

	public static void RunAssembler (Config config)
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
