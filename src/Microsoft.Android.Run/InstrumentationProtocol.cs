/// <summary>
/// Wire contract for the on-device test instrumentation streaming protocol. The
/// device side (<c>Xamarin.Android.UnitTests.TestInstrumentation</c>) emits
/// <c>INSTRUMENTATION_STATUS</c>/<c>INSTRUMENTATION_RESULT</c> bundles through
/// <c>am instrument -r</c>, and the host side (<c>AndroidTestAdapter</c>) parses
/// them. Values are kept as human-readable strings so the raw instrumentation
/// output stays legible in logcat. This file is source-linked into both the
/// device test runner and the host so the two sides cannot drift out of sync.
/// </summary>
static class InstrumentationProtocol
{
	// Keys used in the per-test streaming status blocks.
	public const string KeyEvent = "event";
	public const string KeyTest = "test";
	public const string KeyName = "name";
	public const string KeyClass = "class";
	public const string KeyOutcome = "outcome";
	public const string KeyMessageBase64 = "message-b64";
	public const string KeyStackBase64 = "stack-b64";

	// Keys used in the final results bundle.
	public const string KeyPassedCount = "passed";
	public const string KeyFailedCount = "failed";
	public const string KeySkippedCount = "skipped";
	public const string KeyResultsPath = "resultsPath";
	public const string KeyError = "error";

	// "event" values.
	public const string EventStart = "start";
	public const string EventFinish = "finish";

	// "outcome" values.
	public const string OutcomePassed = "passed";
	public const string OutcomeFailed = "failed";
	public const string OutcomeSkipped = "skipped";
}
