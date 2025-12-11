using System.Diagnostics;
using Mono.Options;
using Xamarin.Android.Tools;

const string Name = "Microsoft.Android.Run";
const string VersionsFileName = "Microsoft.Android.versions.txt";

string? adbPath = null;
string? package = null;
string? activity = null;
bool verbose = false;
int? logcatPid = null;
Process? logcatProcess = null;
CancellationTokenSource cts = new ();
string? logcatArgs = null;

try {
	return Run (args);
} catch (Exception ex) {
	Console.Error.WriteLine ($"Error: {ex.Message}");
	if (verbose)
		Console.Error.WriteLine (ex.ToString ());
	return 1;
}

int Run (string[] args)
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
		{ "p|package=",
			"The Android application {PACKAGE} name (e.g., com.example.myapp). Required.",
			v => package = v },
		{ "c|activity=",
			"The {ACTIVITY} class name to launch. Required.",
			v => activity = v },
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

	try {
		var remaining = options.Parse (args);
		if (remaining.Count > 0) {
			Console.Error.WriteLine ($"Error: Unexpected argument(s): {string.Join (" ", remaining)}");
			Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
			return 1;
		}
	} catch (OptionException e) {
		Console.Error.WriteLine ($"Error: {e.Message}");
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
		Console.WriteLine ($"  {Name} -p com.example.myapp");
		Console.WriteLine ($"  {Name} -p com.example.myapp -c com.example.myapp.MainActivity");
		Console.WriteLine ($"  {Name} --adb /path/to/adb -p com.example.myapp");
		Console.WriteLine ();
		Console.WriteLine ("Press Ctrl+C while running to stop the Android application and exit.");
		return 0;
	}

	if (string.IsNullOrEmpty (package)) {
		Console.Error.WriteLine ("Error: --package is required.");
		Console.Error.WriteLine ($"Try '{Name} --help' for more information.");
		return 1;
	}

	if (string.IsNullOrEmpty (activity)) {
		Console.Error.WriteLine ("Error: --activity is required.");
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

	if (verbose) {
		Console.WriteLine ($"Using adb: {adbPath}");
		Console.WriteLine ($"Package: {package}");
		if (!string.IsNullOrEmpty (activity))
			Console.WriteLine ($"Activity: {activity}");
	}

	// Set up Ctrl+C handler
	Console.CancelKeyPress += OnCancelKeyPress;

	try {
		return RunApp ();
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

	// Force-stop the app
	StopApp ();

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

int RunApp ()
{
	// 1. Start the app
	if (!StartApp ())
		return 1;

	// 2. Get the PID
	logcatPid = GetAppPid ();
	if (logcatPid == null) {
		Console.Error.WriteLine ("Error: App started but could not retrieve PID. The app may have crashed.");
		return 1;
	}

	if (verbose)
		Console.WriteLine ($"App PID: {logcatPid}");

	// 3. Stream logcat
	StartLogcat ();

	// 4. Wait for app to exit or Ctrl+C
	WaitForAppExit ();

	return 0;
}

bool StartApp ()
{
	var cmdArgs = $"shell am start -S -W -n \"{package}/{activity}\"";
	var (exitCode, output, error) = RunAdb (cmdArgs);
	if (exitCode != 0) {
		Console.Error.WriteLine ($"Error: Failed to start app: {error}");
		return false;
	}

	if (verbose)
		Console.WriteLine (output);

	return true;
}

int? GetAppPid ()
{
	var cmdArgs = $"shell pidof {package}";
	var (exitCode, output, error) = RunAdb (cmdArgs);
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

	if (verbose)
		Console.WriteLine ($"Running: adb {logcatArguments}");

	var psi = new ProcessStartInfo {
		FileName = adbPath,
		Arguments = logcatArguments,
		UseShellExecute = false,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		CreateNoWindow = true,
	};

	logcatProcess = new Process { StartInfo = psi };

	logcatProcess.OutputDataReceived += (s, e) => {
		if (e.Data != null)
			Console.WriteLine (e.Data);
	};

	logcatProcess.ErrorDataReceived += (s, e) => {
		if (e.Data != null)
			Console.Error.WriteLine (e.Data);
	};

	logcatProcess.Start ();
	logcatProcess.BeginOutputReadLine ();
	logcatProcess.BeginErrorReadLine ();
}

void WaitForAppExit ()
{
	while (!cts!.Token.IsCancellationRequested) {
		// Check if app is still running
		var pid = GetAppPid ();
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

		Thread.Sleep (1000);
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

void StopApp ()
{
	if (string.IsNullOrEmpty (package) || string.IsNullOrEmpty (adbPath))
		return;

	RunAdb ($"shell am force-stop {package}");
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

(int ExitCode, string Output, string Error) RunAdb (string arguments)
{
	if (verbose)
		Console.WriteLine ($"Running: adb {arguments}");

	var psi = new ProcessStartInfo {
		FileName = adbPath,
		Arguments = arguments,
		UseShellExecute = false,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		CreateNoWindow = true,
	};

	using var process = Process.Start (psi);
	if (process == null)
		return (-1, "", "Failed to start process");

	// Read both streams asynchronously to avoid potential deadlock
	var outputTask = process.StandardOutput.ReadToEndAsync ();
	var errorTask = process.StandardError.ReadToEndAsync ();

	process.WaitForExit ();

	return (process.ExitCode, outputTask.Result, errorTask.Result);
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
