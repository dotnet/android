#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

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
	public const string KeyAssembly = "assembly";
	public const string KeyNamespace = "namespace";
	public const string KeyType = "type";
	public const string KeyMethod = "method";
	public const string KeyMethodArity = "method-arity";
	public const string KeyParameterTypes = "parameter-types";
	public const string KeyReturnType = "return-type";

	// Instrumentation extras. Base64 XML preserves parameterized test IDs,
	// including quotes, whitespace and shell metacharacters.
	public const string ExtraDiscover = "list-tests";
	public const string ExtraTestUids = "test-uids";
	public const string TestUidsDirectoryRequest = "file-location";
	public const string TestUidsFilePrefix = "file:";

	// Keys used in the final results bundle.
	public const string KeyPassedCount = "passed";
	public const string KeyFailedCount = "failed";
	public const string KeySkippedCount = "skipped";
	public const string KeyResultsPath = "resultsPath";
	public const string KeyError = "error";
	public const string KeyDiscoveredCount = "discovered";
	public const string KeyTestUidsDirectory = "test-uids-directory";

	// "event" values.
	public const string EventStart = "start";
	public const string EventFinish = "finish";
	public const string EventDiscover = "discover";

	// "outcome" values.
	public const string OutcomePassed = "passed";
	public const string OutcomeFailed = "failed";
	public const string OutcomeSkipped = "skipped";

	public static string EncodeTestUids (IEnumerable<string> testUids)
		=> Convert.ToBase64String (Encoding.UTF8.GetBytes (
			new XElement ("tests", testUids.Select (uid => new XElement ("test", new XAttribute ("uid", uid)))).ToString (SaveOptions.DisableFormatting)));

	public static string[] DecodeTestUids (string encoded)
		=> XElement.Parse (Encoding.UTF8.GetString (Convert.FromBase64String (encoded)))
			.Elements ("test").Select (test => (string?) test.Attribute ("uid")
				?? throw new FormatException ("Missing test UID in the instrumentation filter.")).ToArray ();

	public static string GetTestUidsFileName (string token)
	{
		if (!Guid.TryParseExact (token, "N", out _))
			throw new FormatException ("Invalid instrumentation filter file token.");
		return $"test-uids-{token}.txt";
	}

	public static string ReadTestUidsArgument (string argument, string? externalFilesPath)
	{
		if (!argument.StartsWith (TestUidsFilePrefix, StringComparison.Ordinal))
			return argument;
		if (externalFilesPath == null)
			throw new InvalidOperationException ("The app's external files directory is unavailable.");
		var fileName = GetTestUidsFileName (argument.Substring (TestUidsFilePrefix.Length));
		return File.ReadAllText (Path.Combine (externalFilesPath, fileName));
	}
}
