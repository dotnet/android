using System.Diagnostics;
using System.Text;
using Microsoft.Testing.Extensions;
using Mono.Options;
using Xamarin.Android.Tools;

const string Name = "Microsoft.Android.Run";
const string VersionsFileName = "Microsoft.Android.versions.txt";
const int CtrlCExitCode = 130; // Standard Unix exit code for SIGINT: 128 + signal 2.
const int StopAppTimeoutSeconds = 10;

string? adbPath = null;
string? adbTarget = null;
string? package = null;
string? activity = null;
string? deviceUserId = null;
string? instrumentation = null;
bool verbose = false;
bool wakeDevice = true;
int? logcatPid = null;
Process? logcatProcess = null;
CancellationTokenSource cts = new ();
int ctrlCRequested = 0;
string? logcatArgs = null;
bool isDotnetTestMode = false;
string? dotnetTestPipe = null;
bool waitForExit = true;
List<PortMapping> forwardPorts = [];
List<PortMapping> reversePorts = [];

try {
	return await RunAsync (args);
} catch (OperationCanceledException) {
	return CtrlCExitCode;
} catch (Exception ex) {
	Console.Error.WriteLine ($"Error: {ex.Message}");
	if (verbose)
		Console.Error.WriteLine (ex.ToString ());
	return 1;
}

async Task<int> RunAsync (string[] args)
{
	bool showHelp = false;
	bool showVersion = false;

	var options = new OptionSet {
		$"Usage: {Name} [OPTIONS]",
		"",
		"Launches an Android application, streams its logcat output, and provides",
		"proper Ctrl+C handling to stop the app gracefully.",
		"Options:",
		{ "a|adb=",
			"Path to the {ADB} executable. If not specified, will attempt to locate " +
			"the Android SDK automatically.",
			v => adbPath = v },
		{ "adb-target=",
			"The {TARGET} device/emulator for adb commands (e.g., '-s emulator-5554').",
			v => adbTarget = v },
		{ "p|package=",
			"The Android application {PACKAGE} name (e.g., com.example.myapp). Required.",
			v => package = v },
		{ "c|activity=",
			"The {ACTIVITY} class name to launch. Required unless --instrument is used.",
			v => activity = v },
		{ "user=",
			"The Android device {USER_ID} to launch the activity under (e.g., 10 for a work profile).",
			v => deviceUserId = v },
		{ "i|instrument=",
			"The instrumentation {RUNNER} class name (e.g., com.example.myapp.TestInstrumentation). " +
			"When specified, runs 'am instrument' instead of 'am start'.",
			v => instrumentation = v },
		{ "server=",
			"The test {SERVER} protocol to use (e.g., 'dotnettestcli'). Used by 'dotnet test'.",
			v => { if (v == "dotnettestcli") isDotnetTestMode = true; } },
		{ "dotnet-test-pipe=",
			"The {PIPE} name for dotnet test communication. Used by 'dotnet test'.",
			v => dotnetTestPipe = v },
		{ "v|verbose",
			"Enable verbose output for debugging.",
			v => verbose = v != null },
		{ "no-wake-device",
			"Do not wake the device or dismiss its keyguard before launching the application.",
			v => wakeDevice = v == null },
		{ "logcat-args=",
			"Extra {ARGUMENTS} to pass to 'adb logcat' (e.g., 'monodroid-assembly:S' to silence a tag).",
			v => logcatArgs = v },
		{ "no-wait",
			"Launch the application without waiting for it to exit or streaming logcat.",
			v => waitForExit = v == null },
		{ "forward-port=",
			"Forward a TCP port from the host to the device in {MAPPING} format (HOST_PORT:DEVICE_PORT). May be repeated.",
			v => forwardPorts.Add (ParsePortMapping (v, "--forward-port")) },
		{ "reverse-port=",
			"Reverse a TCP port from the device to the host in {MAPPING} format (DEVICE_PORT:HOST_PORT). May be repeated.",
			v => reversePorts.Add (ParsePortMapping (v, "--reverse-port")) },
		{ "version",
			"Show version information and exit.",
			v => showVersion = v != null },
		{ "h|help|?",
			"Show this help message and exit.",
			v => showHelp = v != null },
	};

	List<string> remaining;
	try {
		remaining = options.Parse (args);
	} catch (OptionException e) {
		Console.Error.WriteLine ($"Error: {e.Message}");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	if (remaining.Count > 0 && !isDotnetTestMode && string.IsNullOrEmpty (instrumentation)) {
		Console.Error.WriteLine ($"Error: Unexpected argument(s): {string.Join (" ", remaining)}");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	if (showVersion) {
		var (version, commit) = GetVersionInfo ();
		if (!string.IsNullOrEmpty (version)) {
			Console.WriteLine ($"{Name} {version}");
			if (!string.IsNullOrEmpty (commit))
				Console.WriteLine ($"Commit: {commit}");
		} else {
			Console.WriteLine (Name);
		}
		return 0;
	}

	if (showHelp) {
		options.WriteOptionDescriptions (Console.Out);
		Console.WriteLine ();
		Console.WriteLine ("Examples:");
		Console.WriteLine ($"  {Name} -p com.example.myapp -c com.example.myapp.MainActivity");
		Console.WriteLine ($"  {Name} -p com.example.myapp -i com.example.myapp.TestInstrumentation");
		Console.WriteLine ($"  {Name} -p com.example.myapp -i com.example.myapp.Benchmarks --filter *MyBench*");
		Console.WriteLine ($"  {Name} --adb /path/to/adb -p com.example.myapp -c com.example.myapp.MainActivity");
		Console.WriteLine ();
		Console.WriteLine ("When --instrument is used, any unrecognized arguments are forwarded to");
		Console.WriteLine ("'am instrument' as extras: KEY=VALUE becomes '-e KEY VALUE', and everything");
		Console.WriteLine ("else is joined into a single '-e args \"...\"' extra.");
		Console.WriteLine ();
		Console.WriteLine ("Press Ctrl+C while running to stop the Android application and exit.");
		return 0;
	}

	if (string.IsNullOrEmpty (package)) {
		Console.Error.WriteLine ("Error: --package is required.");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	bool isInstrumentMode = !string.IsNullOrEmpty (instrumentation);

	if (!isInstrumentMode && string.IsNullOrEmpty (activity) && !isDotnetTestMode) {
		Console.Error.WriteLine ("Error: --activity or --instrument is required.");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	if (isDotnetTestMode && !isInstrumentMode) {
		Console.Error.WriteLine ("Error: --instrument is required when using dotnet test mode.");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	if (isInstrumentMode && !string.IsNullOrEmpty (activity)) {
		Console.Error.WriteLine ("Error: --activity and --instrument cannot be used together.");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	// Resolve adb path if not specified
	if (string.IsNullOrEmpty (adbPath)) {
		adbPath = FindAdbPath ();
		if (string.IsNullOrEmpty (adbPath)) {
			Console.Error.WriteLine ("Error: Could not locate adb. Please specify --adb.");
			return 1;
		}
	}

	if (!File.Exists (adbPath)) {
		Console.Error.WriteLine ($"Error: adb not found at '{adbPath}'.");
		return 1;
	}

	Debug.Assert (adbPath != null, "adbPath should be non-null after validation");

	if (verbose) {
		Console.WriteLine ($"Using adb: {adbPath}");
		if (!string.IsNullOrEmpty (adbTarget))
			Console.WriteLine ($"Target: {adbTarget}");
		Console.WriteLine ($"Package: {package}");
		if (!string.IsNullOrEmpty (activity))
			Console.WriteLine ($"Activity: {activity}");
		if (isInstrumentMode)
			Console.WriteLine ($"Instrumentation runner: {instrumentation}");
		if (isDotnetTestMode)
			Console.WriteLine ($"dotnet test mode (pipe: {dotnetTestPipe})");
	}

	if (!await ConfigurePortMappingsAsync ())
		return 1;

	// Set up Ctrl+C handler
	Console.CancelKeyPress += OnCancelKeyPress;

	int exitCode;
	bool cancellationRequested;
	try {
		if (isDotnetTestMode)
			exitCode = await RunDotnetTestAsync (remaining);
		else if (isInstrumentMode)
			exitCode = await RunInstrumentationAsync (remaining);
		else
			exitCode = await RunAppAsync ();
	} finally {
		Console.CancelKeyPress -= OnCancelKeyPress;
		cancellationRequested = Volatile.Read (ref ctrlCRequested) != 0;
		if (cancellationRequested)
			await StopAppAsync ();
		cts.Dispose ();
	}

	return cancellationRequested ? CtrlCExitCode : exitCode;
}

void OnCancelKeyPress (object? sender, ConsoleCancelEventArgs e)
{
	e.Cancel = true; // Prevent immediate exit
	Console.WriteLine ();
	Console.WriteLine ("Stopping application...");

	Interlocked.Exchange (ref ctrlCRequested, 1);
	cts.Cancel ();
}

async Task<int> RunInstrumentationAsync (List<string> instrumentationArgs)
{
	// '-w' waits for the run to complete; '-r' prints raw INSTRUMENTATION_STATUS
	// blocks as they arrive instead of buffering everything until the end.
	var cmdArgs = new List<string> { "shell", "am", "instrument" };
	if (waitForExit)
		cmdArgs.Add ("-w");
	cmdArgs.Add ("-r");
	if (!string.IsNullOrEmpty (deviceUserId)) {
		cmdArgs.Add ("--user");
		cmdArgs.Add (deviceUserId);
	}
	cmdArgs.AddRange (BuildInstrumentationExtras (instrumentationArgs));
	cmdArgs.Add ($"{package}/{instrumentation}");

	if (verbose)
		Console.WriteLine ($"Running instrumentation: adb {string.Join (" ", cmdArgs)}");

	// Run instrumentation with streaming output
	var psi = AdbHelper.CreateStartInfo (adbPath, adbTarget, cmdArgs);
	using var instrumentProcess = new Process { StartInfo = psi };

	var locker = new Lock ();
	var output = new StringBuilder ();

	instrumentProcess.OutputDataReceived += (s, e) => {
		if (e.Data != null)
			lock (locker) {
				output.AppendLine (e.Data);
				Console.WriteLine (e.Data);
			}
	};

	instrumentProcess.ErrorDataReceived += (s, e) => {
		if (e.Data != null)
			lock (locker) {
				output.AppendLine (e.Data);
				Console.Error.WriteLine (e.Data);
			}
	};

	instrumentProcess.Start ();
	instrumentProcess.BeginOutputReadLine ();
	instrumentProcess.BeginErrorReadLine ();
	if (!waitForExit) {
		await instrumentProcess.WaitForExitAsync (cts.Token);
		return instrumentProcess.ExitCode == 0 ? 0 : 1;
	}

	// Also stream logcat in the background, which is where Console output from the
	// app ends up. The app process does not exist yet when `am instrument` starts,
	// so poll for it rather than giving up after a single `pidof`.
	var logcatTask = StartLogcatWhenAppStartsAsync ();

	// Wait for instrumentation to complete or Ctrl+C
	try {
		try {
			await instrumentProcess.WaitForExitAsync (cts.Token);
		} catch (OperationCanceledException) {
			try { instrumentProcess.Kill (); } catch (Exception ex) {
				if (verbose)
					Console.Error.WriteLine ($"Cleanup: {ex.Message}");
			}
			return 1;
		}
	} finally {
		cts.Cancel ();
		await logcatTask;
		// Clean up logcat
		try {
			if (logcatProcess != null && !logcatProcess.HasExited) {
				logcatProcess.Kill ();
				logcatProcess.WaitForExit (1000);
			}
		} catch (Exception ex) {
			if (verbose)
				Console.Error.WriteLine ($"Logcat cleanup: {ex.Message}");
		}
	}

	// Check exit status
	if (instrumentProcess.ExitCode != 0) {
		Console.Error.WriteLine ($"Error: adb instrument exited with code {instrumentProcess.ExitCode}");
		return 1;
	}

	// `am instrument` exits 0 even when the instrumentation crashes or reports
	// failure, so inspect what it printed to decide the exit code. `WaitForExitAsync`
	// has already drained both readers, but read under the same lock for clarity.
	string capturedOutput;
	lock (locker)
		capturedOutput = output.ToString ();

	var failure = GetInstrumentationFailure (capturedOutput);
	if (failure != null) {
		Console.Error.WriteLine ($"Error: {failure}");
		return 1;
	}

	return 0;
}

/// <summary>
/// Translates trailing `dotnet run -- ARGS` into `am instrument` extras.
/// `KEY=VALUE` arguments become `-e KEY VALUE`; everything else is joined and
/// passed as a single `-e args "..."` extra.
/// </summary>
List<string> BuildInstrumentationExtras (List<string> instrumentationArgs)
{
	var result = new List<string> ();
	if (instrumentationArgs.Count == 0)
		return result;

	var positional = new List<string> ();

	foreach (var arg in instrumentationArgs) {
		var eqIndex = arg.IndexOf ('=');
		if (eqIndex > 0 && !arg.StartsWith ("-", StringComparison.Ordinal) && IsBundleKey (arg.AsSpan (0, eqIndex))) {
			result.Add ("-e");
			result.Add (arg.Substring (0, eqIndex));
			result.Add (QuoteForDeviceShell (arg.Substring (eqIndex + 1)));
		} else {
			positional.Add (arg);
		}
	}

	if (positional.Count > 0) {
		result.Add ("-e");
		result.Add ("args");
		result.Add (QuoteForDeviceShell (string.Join (" ", positional)));
	}

	return result;

	static bool IsBundleKey (ReadOnlySpan<char> key)
	{
		foreach (var c in key) {
			if (!char.IsLetterOrDigit (c) && c != '_' && c != '.')
				return false;
		}
		return key.Length > 0;
	}
}

/// <summary>
/// Wraps a value in single quotes so the shell on the device treats it as a
/// single token. `adb shell` deliberately does not escape the arguments it
/// forwards, it just joins them with spaces (like `ssh`), so quoting for the
/// device shell is up to the caller. The surrounding quoting needed to survive
/// the *local* command line is handled by <see cref="ProcessStartInfo.ArgumentList"/>.
/// </summary>
static string QuoteForDeviceShell (string value) =>
	"'" + value.Replace ("'", "'\\''") + "'";

/// <summary>
/// Inspects `am instrument` output for signs that the instrumentation crashed or
/// reported failure. Returns a human readable reason, or <c>null</c> on success.
/// </summary>
static string? GetInstrumentationFailure (string output)
{
	if (output.Contains ("INSTRUMENTATION_FAILED", StringComparison.Ordinal))
		return "The instrumentation failed to start. See the output above for details.";

	string? shortMsg = null, longMsg = null;
	int? code = null;
	foreach (var rawLine in output.Split ('\n')) {
		var line = rawLine.TrimEnd ('\r');
		if (line.StartsWith ("INSTRUMENTATION_RESULT: shortMsg=", StringComparison.Ordinal))
			shortMsg = line.Substring ("INSTRUMENTATION_RESULT: shortMsg=".Length).Trim ();
		else if (line.StartsWith ("INSTRUMENTATION_RESULT: longMsg=", StringComparison.Ordinal))
			longMsg = line.Substring ("INSTRUMENTATION_RESULT: longMsg=".Length).Trim ();
		else if (line.StartsWith ("INSTRUMENTATION_CODE: ", StringComparison.Ordinal)) {
			if (int.TryParse (line.Substring ("INSTRUMENTATION_CODE: ".Length).Trim (), out int parsed))
				code = parsed;
		}
	}

	if (longMsg != null || shortMsg != null)
		return $"The application crashed: {longMsg ?? shortMsg}";

	// Activity.RESULT_CANCELED (0) is what Instrumentation.Finish() reports on failure.
	if (code == 0)
		return "The instrumentation reported failure (INSTRUMENTATION_CODE: 0).";

	if (code == null)
		return "The instrumentation did not complete. It may have crashed before calling Finish().";

	return null;
}

/// <summary>
/// Polls for the application process and starts streaming logcat once it appears.
/// </summary>
async Task StartLogcatWhenAppStartsAsync ()
{
	try {
		while (!cts.Token.IsCancellationRequested) {
			var pid = await GetAppPidAsync ();
			if (pid != null) {
				logcatPid = pid;
				StartLogcat ();
				return;
			}
			await Task.Delay (250, cts.Token).ConfigureAwait (ConfigureAwaitOptions.SuppressThrowing);
		}
	} catch (OperationCanceledException) {
		// The instrumentation finished (or was cancelled) before the app process was seen
	} catch (Exception ex) {
		if (verbose)
			Console.Error.WriteLine ($"Error starting logcat: {ex.Message}");
	}
}

async Task<int> RunDotnetTestAsync (List<string> mtpArgs)
{
	if (verbose)
		Console.WriteLine ("Running in dotnet test mode...");

	if (string.IsNullOrEmpty (adbPath)) {
		Console.Error.WriteLine ("Error: adb path must be specified in dotnet test mode.");
		return 1;
	}

	if (string.IsNullOrEmpty (instrumentation)) {
		Console.Error.WriteLine ("Error: Instrumentation must be specified in dotnet test mode.");
		return 1;
	}

	if (string.IsNullOrEmpty (dotnetTestPipe)) {
		Console.Error.WriteLine ("Error: --dotnet-test-pipe must be specified when using --server dotnettestcli.");
		return 1;
	}

	if (string.IsNullOrEmpty (package)) {
		Console.Error.WriteLine ("Error: Package must be specified in dotnet test mode.");
		return 1;
	}

	var validatedAdbPath = adbPath;
	var validatedInstrumentation = instrumentation;
	var validatedDotnetTestPipe = dotnetTestPipe;
	var validatedPackage = package;

	// Re-add the MTP protocol args that Mono.Options consumed,
	// since MTP needs them to set up the test communication channel.
	mtpArgs.AddRange (["--server", "dotnettestcli", "--dotnet-test-pipe", validatedDotnetTestPipe]);

	// MTP defaults its working directory to the DLL location (SDK tools directory),
	// not Environment.CurrentDirectory. Pass --results-directory explicitly so TRX
	// reports are written to the project directory, matching dotnet test conventions.
	if (!mtpArgs.Contains ("--results-directory")) {
		mtpArgs.AddRange (["--results-directory", Path.Combine (Environment.CurrentDirectory, "TestResults")]);
	}

	var testApplicationBuilder = await Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync (mtpArgs.ToArray ());

	var adapter = new AndroidTestAdapter (
		validatedAdbPath,
		adbTarget,
		validatedPackage,
		validatedInstrumentation,
		verbose);

	testApplicationBuilder.RegisterTestFramework (
		_ => new AndroidTestCapabilities (),
		(_, _) => adapter);

	testApplicationBuilder.AddTrxReportProvider ();

	using var testApplication = await testApplicationBuilder.BuildAsync ();
	return await testApplication.RunAsync ();
}

async Task<int> RunAppAsync ()
{
	// 1. Start the app
	if (!await StartAppAsync ())
		return 1;
	if (!waitForExit)
		return 0;

	// 2. Get the PID
	logcatPid = await GetAppPidAsync ();
	if (logcatPid == null) {
		Console.Error.WriteLine ("Error: App started but could not retrieve PID. The app may have crashed.");
		return 1;
	}

	if (verbose)
		Console.WriteLine ($"App PID: {logcatPid}");

	// 3. Stream logcat
	StartLogcat ();

	// 4. Wait for app to exit or Ctrl+C
	await WaitForAppExitAsync ();

	return 0;
}

async Task<bool> StartAppAsync ()
{
	var userArg = string.IsNullOrEmpty (deviceUserId) ? "" : $" --user {deviceUserId}";
	// Device preparation is best effort; am start must run and determine the shell exit code.
	var wakeDeviceCommand = wakeDevice ? "input keyevent KEYCODE_WAKEUP; wm dismiss-keyguard; " : "";
	var waitArg = waitForExit ? " -W" : "";
	var cmdArgs = $"shell {wakeDeviceCommand}am start -S{waitArg}{userArg} -n \"{package}/{activity}\"";
	var (exitCode, output, error) = await AdbHelper.RunAsync (adbPath, adbTarget, cmdArgs, cts.Token, verbose);
	if (exitCode != 0) {
		Console.Error.WriteLine ($"Error: Failed to start app: {error}");
		return false;
	}

	if (verbose)
		Console.WriteLine (output);

	return true;
}

async Task<bool> ConfigurePortMappingsAsync ()
{
	foreach (var port in forwardPorts) {
		var (forwardExitCode, _, forwardError) = await AdbHelper.RunAsync (
			adbPath, adbTarget, $"forward tcp:{port.Source} tcp:{port.Destination}", cts.Token, verbose);
		if (forwardExitCode != 0) {
			Console.Error.WriteLine ($"Error: Failed to forward port {port.Source}:{port.Destination}: {forwardError}");
			return false;
		}
	}

	foreach (var port in reversePorts) {
		var (reverseExitCode, _, reverseError) = await AdbHelper.RunAsync (
			adbPath, adbTarget, $"reverse tcp:{port.Source} tcp:{port.Destination}", cts.Token, verbose);
		if (reverseExitCode != 0) {
			Console.Error.WriteLine ($"Error: Failed to reverse port {port.Source}:{port.Destination}: {reverseError}");
			return false;
		}
	}

	return true;
}

PortMapping ParsePortMapping (string value, string option)
{
	var ports = value.Split (':');
	if (ports.Length != 2 ||
			!int.TryParse (ports [0], out int source) ||
			!int.TryParse (ports [1], out int destination) ||
			source is < 1 or > 65535 ||
			destination is < 1 or > 65535) {
		throw new OptionException ("Expected two TCP ports between 1 and 65535 in SOURCE:DESTINATION format.", option);
	}

	return new PortMapping (source, destination);
}

async Task<int?> GetAppPidAsync ()
{
	var cmdArgs = $"shell pidof {package}";
	var (exitCode, output, error) = await AdbHelper.RunAsync (adbPath, adbTarget, cmdArgs, cts.Token, verbose);
	if (exitCode != 0 || string.IsNullOrWhiteSpace (output))
		return null;

	var pidStr = output.Trim ().Split (' ') [0]; // Take first PID if multiple
	if (int.TryParse (pidStr, out int pid))
		return pid;

	return null;
}

void StartLogcat ()
{
	if (logcatPid == null)
		return;

	var logcatArguments = $"logcat --pid={logcatPid}";
	if (!string.IsNullOrEmpty (logcatArgs))
		logcatArguments += $" {logcatArgs}";

	var psi = AdbHelper.CreateStartInfo (adbPath, adbTarget, logcatArguments);

	if (verbose)
		Console.WriteLine ($"Running: adb {psi.Arguments}");

	var locker = new Lock();

	logcatProcess = new Process { StartInfo = psi };

	logcatProcess.OutputDataReceived += (s, e) => {
		if (e.Data != null)
			lock (locker)
				Console.WriteLine (e.Data);
	};

	logcatProcess.ErrorDataReceived += (s, e) => {
		if (e.Data != null)
			lock (locker)
				Console.Error.WriteLine (e.Data);
	};

	logcatProcess.Start ();
	logcatProcess.BeginOutputReadLine ();
	logcatProcess.BeginErrorReadLine ();
}

async Task WaitForAppExitAsync ()
{
	try {
		while (!cts.Token.IsCancellationRequested) {
			// Check if app is still running
			var pid = await GetAppPidAsync ();
			if (pid == null || pid != logcatPid) {
				if (verbose)
					Console.WriteLine ("App has exited.");
				break;
			}

			// Also check if logcat process exited unexpectedly
			if (logcatProcess != null && logcatProcess.HasExited) {
				if (verbose)
					Console.WriteLine ("Logcat process exited.");
				break;
			}

			await Task.Delay (1000, cts.Token).ConfigureAwait (ConfigureAwaitOptions.SuppressThrowing);
		}
	} finally {
		// Clean up logcat process
		try {
			if (logcatProcess != null && !logcatProcess.HasExited) {
				logcatProcess.Kill ();
				logcatProcess.WaitForExit (1000);
			}
		} catch (Exception ex) {
			if (verbose)
				Console.Error.WriteLine ($"Error cleaning up logcat process: {ex.Message}");
		}
	}
}

async Task StopAppAsync ()
{
	if (string.IsNullOrEmpty (package) || string.IsNullOrEmpty (adbPath))
		return;

	var userArg = string.IsNullOrEmpty (deviceUserId) ? "" : $" --user {deviceUserId}";
	using var timeoutCts = new CancellationTokenSource (TimeSpan.FromSeconds (StopAppTimeoutSeconds));
	try {
		var (exitCode, _, error) = await AdbHelper.RunAsync (adbPath, adbTarget, $"shell am force-stop{userArg} {package}", timeoutCts.Token, verbose);
		if (exitCode != 0)
			Console.Error.WriteLine ($"Error: Failed to stop app: {error}");
	} catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested) {
		Console.Error.WriteLine ($"Error: Timed out stopping app after {StopAppTimeoutSeconds} seconds.");
	} catch (Exception ex) {
		Console.Error.WriteLine ($"Error: Failed to stop app: {ex.Message}");
		if (verbose)
			Console.Error.WriteLine (ex.ToString ());
	}
}

string? FindAdbPath ()
{
	try {
		// Use AndroidSdkInfo to locate the SDK
		var sdk = new AndroidSdkInfo (
			logger: verbose ? (level, msg) => Console.WriteLine ($"[{level}] {msg}") : null
		);

		if (!string.IsNullOrEmpty (sdk.AndroidSdkPath)) {
			var adb = Path.Combine (sdk.AndroidSdkPath, "platform-tools", OperatingSystem.IsWindows () ? "adb.exe" : "adb");
			if (File.Exists (adb))
				return adb;
		}
	} catch (Exception ex) {
		if (verbose)
			Console.WriteLine ($"AndroidSdkInfo failed: {ex.Message}");
	}

	return null;
}

(string? Version, string? Commit) GetVersionInfo ()
{
	try {
		// The tool is in: <sdk>/tools/Microsoft.Android.Run.dll
		// The versions file is in: <sdk>/Microsoft.Android.versions.txt
		var toolPath = typeof (OptionSet).Assembly.Location;
		if (string.IsNullOrEmpty (toolPath))
			toolPath = Environment.ProcessPath;

		if (string.IsNullOrEmpty (toolPath))
			return (null, null);

		var toolDir = Path.GetDirectoryName (toolPath);
		if (string.IsNullOrEmpty (toolDir))
			return (null, null);

		var sdkDir = Path.GetDirectoryName (toolDir);
		if (string.IsNullOrEmpty (sdkDir))
			return (null, null);

		var versionsFile = Path.Combine (sdkDir, VersionsFileName);
		if (!File.Exists (versionsFile))
			return (null, null);

		var lines = File.ReadAllLines (versionsFile);
		string? commit = lines.Length > 0 ? lines [0].Trim () : null;
		string? version = lines.Length > 1 ? lines [1].Trim () : null;

		return (version, commit);
	} catch (Exception ex) {
		if (verbose)
			Console.Error.WriteLine ($"Error reading version info: {ex.Message}");
		return (null, null);
	}
}

readonly record struct PortMapping (int Source, int Destination);
