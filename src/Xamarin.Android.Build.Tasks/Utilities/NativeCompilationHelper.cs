using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class NativeCompilationHelper
{
	public abstract class Config
	{
		public readonly string ExecutablePath;
		public readonly List<string> ProcessOptions;
		public readonly TaskLoggingHelper Log;
		public readonly string OutputFile;
		public readonly string? WorkingDirectory;
		public readonly AndroidTargetArch TargetArch;

		public object? State { get; set; }
		public CancellationToken? CancellationToken { get; set; }
		public Action? Cancel { get; set; }

		protected Config (TaskLoggingHelper log, AndroidTargetArch targetArch, string executablePath, string outputFile, List<string> processOptions, string? workingDirectory)
		{
			Log = log;
			TargetArch = targetArch;
			ExecutablePath = EnsureValidString (executablePath, nameof (executablePath));
			OutputFile = EnsureValidString (outputFile, nameof (outputFile));
			ProcessOptions = new List<string> (processOptions);
			WorkingDirectory = workingDirectory;
		}

		string EnsureValidString (string v, string name)
		{
			if (String.IsNullOrEmpty (v)) {
				throw new ArgumentException ("must not be null or empty", name);
			}

			return v;
		}
	}

	public sealed class LinkerConfig : Config
	{
		public readonly List<string> ObjectFilePaths;
		public readonly List<string> ExtraLibraries;

		public LinkerConfig (TaskLoggingHelper log, AndroidTargetArch targetArch, string linkerPath, string outputSharedLibrary, List<string>? linkerOptions = null, string? workingDirectory = null)
			: base (
				log,
				targetArch,
				linkerPath,
				outputSharedLibrary,
				linkerOptions != null ? linkerOptions : NativeCompilationHelper.CommonLinkerArgs,
				workingDirectory
			)
		{
			AddArchOptions (ProcessOptions, targetArch);
			ProcessOptions.Add ($"-soname {Path.GetFileName(OutputFile)}");
			ProcessOptions.Add ($"-o {QuoteFileName(OutputFile)}");

			ObjectFilePaths = new List<string> ();
			ExtraLibraries = new List<string> ();
		}

		void AddArchOptions (List<string> options, AndroidTargetArch arch)
		{
			string elfArch;
			switch (arch) {
				case AndroidTargetArch.Arm:
					options.Add ("-X");
					elfArch = "armelf_linux_eabi";
					break;

				case AndroidTargetArch.Arm64:
					options.Add ("--fix-cortex-a53-843419");
					elfArch = "aarch64linux";
					break;

				case AndroidTargetArch.X86:
					elfArch = "elf_i386";
					break;

				case AndroidTargetArch.X86_64:
					elfArch = "elf_x86_64";
					break;

				default:
					throw new NotSupportedException ($"Unsupported Android target architecture '{arch}'");
			}

			options.Add ("-m");
			options.Add (elfArch);
		}
	}

	public sealed class AssemblerConfig : Config
	{
		public readonly string InputSource;

		public AssemblerConfig (TaskLoggingHelper log, AndroidTargetArch targetArch, string assemblerPath, string inputSource, string workingDirectory, List<string>? assemblerOptions = null, string? outputFile = null)
			: base (
				log,
				targetArch,
				assemblerPath,
				String.IsNullOrEmpty (outputFile) ? Path.ChangeExtension (inputSource, ".o") : outputFile,
				assemblerOptions != null ? assemblerOptions : NativeCompilationHelper.DefaultAssemblerOptions,
				workingDirectory
			)
		{
			InputSource = inputSource;

			ProcessOptions.Add ($"-o={QuoteFileName(OutputFile)}");
			ProcessOptions.Add (QuoteFileName (InputSource));
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

	public static readonly List<string> CommonLinkerArgs = new List<string> {
		"--shared",
		"--allow-shlib-undefined",
		"--export-dynamic",
		"-z relro",
		"-z noexecstack",
		"--enable-new-dtags",
		"--build-id",
		"--warn-shared-textrel",
		"--fatal-warnings",
	};

	public static string GetAssemblerPath (string androidBinUtilsDirectory)
	{
		string llcPath = Path.Combine (androidBinUtilsDirectory, "llc");
		string executableDir = Path.GetDirectoryName (llcPath);
		string executableName = MonoAndroidHelper.GetExecutablePath (executableDir, Path.GetFileName (llcPath));

		return Path.Combine (executableDir, executableName);
	}

	public static string GetLinkerPath (string androidBinUtilsDirectory)
	{
		return Path.Combine (androidBinUtilsDirectory, MonoAndroidHelper.GetExecutablePath (androidBinUtilsDirectory, "ld"));
	}

	static string QuoteFileName (string fileName)
	{
		var builder = new CommandLineBuilder ();
		builder.AppendFileNameIfNotNull (fileName);
		return builder.ToString ();
	}

	static void CreateOutputDirectory (string filePath)
	{
		string? dirPath = Path.GetDirectoryName (filePath);
		if (!String.IsNullOrEmpty (dirPath)) {
			Directory.CreateDirectory (dirPath);
		}
	}

	public static bool RunLinker (LinkerConfig config)
	{
		if (config.ObjectFilePaths.Count > 0) {
			foreach (string objFile in config.ObjectFilePaths) {
				config.ProcessOptions.Add (QuoteFileName (objFile));
			}
		}

		if (config.ExtraLibraries.Count > 0) {
			foreach (string libName in config.ObjectFilePaths) {
				config.ProcessOptions.Add ($"-l{libName}");
			}
		}

		ProcessStartInfo psi = CreateProcessStartInfo (config);
		return RunProcess (
			config,
			psi,
			"LLVM ld",
			(StringBuilder sb) => config.Log.LogCodedError ("XA3007", Properties.Resources.XA3007, Path.GetFileName (config.OutputFile), sb.ToString ())
		);
	}

	public static bool RunAssembler (AssemblerConfig config)
	{
		ProcessStartInfo psi = CreateProcessStartInfo (config);
		return RunProcess (
			config,
			psi,
			"LLVM llc",
			(StringBuilder sb) => config.Log.LogCodedError ("XA3006", Properties.Resources.XA3006, Path.GetFileName (config.InputSource), sb.ToString ())
		);
	}

	static bool RunProcess (Config config, ProcessStartInfo psi, string processDisplayName, Action<StringBuilder> onError)
	{
		CreateOutputDirectory (config.OutputFile);

		using var stdout_completed = new ManualResetEvent (false);
		using var stderr_completed = new ManualResetEvent (false);

		string assemblerName = Path.GetFileName (config.ExecutablePath);
		config.Log.LogDebugMessage ($"[{processDisplayName}] {psi.FileName} {psi.Arguments}");

		var stdoutLines = new List<string> ();
		var stderrLines = new List<string> ();

		using var proc = new Process ();
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
			onError (sb);
			if (config.Cancel != null) {
				config.Cancel ();
			}
			return false;
		}

		return true;
	}

	static ProcessStartInfo CreateProcessStartInfo (Config config)
	{
		var psi = new ProcessStartInfo {
			FileName = config.ExecutablePath,
			Arguments = String.Join (" ", config.ProcessOptions),
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		};

		if (!String.IsNullOrEmpty (config.WorkingDirectory)) {
			psi.WorkingDirectory = config.WorkingDirectory;
		}

		return psi;
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
