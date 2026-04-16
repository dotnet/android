using System.Diagnostics;
using Mono.Options;
using Xamarin.Android.Tools;

const string Name = "Microsoft.Android.Run";
const string VersionsFileName = "Microsoft.Android.versions.txt";

string? adbPath = null;
string? adbTarget = null;
string? package = null;
string? activity = null;
string? deviceUserId = null;
string? instrumentation = null;
bool verbose = false;
int? logcatPid = null;
Process? logcatProcess = null;
CancellationTokenSource cts = new ();
string? logcatArgs = null;
bool isDotnetTestMode = false;
string? dotnetTestPipe = null;

try {
	return await RunAsync (args);
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
		{ "logcat-args=",
			"Extra {ARGUMENTS} to pass to 'adb logcat' (e.g., 'monodroid-assembly:S' to silence a tag).",
			v => logcatArgs = v },
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

	if (remaining.Count > 0 && !isDotnetTestMode) {
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
		Console.WriteLine ($"  {Name} --adb /path/to/adb -p com.example.myapp -c com.example.myapp.MainActivity");
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

	// Set up Ctrl+C handler
	Console.CancelKeyPress += OnCancelKeyPress;

	try {
		if (isDotnetTestMode)
			return await RunDotnetTestAsync (remaining);

		if (isInstrumentMode)
			return await RunInstrumentationAsync ();

		return await RunAppAsync ();
	} finally {
		Console.CancelKeyPress -= OnCancelKeyPress;
		cts.Dispose ();
	}
}

void OnCancelKeyPress (object? sender, ConsoleCancelEventArgs e)
{
	e.Cancel = true; // Prevent immediate exit
	Console.WriteLine ();
	Console.WriteLine ("Stopping application...");

	cts.Cancel ();

	// Force-stop the app (fire-and-forget in cancel handler)
	_ = StopAppAsync ();

	// Kill logcat process if running
	try {
		if (logcatProcess != null && !logcatProcess.HasExited) {
			logcatProcess.Kill ();
		}
	} catch (Exception ex) {
		if (verbose)
			Console.Error.WriteLine ($"Error killing logcat process: {ex.Message}");
	}
}

async Task<int> RunInstrumentationAsync ()
{
	// Build the am instrument command
	var userArg = string.IsNullOrEmpty (deviceUserId) ? "" : $" --user {deviceUserId}";
	var cmdArgs = $"shell am instrument -w{userArg} {package}/{instrumentation}";

	if (verbose)
		Console.WriteLine ($"Running instrumentation: adb {cmdArgs}");

	// Run instrumentation with streaming output
	var psi = AdbHelper.CreateStartInfo (adbPath, adbTarget, cmdArgs);
	using var instrumentProcess = new Process { StartInfo = psi };

	var locker = new Lock ();

	instrumentProcess.OutputDataReceived += (s, e) => {
		if (e.Data != null)
			lock (locker)
				Console.WriteLine (e.Data);
	};

	instrumentProcess.ErrorDataReceived += (s, e) => {
		if (e.Data != null)
			lock (locker)
				Console.Error.WriteLine (e.Data);
	};

	instrumentProcess.Start ();
	instrumentProcess.BeginOutputReadLine ();
	instrumentProcess.BeginErrorReadLine ();

	// Also start logcat in the background for additional debug output
	logcatPid = await GetAppPidAsync ();
	if (logcatPid != null)
		StartLogcat ();

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

	return 0;
}

async Task<int> RunDotnetTestAsync (List<string> mtpArgs)
{
	if (verbose)
		Console.WriteLine ("Running in dotnet test mode...");

	Debug.Assert (adbPath != null, "adbPath should be non-null after validation");
	Debug.Assert (instrumentation != null, "Instrumentation must be specified in dotnet test mode.");

	// Re-add the MTP protocol args that Mono.Options consumed,
	// since MTP needs them to set up the test communication channel.
	mtpArgs.AddRange (["--server", "dotnettestcli", "--dotnet-test-pipe", dotnetTestPipe!]);

	var testApplicationBuilder = await Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync (mtpArgs.ToArray ());

	var adapter = new AndroidTestAdapter (
		adbPath,
		adbTarget,
		package!,
		instrumentation,
		verbose);

	testApplicationBuilder.RegisterTestFramework (
		_ => new AndroidTestCapabilities (),
		(_, _) => adapter);

	using var testApplication = await testApplicationBuilder.BuildAsync ();
	return await testApplication.RunAsync ();
}

async Task<int> RunAppAsync ()
{
	// 1. Start the app
	if (!await StartAppAsync ())
		return 1;

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
	var cmdArgs = $"shell am start -S -W{userArg} -n \"{package}/{activity}\"";
	var (exitCode, output, error) = await AdbHelper.RunAsync (adbPath, adbTarget, cmdArgs, cts.Token, verbose);
	if (exitCode != 0) {
		Console.Error.WriteLine ($"Error: Failed to start app: {error}");
		return false;
	}

	if (verbose)
		Console.WriteLine (output);

	return true;
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
	while (!cts!.Token.IsCancellationRequested) {
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

async Task StopAppAsync ()
{
	if (string.IsNullOrEmpty (package) || string.IsNullOrEmpty (adbPath))
		return;

	var userArg = string.IsNullOrEmpty (deviceUserId) ? "" : $" --user {deviceUserId}";
	await AdbHelper.RunAsync (adbPath, adbTarget, $"shell am force-stop{userArg} {package}", CancellationToken.None, verbose);
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
