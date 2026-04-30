using System.Xml.Linq;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

/// <summary>
/// A Microsoft Testing Platform test framework adapter that runs Android instrumentation
/// tests on a device/emulator via adb, pulls the TRX results file, and reports
/// individual test results back through MTP.
/// </summary>
class AndroidTestAdapter(
	string adbPath,
	string? adbTarget,
	string package,
	string instrumentation,
	bool verbose) : ITestFramework, IDataProducer
{
	public string Uid => "android-test-adapter";
	public string DisplayName => "Android Instrumentation Test Adapter";
	public string Description => "Runs Android instrumentation tests on a device or emulator.";
	public string Version => "1.0.0";

	public Type[] DataTypesProduced => [typeof (TestNodeUpdateMessage)];

	public Task<bool> IsEnabledAsync () => Task.FromResult (true);

	public Task<CreateTestSessionResult> CreateTestSessionAsync (CreateTestSessionContext context)
		=> Task.FromResult (new CreateTestSessionResult { IsSuccess = true });

	public Task<CloseTestSessionResult> CloseTestSessionAsync (CloseTestSessionContext context)
		=> Task.FromResult (new CloseTestSessionResult { IsSuccess = true });

	public async Task ExecuteRequestAsync (ExecuteRequestContext context)
	{
		switch (context.Request) {
		case RunTestExecutionRequest runRequest:
			await RunAndReportAsync (context, runRequest.Session.SessionUid);
			break;
		default:
			break;
		}

		context.Complete ();
	}

	async Task RunAndReportAsync (ExecuteRequestContext context, SessionUid sessionUid)
	{
		// 1. Run instrumentation on device
		var bundleResults = await RunInstrumentationOnDeviceAsync (context.CancellationToken);

		if (bundleResults.Error != null) {
			var message = $"Error from instrumentation: {bundleResults.Error}";
			if (bundleResults.InstrumentationCode.HasValue)
				message += $" (code: {bundleResults.InstrumentationCode.Value})";
			Console.Error.WriteLine (message);
			throw new InvalidOperationException (message);
		}

		if (verbose) {
			Console.WriteLine ($"[AndroidTestAdapter] Instrumentation results: passed={bundleResults.Passed}, failed={bundleResults.Failed}, skipped={bundleResults.Skipped}");
			if (bundleResults.ResultsPath != null)
				Console.WriteLine ($"[AndroidTestAdapter] TRX path on device: {bundleResults.ResultsPath}");
		}

		// 2. Pull and parse TRX
		if (bundleResults.ResultsPath == null)
			throw new InvalidOperationException ("Instrumentation did not report a resultsPath in the bundle.");

		var localTrxPath = await PullTrxFileAsync (bundleResults.ResultsPath, context.CancellationToken);
		if (localTrxPath == null)
			throw new InvalidOperationException ($"Failed to pull TRX file from device: {bundleResults.ResultsPath}");

		var testResults = ParseTrxFile (localTrxPath);
		foreach (var result in testResults) {
			var stateProperty = result.Outcome switch {
				TrxOutcome.Passed => (IProperty) new PassedTestNodeStateProperty (),
				TrxOutcome.Failed => new FailedTestNodeStateProperty (result.ErrorMessage ?? "Test failed"),
				TrxOutcome.NotExecuted => new SkippedTestNodeStateProperty (result.ErrorMessage),
				_ => new PassedTestNodeStateProperty (),
			};

			var testNode = new TestNode {
				Uid = new TestNodeUid (result.FullyQualifiedName),
				DisplayName = result.TestName,
				Properties = new PropertyBag (stateProperty),
			};

			await context.MessageBus.PublishAsync (this, new TestNodeUpdateMessage (sessionUid, testNode));
		}
	}

	async Task<InstrumentationBundleResult> RunInstrumentationOnDeviceAsync (CancellationToken cancellationToken)
	{
		var cmdArgs = $"shell am instrument -w {package}/{instrumentation}";
		var (exitCode, output, error) = await AdbHelper.RunAsync (adbPath, adbTarget, cmdArgs, cancellationToken, verbose);

		if (verbose) {
			Console.WriteLine ($"[AndroidTestAdapter] Exit code: {exitCode}");
			Console.WriteLine (output);
		}

		if (exitCode != 0) {
			var failureMessage = !string.IsNullOrWhiteSpace (error) ? error : output;
			if (string.IsNullOrWhiteSpace (failureMessage))
				failureMessage = $"adb shell am instrument failed with exit code {exitCode}.";
			Console.Error.WriteLine ($"[AndroidTestAdapter] {failureMessage}");
			return new InstrumentationBundleResult {
				Error = failureMessage,
			};
		}

		return ParseInstrumentationBundle (output, error);
	}

	async Task<string?> PullTrxFileAsync (string devicePath, CancellationToken cancellationToken)
	{
		var localDir = Path.Combine (Path.GetTempPath (), "AndroidTestResults", package);
		Directory.CreateDirectory (localDir);

		var localPath = Path.Combine (localDir, Path.GetFileName (devicePath));

		if (verbose)
			Console.WriteLine ($"[AndroidTestAdapter] Pulling TRX: {devicePath} -> {localPath}");

		var (exitCode, output, error) = await AdbHelper.RunAsync (adbPath, adbTarget, $"pull \"{devicePath}\" \"{localPath}\"", cancellationToken, verbose);

		if (exitCode != 0) {
			Console.Error.WriteLine ($"[AndroidTestAdapter] Failed to pull TRX file: {error}");
			return null;
		}

		if (!File.Exists (localPath)) {
			Console.Error.WriteLine ($"[AndroidTestAdapter] TRX file not found after pull: {localPath}");
			return null;
		}

		if (verbose)
			Console.WriteLine ($"[AndroidTestAdapter] TRX file pulled to: {localPath}");

		return localPath;
	}

	/// <summary>
	/// Parses the INSTRUMENTATION_RESULT bundle lines from 'am instrument -w' output.
	///
	/// Expected format:
	///   INSTRUMENTATION_RESULT: passed=1
	///   INSTRUMENTATION_RESULT: failed=0
	///   INSTRUMENTATION_RESULT: skipped=0
	///   INSTRUMENTATION_RESULT: resultsPath=/path/to/file.trx
	///   INSTRUMENTATION_CODE: -1
	/// </summary>
	static InstrumentationBundleResult ParseInstrumentationBundle (string output, string error)
	{
		var result = new InstrumentationBundleResult ();
		var bundleValues = new Dictionary<string, string> ();

		foreach (var rawLine in output.Split ('\n')) {
			var line = rawLine.TrimEnd ('\r');

			if (line.StartsWith ("INSTRUMENTATION_RESULT: ", StringComparison.Ordinal)) {
				var kvp = line.Substring ("INSTRUMENTATION_RESULT: ".Length);
				var eqIndex = kvp.IndexOf ('=');
				if (eqIndex > 0) {
					var key = kvp.Substring (0, eqIndex).Trim ();
					var value = kvp.Substring (eqIndex + 1).Trim ();
					bundleValues [key] = value;
				}
			} else if (line.StartsWith ("INSTRUMENTATION_CODE: ", StringComparison.Ordinal)) {
				var codeStr = line.Substring ("INSTRUMENTATION_CODE: ".Length).Trim ();
				if (int.TryParse (codeStr, out int code))
					result.InstrumentationCode = code;
			}
		}

		if (bundleValues.TryGetValue ("passed", out var passedStr) && int.TryParse (passedStr, out int passed))
			result.Passed = passed;
		if (bundleValues.TryGetValue ("failed", out var failedStr) && int.TryParse (failedStr, out int failed))
			result.Failed = failed;
		if (bundleValues.TryGetValue ("skipped", out var skippedStr) && int.TryParse (skippedStr, out int skipped))
			result.Skipped = skipped;
		if (bundleValues.TryGetValue ("resultsPath", out var resultsPath))
			result.ResultsPath = resultsPath;
		if (bundleValues.TryGetValue ("error", out var bundleError))
			result.Error = bundleError;

		// Surface adb stderr if no results were parsed
		if (!string.IsNullOrWhiteSpace (error) && result.ResultsPath == null && result.Error == null)
			result.Error = error;

		return result;
	}

	/// <summary>
	/// Parses a TRX (Visual Studio Test Results) XML file into individual test results.
	/// </summary>
	static List<TrxTestResult> ParseTrxFile (string trxPath)
	{
		var results = new List<TrxTestResult> ();
		var doc = XDocument.Load (trxPath);
		var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

		// Build a map from testId -> test definition for the fully qualified name
		var testDefinitions = new Dictionary<string, (string ClassName, string TestName)> ();
		var testDefinitionsElement = doc.Root?.Element (ns + "TestDefinitions");
		if (testDefinitionsElement != null) {
			foreach (var unitTest in testDefinitionsElement.Elements (ns + "UnitTest")) {
				var id = unitTest.Attribute ("id")?.Value;
				var name = unitTest.Attribute ("name")?.Value;
				var testMethod = unitTest.Element (ns + "TestMethod");
				var className = testMethod?.Attribute ("className")?.Value;

				if (id != null && name != null)
					testDefinitions [id] = (className ?? "", name);
			}
		}

		// Parse the Results section
		var resultsElement = doc.Root?.Element (ns + "Results");
		if (resultsElement != null) {
			foreach (var unitTestResult in resultsElement.Elements (ns + "UnitTestResult")) {
				var testId = unitTestResult.Attribute ("testId")?.Value;
				var testName = unitTestResult.Attribute ("testName")?.Value ?? "Unknown";
				var outcome = unitTestResult.Attribute ("outcome")?.Value ?? "Passed";

				string? className = null;
				if (testId != null && testDefinitions.TryGetValue (testId, out var def))
					className = def.ClassName;

				var fullyQualifiedName = !string.IsNullOrEmpty (className)
					? $"{className}.{testName}"
					: testName;

				// Extract error message if present
				string? errorMessage = null;
				var outputElement = unitTestResult.Element (ns + "Output");
				var errorInfo = outputElement?.Element (ns + "ErrorInfo");
				if (errorInfo != null) {
					var message = errorInfo.Element (ns + "Message")?.Value;
					var stackTrace = errorInfo.Element (ns + "StackTrace")?.Value;
					errorMessage = message;
					if (!string.IsNullOrEmpty (stackTrace))
						errorMessage = string.IsNullOrEmpty (errorMessage)
							? stackTrace
							: $"{errorMessage}\n{stackTrace}";
				}

				var trxOutcome = outcome switch {
					"Passed" => TrxOutcome.Passed,
					"Failed" => TrxOutcome.Failed,
					"NotExecuted" => TrxOutcome.NotExecuted,
					_ => TrxOutcome.Passed,
				};

				results.Add (new TrxTestResult (fullyQualifiedName, testName, trxOutcome, errorMessage));
			}
		}

		return results;
	}
}

class InstrumentationBundleResult
{
	public int Passed { get; set; }
	public int Failed { get; set; }
	public int Skipped { get; set; }
	public string? ResultsPath { get; set; }
	public string? Error { get; set; }
	public int? InstrumentationCode { get; set; }
}

enum TrxOutcome
{
	Passed,
	Failed,
	NotExecuted,
}

record TrxTestResult (
	string FullyQualifiedName,
	string TestName,
	TrxOutcome Outcome,
	string? ErrorMessage);

class AndroidTestCapabilities : ITestFrameworkCapabilities
{
	public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } = [];
}
