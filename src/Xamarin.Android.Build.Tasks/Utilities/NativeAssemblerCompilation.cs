using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class NativeAssemblerCompilation
{
	public sealed class AssemblerConfig
	{
		public readonly string ExecutablePath;
		public readonly string Options;
		public readonly string InputSource;

		public AssemblerConfig (string executablePath, string options, string inputSource)
		{
			ExecutablePath = executablePath;
			Options = options;
			InputSource = inputSource;
		}
	}

	public sealed class AssemblerRunContext
	{
		public readonly TaskLoggingHelper Log;
		public readonly Action<Process>? RegisterForCancellation;
		public readonly Action? Cancel;
		public readonly string? WorkingDirectory;

		public AssemblerRunContext (TaskLoggingHelper log, string? workingDirectory = null, Action<Process>? registerForCancellation = null, Action? cancel = null)
		{
			Log = log;
			RegisterForCancellation = registerForCancellation;
			Cancel = cancel;
			WorkingDirectory = workingDirectory;
		}
	}

	public sealed class LlvmMcTargetConfig
	{
		public readonly string TargetArch;
		public readonly string TripleArch;
		public readonly string TripleApiPrefix;
		public readonly string AssemblerDirectivePrefix;
		public readonly string SizeType;
		public readonly uint WordSize;

		public LlvmMcTargetConfig (string targetArch, string tripleArch, string tripleApiPrefix, string assemblerDirectivePrefix, string sizeType, uint wordSize)
		{
			TargetArch = targetArch;
			TripleArch = tripleArch;
			TripleApiPrefix = tripleApiPrefix;
			AssemblerDirectivePrefix = assemblerDirectivePrefix;
			SizeType = sizeType;
			WordSize = wordSize;
		}
	}

	static readonly Dictionary<AndroidTargetArch, LlvmMcTargetConfig> llvmMcConfigs = new () {
		{ AndroidTargetArch.Arm64,  new ("aarch64", "aarch64", "android",     "@", ".xword", 8) },
		{ AndroidTargetArch.Arm,    new ("arm",     "armv7a",  "androideabi", "%", ".long",  4) },
		{ AndroidTargetArch.X86_64, new ("x86-64",  "x86_64",  "android",     "@", ".quad",  8) },
		{ AndroidTargetArch.X86,    new ("x86",     "i686",    "android",     "@", ".long",  4) },
	};

	static readonly List<string> llcArguments = new () {
		"-O2",
		"--debugger-tune=lldb", // NDK uses lldb now
		"--debugify-level=location+variables",
		"--fatal-warnings",
		"--filetype=obj",
		"--relocation-model=pic",
	};

	static readonly List<string> llvmMcArguments = new () {
		"--assemble",
		"--filetype=obj",
		"-g",
	};

	public static AssemblerConfig GetAssemblerConfig (string androidBinUtilsDir, ITaskItem source, bool stripFilePaths)
	{
		string sourceFile = stripFilePaths ? Path.GetFileName (source.ItemSpec) : source.ItemSpec;
		string sourceExtension = Path.GetExtension (sourceFile);
		string executable;
		var arguments = new List<string> ();

		if (String.Compare (".ll", sourceExtension, StringComparison.OrdinalIgnoreCase) == 0) {
			executable = MonoAndroidHelper.GetLlvmLlcPath (androidBinUtilsDir);
			arguments.AddRange (llcArguments);
		} else if (String.Compare (".s", sourceExtension, StringComparison.OrdinalIgnoreCase) == 0) {
			executable = MonoAndroidHelper.GetLlvmMcPath (androidBinUtilsDir);
			arguments.AddRange (llvmMcArguments);

			LlvmMcTargetConfig cfg = GetLlvmMcConfig (MonoAndroidHelper.GetTargetArch (source));
			arguments.Add ($"--arch={cfg.TargetArch}");
			arguments.Add ($"--triple={cfg.TripleArch}-linux-{cfg.TripleApiPrefix}{XABuildConfig.AndroidMinimumDotNetApiLevel}");
		} else {
			throw new InvalidOperationException ($"Internal exception: unknown native assembler source {source.ItemSpec}");
		}

		string outputFile = Path.ChangeExtension (sourceFile, ".o");
		arguments.Add ("-o");
		arguments.Add (MonoAndroidHelper.QuoteFileNameArgument (outputFile));
		arguments.Add (MonoAndroidHelper.QuoteFileNameArgument (sourceFile));

		return new AssemblerConfig (executable, String.Join (" ", arguments), source.ItemSpec);
	}

	public static LlvmMcTargetConfig GetLlvmMcConfig (AndroidTargetArch arch)
	{
		if (!llvmMcConfigs.TryGetValue (arch, out LlvmMcTargetConfig cfg)) {
			throw new NotSupportedException ($"Internal error: unsupported target arch '{arch}'");
		}

		return cfg;
	}

	public static void RunAssembler (AssemblerRunContext context, AssemblerConfig config)
	{
		var stdout_completed = new ManualResetEvent (false);
		var stderr_completed = new ManualResetEvent (false);
		var psi = new ProcessStartInfo () {
			FileName = config.ExecutablePath,
			Arguments = config.Options,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
			WorkingDirectory = context.WorkingDirectory,
		};

		string assemblerName = Path.GetFileName (config.ExecutablePath);
		context.Log.LogDebugMessage ($"[{assemblerName}] {psi.FileName} {psi.Arguments}");

		var stdoutLines = new List<string> ();
		var stderrLines = new List<string> ();

		using var proc = new Process ();
		proc.OutputDataReceived += (s, e) => {
			if (e.Data != null) {
				OnOutputData (context, assemblerName, s, e);
				stdoutLines.Add (e.Data);
			} else {
				stdout_completed.Set ();
			}
		};

		proc.ErrorDataReceived += (s, e) => {
			if (e.Data != null) {
				OnErrorData (context, assemblerName, s, e);
				stderrLines.Add (e.Data);
			} else {
				stderr_completed.Set ();
			}
		};

		proc.StartInfo = psi;
		proc.Start ();
		proc.BeginOutputReadLine ();
		proc.BeginErrorReadLine ();
		context.RegisterForCancellation?.Invoke (proc);

		proc.WaitForExit ();

		if (psi.RedirectStandardError) {
			stderr_completed.WaitOne (TimeSpan.FromSeconds (30));
		}

		if (psi.RedirectStandardOutput) {
			stdout_completed.WaitOne (TimeSpan.FromSeconds (30));
		}

		if (proc.ExitCode != 0) {
			var sb = MonoAndroidHelper.MergeStdoutAndStderrMessages (stdoutLines, stderrLines);
			context.Log.LogCodedError ("XA3006", Properties.Resources.XA3006, Path.GetFileName (config.InputSource), sb.ToString ());
			context.Cancel?.Invoke ();
		}
	}

	static void OnOutputData (AssemblerRunContext context, string assemblerName, object sender, DataReceivedEventArgs e)
	{
		context.Log.LogDebugMessage ($"[{assemblerName} stdout] {e.Data}");
	}

	static void OnErrorData (AssemblerRunContext context, string assemblerName, object sender, DataReceivedEventArgs e)
	{
		context.Log.LogMessage ($"[{assemblerName} stderr] {e.Data}", MessageImportance.High);
	}
}
