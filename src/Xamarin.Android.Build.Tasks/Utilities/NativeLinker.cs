using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

class NativeLinker
{
	static readonly List<string> standardArgs = new () {
		"--shared",
		// TODO: need to enable zstd in binutils build
		// "--compress-debug-sections=zstd",
		// TODO: test the commented-out flags
		"--gc-sections",
		// "--icf=safe",
		// "--lto=full|thin",
		"--export-dynamic",
		"-z relro",
		"-z noexecstack",
		"-z max-page-size=16384",
		"-z now", // we need it for security reasons (without it PLT can be overwritten)
		"--enable-new-dtags",
		"--build-id=sha1",
		"--warn-shared-textrel",
		"--fatal-warnings",
		"--no-rosegment",
	};

	readonly List<string> extraArgs = new ();
	readonly TaskLoggingHelper log;
	readonly string abi;
	readonly string ld;
	readonly string objcopy;
	readonly string intermediateDir;
	readonly CancellationToken? cancellationToken;
	readonly Action? cancelTask;

	public bool StripDebugSymbols { get; set; } = true;
	public bool SaveDebugSymbols  { get; set; } = true;
	public bool AllowUndefinedSymbols { get; set; } = false;
	public bool UseNdkLibraries { get; set; } = false;
	public bool TargetsCLR { get; set; }
	public string? NdkRootPath { get; set; }
	public string? NdkApiLevel { get; set; }
	public int ZipAlignmentPages { get; set; } = AndroidZipAlign.DefaultZipAlignment64Bit;

	public NativeLinker (TaskLoggingHelper log, string abi, string soname, string binutilsDir, string intermediateDir,
	                     IEnumerable<ITaskItem> runtimePackLibDirs, CancellationToken? cancellationToken = null, Action? cancelTask = null)
	{
		this.log = log;
		this.abi = abi;
		this.intermediateDir = intermediateDir;
		this.cancellationToken = cancellationToken;
		this.cancelTask = cancelTask;

		ld = Path.Combine (binutilsDir, MonoAndroidHelper.GetExecutablePath (binutilsDir, "ld"));
		objcopy = Path.Combine (binutilsDir, MonoAndroidHelper.GetExecutablePath (binutilsDir, "llvm-objcopy"));

		extraArgs.Add ($"-soname {soname}");

		string? elfArch = null;
		uint maxPageSize;
		switch (abi) {
			case "armeabi-v7a":
				extraArgs.Add ("-X");
				elfArch = "armelf_linux_eabi";
				maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (AndroidZipAlign.ZipAlignment32Bit);
				break;

			case "arm64":
			case "arm64-v8a":
			case "aarch64":
				extraArgs.Add ("--fix-cortex-a53-843419");
				elfArch = "aarch64linux";
				maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (ZipAlignmentPages);
				break;

			case "x86":
				elfArch = "elf_i386";
				maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (AndroidZipAlign.ZipAlignment32Bit);
				break;

			case "x86_64":
				elfArch = "elf_x86_64";
				maxPageSize = MonoAndroidHelper.ZipAlignmentToPageSize (ZipAlignmentPages);
				break;

			default:
				throw new NotSupportedException ($"Unsupported Android target architecture ABI: {abi}");
		}

		if (!String.IsNullOrEmpty (elfArch)) {
			extraArgs.Add ($"-m {elfArch}");
		}

		extraArgs.Add ($"-z max-page-size={maxPageSize}");

		string nativeLibsDir = MonoAndroidHelper.GetRuntimePackNativeLibDir (MonoAndroidHelper.AbiToTargetArch (abi), runtimePackLibDirs);
		extraArgs.Add ($"-L {MonoAndroidHelper.QuoteFileNameArgument (nativeLibsDir)}");
	}

	/// <summary>
	/// A helper method to create a task item that refers to a native library, for the purpose of linking
	/// it into another library.  `baseLibraryName` must be just the "stem" of the library name (e.g. `c` or `dl`)
	/// without any paths, prefixes or extensions.  The returned item will then be turned by the <see cref="Link" />
	/// method into the `-l` parameter passed to the native linker.
	/// </summary>
	public static ITaskItem MakeLibraryItem (string baseLibraryName, string abi)
	{
		ITaskItem libItem = new TaskItem (baseLibraryName);
		libItem.SetMetadata (KnownMetadata.Abi, abi);
		libItem.SetMetadata (KnownMetadata.NativeSharedLibrary, "true");
		return libItem;
	}

	public bool Link (ITaskItem outputLibraryPath, List<ITaskItem> linkItems, List<ITaskItem>? linkStartFiles = null, List<ITaskItem>? linkEndFiles = null, ICollection<ITaskItem>? exportDynamicSymbols = null)
	{
		if (UseNdkLibraries) {
			if (String.IsNullOrEmpty (NdkRootPath)) {
				throw new InvalidOperationException ("Internal error: request to use NDK libraries, but NDK root not specified.");
			}

			if (String.IsNullOrEmpty (NdkApiLevel)) {
				throw new InvalidOperationException ("Internal error: request to use NDK libraries, but NDK API level not specified.");
			}
		}

		log.LogDebugMessage ($"Linking: {outputLibraryPath}");
		EnsureCorrectAbi (outputLibraryPath);
		EnsureCorrectAbi (linkItems);
		EnsureCorrectAbi (linkStartFiles);
		EnsureCorrectAbi (linkEndFiles);

		Directory.CreateDirectory (Path.GetDirectoryName (outputLibraryPath.ItemSpec));

		string libBaseName = Path.GetFileNameWithoutExtension (outputLibraryPath.ItemSpec);
		string respFilePath = Path.Combine (intermediateDir, $"ld.{libBaseName}.{abi}.rsp");
		using var sw = new StreamWriter (File.Open (respFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding (false));
		foreach (string arg in standardArgs) {
			sw.WriteLine (arg);
		}

		if (AllowUndefinedSymbols) {
			sw.WriteLine ("--allow-shlib-undefined");
		} else {
			sw.WriteLine ("--no-undefined");
		}

		if (TargetsCLR) {
			sw.WriteLine ("--eh-frame-hdr"); // CoreCLR needs it for its exception stack unwinding
		}

		// This MUST go before extra args, since the NDK library path must take precedence over the path in extra args set in the ctor
		if (UseNdkLibraries) {
			sw.WriteLine ($"-L {MonoAndroidHelper.QuoteFileNameArgument (GetAbiNdkRootDir ())}");
		}

		foreach (string arg in extraArgs) {
			sw.WriteLine (arg);
		}

		if (StripDebugSymbols && !SaveDebugSymbols) {
			sw.WriteLine ("-s");
		}

		var excludeExportsLibs = new List<string> ();
		WriteFilesToResponseFile (sw, linkStartFiles);
		WriteFilesToResponseFile (sw, linkItems);

		if (exportDynamicSymbols != null && exportDynamicSymbols.Count > 0) {
			foreach (ITaskItem symbolItem in exportDynamicSymbols) {
				sw.WriteLine ($"--export-dynamic-symbol={symbolItem.ItemSpec}");
			}
		}

		if (excludeExportsLibs.Count > 0) {
			string libs = String.Join (",", excludeExportsLibs);
			sw.WriteLine ($"--exclude-libs={libs}");
		}

		WriteFilesToResponseFile (sw, linkEndFiles);
		sw.Flush ();

		var ldArgs = new List<string> {
			$"@{respFilePath}",
			"-o",
			MonoAndroidHelper.QuoteFileNameArgument (outputLibraryPath.ItemSpec)
		};

		var watch = new Stopwatch ();
		watch.Start ();
		bool ret = RunLinker (ldArgs, outputLibraryPath);
		watch.Stop ();
		log.LogDebugMessage ($"[{Path.GetFileName (outputLibraryPath.ItemSpec)} link time] {watch.Elapsed}");

		if (!ret || !SaveDebugSymbols) {
			return ret;
		}

		return ExtractDebugSymbols (outputLibraryPath);

		void WriteFilesToResponseFile (StreamWriter sw, List<ITaskItem>? files)
		{
			if (files == null) {
				return;
			}

			foreach (ITaskItem file in files) {
				bool wholeArchive = IncludeWholeArchive (file);

				if (ExcludeFromExports (file)) {
					excludeExportsLibs.Add (Path.GetFileName (file.ItemSpec));
				}

				if (wholeArchive) {
					sw.Write ("--whole-archive ");
				} else if (IsNativeSharedLibrary (file)) {
					sw.Write ("-l");
				}

				sw.Write (MonoAndroidHelper.QuoteFileNameArgument (file.ItemSpec));
				// string abi = file.GetMetadata ("Abi") ?? String.Empty;
				// string destDir = Path.Combine ("/tmp/t", abi);
				// Directory.CreateDirectory (destDir);
				// File.Copy (file.ItemSpec, Path.Combine (destDir, Path.GetFileName (file.ItemSpec)));
				if (wholeArchive) {
					sw.Write (" --no-whole-archive");
				}
				sw.WriteLine ();
			}
		}

		bool IncludeWholeArchive (ITaskItem item) => ParseBooleanMetadata (item, KnownMetadata.NativeLinkWholeArchive);
		bool ExcludeFromExports (ITaskItem item) => ParseBooleanMetadata (item, KnownMetadata.NativeDontExportSymbols);
		bool IsNativeSharedLibrary (ITaskItem item) => ParseBooleanMetadata (item, KnownMetadata.NativeSharedLibrary);

		bool ParseBooleanMetadata (ITaskItem item, string metadata)
		{
			string? value = item.GetMetadata (metadata);
			if (String.IsNullOrEmpty (value)) {
				return false;
			}

			// Purposefully not calling TryParse, let it throw and let us know if the value isn't a boolean.
			return Boolean.Parse (value);
		}
	}

	string GetAbiNdkRootDir ()
	{
		// Let it throw if invalid
		int apiLevel = Int32.Parse (NdkApiLevel);
		NdkTools ndk = NdkTools.Create (NdkRootPath, logErrors: true, log: log);

		return ndk.GetDirectoryPath (NdkToolchainDir.PlatformLib, MonoAndroidHelper.AbiToTargetArch (abi), apiLevel);
	}

	void EnsureCorrectAbi (ITaskItem item)
	{
		// The exception is just a precaution, since the items passed to us should have already been checked
		string itemAbi = item.GetMetadata (KnownMetadata.Abi) ?? throw new InvalidOperationException ($"Internal error: 'Abi' metadata not found in item '{item}'");
		if (String.Compare (abi, itemAbi, StringComparison.OrdinalIgnoreCase) == 0) {
			return;
		}

		throw new InvalidOperationException ($"Internal error: '{item}' ABI ('{itemAbi}') doesn't have the expected value '{abi}'");
	}

	void EnsureCorrectAbi (List<ITaskItem>? items)
	{
		if (items == null) {
			return;
		}

		foreach (ITaskItem item in items) {
			EnsureCorrectAbi (item);
		}
	}

	bool ExtractDebugSymbols (ITaskItem outputSharedLibrary)
	{
		var stdoutLines = new List<string> ();
		var stderrLines = new List<string> ();

		string sourceLib = outputSharedLibrary.ItemSpec;
		string sourceLibQuoted = MonoAndroidHelper.QuoteFileNameArgument (sourceLib);
		string destLib = Path.Combine (Path.GetDirectoryName (sourceLib), $"{Path.GetFileNameWithoutExtension (sourceLib)}.dbg.so");
		string destLibQuoted = MonoAndroidHelper.QuoteFileNameArgument (destLib);

		var args = new List<string> {
			"--only-keep-debug",
			sourceLibQuoted,
			destLibQuoted,
		};

		if (!RunCommand ("Extract Debug Info", objcopy, args, stdoutLines, stderrLines)) {
			LogFailure ();
			return false;
		}

		stdoutLines.Clear ();
		stderrLines.Clear ();

		args.Clear ();
		args.Add ("--strip-debug");
		args.Add ("--strip-unneeded");
		args.Add (sourceLibQuoted);

		if (!RunCommand ("Strip Debug Info", objcopy, args, stdoutLines, stderrLines)) {
			LogFailure ();
			return false;
		}

		stdoutLines.Clear ();
		stderrLines.Clear ();
		args.Clear ();
		args.Add ($"--add-gnu-debuglink={destLibQuoted}");
		args.Add (sourceLibQuoted);

		if (!RunCommand ("Add Debug Info Link", objcopy, args, stdoutLines, stderrLines)) {
			LogFailure ();
			return false;
		}

		return true;

		void LogFailure ()
		{
			var sb = MonoAndroidHelper.MergeStdoutAndStderrMessages (stdoutLines, stderrLines);
			// TODO: consider making it a warning
			// TODO: make it a coded message
			log.LogError ("Failed to extract debug info", Path.GetFileName (sourceLib), sb.ToString ());
		}
	}

	bool RunLinker (List<string> args, ITaskItem outputSharedLibrary)
	{
		var stdoutLines = new List<string> ();
		var stderrLines = new List<string> ();

		if (!RunCommand ("Native Linker", ld, args, stdoutLines, stderrLines)) {
			var sb = MonoAndroidHelper.MergeStdoutAndStderrMessages (stdoutLines, stderrLines);
			log.LogCodedError ("XA3007", Properties.Resources.XA3007, Path.GetFileName (outputSharedLibrary.ItemSpec), sb.ToString ());
			return false;
		}

		return true;
	}

	bool RunCommand (string label, string binaryPath, List<string> args, List<string> stdoutLines, List<string> stderrLines)
	{
		using var stdout_completed = new ManualResetEvent (false);
		using var stderr_completed = new ManualResetEvent (false);
		var psi = new ProcessStartInfo () {
			FileName = binaryPath,
			Arguments = String.Join (" ", args),
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		};

		string binaryName = Path.GetFileName (ld);
		log.LogDebugMessage ($"[{label}] {psi.FileName} {psi.Arguments}");

		using var proc = new Process ();
		proc.OutputDataReceived += (s, e) => {
			if (e.Data != null) {
				OnOutputData (binaryName, s, e);
				stdoutLines.Add (e.Data);
			} else {
				stdout_completed.Set ();
			}
		};

		proc.ErrorDataReceived += (s, e) => {
			if (e.Data != null) {
				OnErrorData (binaryName, s, e);
				stderrLines.Add (e.Data);
			} else {
				stderr_completed.Set ();
			}
		};

		proc.StartInfo = psi;
		proc.Start ();
		proc.BeginOutputReadLine ();
		proc.BeginErrorReadLine ();
		cancellationToken?.Register (() => { try { proc.Kill (); } catch (Exception) { } });
		proc.WaitForExit ();

		if (psi.RedirectStandardError) {
			stderr_completed.WaitOne (TimeSpan.FromSeconds (30));
		}

		if (psi.RedirectStandardOutput) {
			stdout_completed.WaitOne (TimeSpan.FromSeconds (30));
		}

		log.LogDebugMessage ($"[{label}] exit code == {proc.ExitCode}");
		if (proc.ExitCode != 0) {
			cancelTask?.Invoke ();
			return false;
		}

		return true;
	}

	void OnOutputData (string linkerName, object sender, DataReceivedEventArgs e)
	{
		if (e.Data != null) {
			log.LogMessage ($"[{linkerName} stdout] {e.Data}");
		}
	}

	void OnErrorData (string linkerName, object sender, DataReceivedEventArgs e)
	{
		if (e.Data != null) {
			log.LogMessage ($"[{linkerName} stderr] {e.Data}");
		}
	}
}
