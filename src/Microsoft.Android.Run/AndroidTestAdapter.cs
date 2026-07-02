using System.Text;
using System.Threading.Channels;
using System.Xml.Linq;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using Xamarin.Android.Tools;

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
		// Shared state for the streamed run: the set of tests we've streamed a
		// final outcome for (a non-empty set means streaming was authoritative, so
		// we skip the TRX fallback), and which test — if any — is currently
		// executing (so a crash can resolve it to a terminal state instead of
		// leaving it "in progress").
		var state = new StreamingState ();

		// 1. Run instrumentation on device, publishing each test to MTP live as
		//    its INSTRUMENTATION_STATUS block arrives.
		var bundleResults = await RunInstrumentationOnDeviceAsync (context, sessionUid, state, context.CancellationToken);

		if (verbose) {
			Console.WriteLine ($"[AndroidTestAdapter] Instrumentation results: passed={bundleResults.Passed}, failed={bundleResults.Failed}, skipped={bundleResults.Skipped}, streamed={state.ReportedFinal.Count}");
			if (bundleResults.ResultsPath != null)
				Console.WriteLine ($"[AndroidTestAdapter] TRX path on device: {bundleResults.ResultsPath}");
		}

		// 2. If we streamed at least one completed test, streaming is authoritative
		//    (it's resilient to a mid-run crash). Otherwise fall back to the TRX.
		if (state.ReportedFinal.Count == 0) {
			await ReportFromTrxAsync (context, sessionUid, bundleResults);
			return;
		}

		// 3. We streamed live results. If the run did not finish cleanly (the app
		//    process crashed before writing the final results bundle), surface the
		//    crash so the run is clearly marked failed rather than silently dropping
		//    the in-flight/never-run tests.
		if (bundleResults.Crashed)
			await PublishCrashAsync (context, sessionUid, bundleResults, state);
	}

	/// <summary>
	/// Publishes each test to MTP as its INSTRUMENTATION_STATUS block arrives from
	/// the streamed 'am instrument -w -r' output, and returns the parsed final
	/// results bundle (summary counts, resultsPath, error, crash state).
	/// </summary>
	async Task<InstrumentationBundleResult> RunInstrumentationOnDeviceAsync (ExecuteRequestContext context, SessionUid sessionUid, StreamingState state, CancellationToken cancellationToken)
	{
		// '-r' prints raw INSTRUMENTATION_STATUS results (so we can stream them);
		// '-w' waits for the run to complete.
		var cmdArgs = $"shell am instrument -w -r {package}/{instrumentation}";
		var psi = AdbHelper.CreateStartInfo (adbPath, adbTarget, cmdArgs);

		if (verbose)
			Console.WriteLine ($"Running: adb {psi.Arguments}");

		// Completed status blocks are handed off to a single async consumer that
		// publishes them to MTP, so the synchronous stdout read loop is never
		// blocked on an async PublishAsync.
		var channel = Channel.CreateUnbounded<InstrumentationStatus> (new UnboundedChannelOptions {
			SingleReader = true,
			SingleWriter = true,
		});

		var fullOutput = new StringBuilder ();
		using var stderr = new StringWriter ();

		var consumer = Task.Run (async () => {
			await foreach (var status in channel.Reader.ReadAllAsync ()) {
				try {
					await PublishStatusAsync (context, sessionUid, status, state);
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					// One malformed/unreportable status block must not abort
					// reporting for the rest of the run.
					Console.Error.WriteLine ($"[AndroidTestAdapter] Failed to report a streamed test result: {ex}");
				}
			}
		});

		var parser = new StatusStreamParser (channel.Writer, fullOutput, verbose);
		using var stdout = new LineWriter (parser.OnLine);

		int exitCode = 0;
		try {
			exitCode = await ProcessUtils.StartProcess (psi, stdout, stderr, cancellationToken);
		} finally {
			// Flush the trailing partial line, discard any unterminated status
			// block, then complete the channel and always observe the consumer —
			// even if StartProcess threw or was cancelled.
			stdout.Flush ();
			parser.Complete ();
			channel.Writer.Complete ();
			await consumer;
		}

		var output = fullOutput.ToString ();
		if (verbose) {
			Console.WriteLine ($"[AndroidTestAdapter] Exit code: {exitCode}");
			Console.WriteLine (output);
		}

		var result = ParseInstrumentationBundle (output, stderr.ToString ());

		// The final bundle (with resultsPath) is only emitted if Finish() ran to
		// completion. Its absence — or a non-zero exit / an explicit crash marker —
		// means the app process died mid-run.
		result.Crashed =
			exitCode != 0 ||
			output.Contains ("INSTRUMENTATION_FAILED", StringComparison.Ordinal) ||
			output.Contains ("Process crashed", StringComparison.Ordinal) ||
			(state.ReportedFinal.Count > 0 && result.ResultsPath == null);

		if (result.Crashed && result.Error == null)
			result.Error = ExtractCrashMessage (output) ?? (exitCode != 0 ? $"Instrumentation exited with code {exitCode}." : "The test process terminated before reporting a result (likely a native crash).");

		return result;
	}

	/// <summary>
	/// Publishes a single streamed instrumentation status block to MTP: a "start"
	/// event becomes an in-progress node; a "finish" event becomes the final
	/// pass/fail/skip node. Blocks that aren't part of this streaming protocol
	/// (no recognized "event", or a "finish" without an "outcome") are ignored so
	/// they neither mis-report a test nor suppress the TRX fallback.
	/// </summary>
	async Task PublishStatusAsync (ExecuteRequestContext context, SessionUid sessionUid, InstrumentationStatus status, StreamingState state)
	{
		// Only handle our explicit streaming protocol. Other instrumentation
		// (e.g. AndroidJUnitRunner) emits status blocks with a "test" key but no
		// "event"/"outcome"; treating those as results would report them as
		// passed and, worse, mark ReportedFinal non-empty so the TRX fallback
		// never runs. Skip them and let ReportFromTrxAsync handle the run.
		if (!status.Values.TryGetValue (InstrumentationProtocol.KeyEvent, out var eventKind) || (eventKind != InstrumentationProtocol.EventStart && eventKind != InstrumentationProtocol.EventFinish))
			return;

		if (!status.Values.TryGetValue (InstrumentationProtocol.KeyTest, out var fullyQualifiedName) || string.IsNullOrEmpty (fullyQualifiedName))
			return;

		status.Values.TryGetValue (InstrumentationProtocol.KeyName, out var displayName);
		status.Values.TryGetValue (InstrumentationProtocol.KeyClass, out var className);

		if (eventKind == InstrumentationProtocol.EventStart) {
			// Remember the running test so a crash can resolve it to a terminal
			// state instead of leaving a dangling "in progress" node.
			state.InFlightUid = fullyQualifiedName;
			state.InFlightName = displayName;
			var startNode = new TestNode {
				Uid = new TestNodeUid (fullyQualifiedName),
				DisplayName = displayName ?? fullyQualifiedName,
				Properties = new PropertyBag (new InProgressTestNodeStateProperty ()),
			};
			await context.MessageBus.PublishAsync (this, new TestNodeUpdateMessage (sessionUid, startNode));
			return;
		}

		// eventKind == "finish": a valid completion must carry an outcome.
		if (!status.Values.TryGetValue (InstrumentationProtocol.KeyOutcome, out var outcome) || string.IsNullOrEmpty (outcome))
			return;

		// The test completed, so it's no longer in flight.
		if (state.InFlightUid == fullyQualifiedName) {
			state.InFlightUid = null;
			state.InFlightName = null;
		}

		var errorMessage = DecodeOrNull (status.Values, InstrumentationProtocol.KeyMessageBase64);
		var stackTrace = DecodeOrNull (status.Values, InstrumentationProtocol.KeyStackBase64);

		var failureMessage = errorMessage ?? "Test failed";
		if (!string.IsNullOrEmpty (stackTrace))
			failureMessage += "\n" + stackTrace;

		// An unrecognized outcome is reported as an error rather than silently
		// counted as a pass.
		var stateProperty = outcome switch {
			InstrumentationProtocol.OutcomePassed => (IProperty) new PassedTestNodeStateProperty (),
			InstrumentationProtocol.OutcomeFailed => new FailedTestNodeStateProperty (failureMessage),
			InstrumentationProtocol.OutcomeSkipped => new SkippedTestNodeStateProperty (errorMessage),
			_ => new ErrorTestNodeStateProperty ($"Unrecognized test outcome '{outcome}'."),
		};

		var properties = new List<IProperty> { stateProperty };
		if (!string.IsNullOrEmpty (className))
			properties.Add (new TrxFullyQualifiedTypeNameProperty (className));
		if (outcome == InstrumentationProtocol.OutcomeFailed && (!string.IsNullOrEmpty (errorMessage) || !string.IsNullOrEmpty (stackTrace)))
			properties.Add (new TrxExceptionProperty (errorMessage, stackTrace));

		var testNode = new TestNode {
			Uid = new TestNodeUid (fullyQualifiedName),
			DisplayName = displayName ?? fullyQualifiedName,
			Properties = new PropertyBag (properties.ToArray ()),
		};

		await context.MessageBus.PublishAsync (this, new TestNodeUpdateMessage (sessionUid, testNode));
		state.ReportedFinal.Add (fullyQualifiedName);
	}

	/// <summary>
	/// Reports a mid-run process crash. If a test was executing when the process
	/// died, that test is resolved from "in progress" to an <see cref="ErrorTestNodeStateProperty"/>
	/// (an infrastructure error, not an assertion failure) so it isn't left
	/// dangling and the crash is attributed to it. Otherwise a synthetic error
	/// node is published so the overall run is still clearly marked failed.
	/// </summary>
	async Task PublishCrashAsync (ExecuteRequestContext context, SessionUid sessionUid, InstrumentationBundleResult bundleResults, StreamingState state)
	{
		var message = bundleResults.Error ?? "The test process terminated before all tests completed (likely a native crash).";
		Console.Error.WriteLine ($"[AndroidTestAdapter] {message}");

		TestNode crashNode;
		if (state.InFlightUid != null) {
			crashNode = new TestNode {
				Uid = new TestNodeUid (state.InFlightUid),
				DisplayName = state.InFlightName ?? state.InFlightUid,
				Properties = new PropertyBag (
					new ErrorTestNodeStateProperty ($"The test process crashed while running this test.\n{message}"),
					new TrxExceptionProperty (message, null)),
			};
			state.ReportedFinal.Add (state.InFlightUid);
			state.InFlightUid = null;
			state.InFlightName = null;
		} else {
			crashNode = new TestNode {
				Uid = new TestNodeUid ($"{instrumentation}.ProcessCrashed"),
				DisplayName = "Test process crashed before completion",
				Properties = new PropertyBag (
					new ErrorTestNodeStateProperty (message),
					new TrxFullyQualifiedTypeNameProperty (instrumentation),
					new TrxExceptionProperty (message, null)),
			};
		}

		await context.MessageBus.PublishAsync (this, new TestNodeUpdateMessage (sessionUid, crashNode));
	}

	/// <summary>
	/// Fallback path used only when no results were streamed (e.g. an older
	/// on-device instrumentation): pull and parse the TRX, then publish all tests.
	/// </summary>
	async Task ReportFromTrxAsync (ExecuteRequestContext context, SessionUid sessionUid, InstrumentationBundleResult bundleResults)
	{
		if (bundleResults.Error != null) {
			var message = $"Error from instrumentation: {bundleResults.Error}";
			if (bundleResults.InstrumentationCode.HasValue)
				message += $" (code: {bundleResults.InstrumentationCode.Value})";
			Console.Error.WriteLine (message);
			throw new InvalidOperationException (message);
		}

		if (bundleResults.ResultsPath == null)
			throw new InvalidOperationException ("Instrumentation did not report a resultsPath in the bundle.");

		var localTrxPath = await PullTrxFileAsync (bundleResults.ResultsPath, context.CancellationToken);
		if (localTrxPath == null)
			throw new InvalidOperationException ($"Failed to pull TRX file from device: {bundleResults.ResultsPath}");

		var testResults = ParseTrxFile (localTrxPath);
		foreach (var result in testResults) {
			// Build the failure message including stack trace for non-TRX consumers
			var failureMessage = result.ErrorMessage ?? "Test failed";
			if (!string.IsNullOrEmpty (result.StackTrace))
				failureMessage += "\n" + result.StackTrace;

			var stateProperty = result.Outcome switch {
				TrxOutcome.Passed => (IProperty) new PassedTestNodeStateProperty (),
				TrxOutcome.Failed => new FailedTestNodeStateProperty (failureMessage),
				TrxOutcome.NotExecuted => new SkippedTestNodeStateProperty (result.ErrorMessage),
				_ => new PassedTestNodeStateProperty (),
			};

			var properties = new List<IProperty> { stateProperty };

			// Add TRX report properties required by ITrxReportCapability
			if (!string.IsNullOrEmpty (result.ClassName))
				properties.Add (new TrxFullyQualifiedTypeNameProperty (result.ClassName));
			if (result.Outcome == TrxOutcome.Failed && (!string.IsNullOrEmpty (result.ErrorMessage) || !string.IsNullOrEmpty (result.StackTrace)))
				properties.Add (new TrxExceptionProperty (result.ErrorMessage, result.StackTrace));

			var testNode = new TestNode {
				Uid = new TestNodeUid (result.FullyQualifiedName),
				DisplayName = result.TestName,
				Properties = new PropertyBag (properties.ToArray ()),
			};

			await context.MessageBus.PublishAsync (this, new TestNodeUpdateMessage (sessionUid, testNode));
		}
	}

	static string? DecodeOrNull (IReadOnlyDictionary<string, string> values, string key)
	{
		if (!values.TryGetValue (key, out var encoded) || string.IsNullOrEmpty (encoded))
			return null;
		try {
			return Encoding.UTF8.GetString (Convert.FromBase64String (encoded));
		} catch (FormatException) {
			return encoded;
		}
	}

	/// <summary>
	/// Extracts a human-readable crash reason from the instrumentation output
	/// (shortMsg/longMsg are emitted by 'am instrument' when the process crashes).
	/// </summary>
	static string? ExtractCrashMessage (string output)
	{
		string? shortMsg = null, longMsg = null;
		foreach (var rawLine in output.Split ('\n')) {
			var line = rawLine.TrimEnd ('\r');
			if (line.StartsWith ("INSTRUMENTATION_RESULT: shortMsg=", StringComparison.Ordinal))
				shortMsg = line.Substring ("INSTRUMENTATION_RESULT: shortMsg=".Length).Trim ();
			else if (line.StartsWith ("INSTRUMENTATION_RESULT: longMsg=", StringComparison.Ordinal))
				longMsg = line.Substring ("INSTRUMENTATION_RESULT: longMsg=".Length).Trim ();
		}
		if (longMsg != null || shortMsg != null)
			return $"The test process crashed: {longMsg ?? shortMsg}";
		return null;
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

		if (bundleValues.TryGetValue (InstrumentationProtocol.KeyPassedCount, out var passedStr) && int.TryParse (passedStr, out int passed))
			result.Passed = passed;
		if (bundleValues.TryGetValue (InstrumentationProtocol.KeyFailedCount, out var failedStr) && int.TryParse (failedStr, out int failed))
			result.Failed = failed;
		if (bundleValues.TryGetValue (InstrumentationProtocol.KeySkippedCount, out var skippedStr) && int.TryParse (skippedStr, out int skipped))
			result.Skipped = skipped;
		if (bundleValues.TryGetValue (InstrumentationProtocol.KeyResultsPath, out var resultsPath))
			result.ResultsPath = resultsPath;
		if (bundleValues.TryGetValue (InstrumentationProtocol.KeyError, out var bundleError))
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

				// Extract error message and stack trace if present
				string? errorMessage = null;
				string? stackTrace = null;
				var outputElement = unitTestResult.Element (ns + "Output");
				var errorInfo = outputElement?.Element (ns + "ErrorInfo");
				if (errorInfo != null) {
					errorMessage = errorInfo.Element (ns + "Message")?.Value;
					stackTrace = errorInfo.Element (ns + "StackTrace")?.Value;
				}

				var trxOutcome = outcome switch {
					"Passed" => TrxOutcome.Passed,
					"Failed" => TrxOutcome.Failed,
					"NotExecuted" => TrxOutcome.NotExecuted,
					_ => TrxOutcome.Passed,
				};

				results.Add (new TrxTestResult (fullyQualifiedName, testName, className, trxOutcome, errorMessage, stackTrace));
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
	public bool Crashed { get; set; }
}

/// <summary>
/// Mutable state shared across a streamed run: the tests streamed with a final
/// outcome (a non-empty set means streaming was authoritative, so the TRX
/// fallback is skipped), and the test currently executing (if any).
/// </summary>
sealed class StreamingState
{
	public HashSet<string> ReportedFinal { get; } = new (StringComparer.Ordinal);
	public string? InFlightUid { get; set; }
	public string? InFlightName { get; set; }
}

/// <summary>
/// A single completed <c>INSTRUMENTATION_STATUS</c> block: its key/value pairs
/// plus the trailing <c>INSTRUMENTATION_STATUS_CODE</c>.
/// </summary>
class InstrumentationStatus
{
	public required IReadOnlyDictionary<string, string> Values { get; init; }
	public int Code { get; init; }
}

/// <summary>
/// A <see cref="TextWriter"/> that splits the (arbitrarily chunked) writes it
/// receives from <c>ProcessUtils.StartProcess</c> into complete lines and invokes
/// a callback for each one, so instrumentation output can be parsed as it streams.
/// </summary>
sealed class LineWriter (Action<string> onLine) : TextWriter
{
	readonly StringBuilder buffer = new ();

	public override Encoding Encoding => Encoding.UTF8;

	public override void Write (char value)
	{
		if (value == '\n')
			Flush ();
		else if (value != '\r')
			buffer.Append (value);
	}

	public override void Write (char[] buffer, int index, int count)
	{
		for (int i = 0; i < count; i++)
			Write (buffer [index + i]);
	}

	public override void Write (string? value)
	{
		if (value == null)
			return;
		foreach (var c in value)
			Write (c);
	}

	// Emit whatever has been buffered as a completed line.
	public override void Flush ()
	{
		if (buffer.Length == 0)
			return;
		var line = buffer.ToString ();
		buffer.Clear ();
		onLine (line);
	}
}

/// <summary>
/// Incrementally parses streamed <c>am instrument -r</c> output. Each completed
/// <c>INSTRUMENTATION_STATUS</c> block (terminated by <c>INSTRUMENTATION_STATUS_CODE</c>)
/// is written to <paramref name="writer"/> for the consumer to publish, while the
/// raw text is also accumulated for the final results-bundle parse.
/// </summary>
sealed class StatusStreamParser (ChannelWriter<InstrumentationStatus> writer, StringBuilder fullOutput, bool verbose)
{
	const string StatusPrefix = "INSTRUMENTATION_STATUS: ";
	const string StatusCodePrefix = "INSTRUMENTATION_STATUS_CODE: ";

	Dictionary<string, string>? current;
	string? lastKey;

	public void OnLine (string line)
	{
		fullOutput.Append (line).Append ('\n');
		if (verbose)
			Console.WriteLine (line);

		if (line.StartsWith (StatusCodePrefix, StringComparison.Ordinal)) {
			var codeStr = line.Substring (StatusCodePrefix.Length).Trim ();
			int.TryParse (codeStr, out int code);
			if (current != null) {
				writer.TryWrite (new InstrumentationStatus { Values = current, Code = code });
				current = null;
				lastKey = null;
			}
			return;
		}

		if (line.StartsWith (StatusPrefix, StringComparison.Ordinal)) {
			var kvp = line.Substring (StatusPrefix.Length);
			var eqIndex = kvp.IndexOf ('=');
			if (eqIndex > 0) {
				current ??= new Dictionary<string, string> (StringComparer.Ordinal);
				lastKey = kvp.Substring (0, eqIndex).Trim ();
				current [lastKey] = kvp.Substring (eqIndex + 1);
			}
			return;
		}

		// Continuation of a multi-line value (should be rare — message/stack are
		// Base64-encoded on the device precisely to avoid this).
		if (current != null && lastKey != null)
			current [lastKey] += "\n" + line;
	}

	// Drop any block that never received a terminating status code (e.g. the
	// process crashed mid-status); it is intentionally not published.
	public void Complete ()
	{
		current = null;
		lastKey = null;
	}
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
	string? ClassName,
	TrxOutcome Outcome,
	string? ErrorMessage,
	string? StackTrace);

class AndroidTestCapabilities : ITestFrameworkCapabilities
{
	public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } = [new AndroidTrxReportCapability ()];
}

class AndroidTrxReportCapability : ITrxReportCapability
{
	public bool IsSupported => true;

	public void Enable ()
	{
		// No-op: TRX properties are always added to test nodes.
	}
}
