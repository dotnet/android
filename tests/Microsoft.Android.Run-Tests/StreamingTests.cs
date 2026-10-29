using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using NUnit.Framework;

[TestFixture]
[NonParallelizable]
public class StreamingTests
{
	string directory = "";
	TextWriter previousError = Console.Error;
	StringWriter error = new ();

	[SetUp]
	public void SetUp ()
	{
		directory = Path.Combine (Path.GetTempPath (), "android-streaming-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
		previousError = Console.Error;
		error = new StringWriter ();
		Console.SetError (TextWriter.Synchronized (error));
	}

	[TearDown]
	public void TearDown ()
	{
		Console.SetError (previousError);
		error.Dispose ();
		Directory.Delete (directory, recursive: true);
	}

	static InstrumentationStatus Status (string kind, string test, string? outcome = null)
	{
		var values = new Dictionary<string, string> { ["event"] = kind, ["test"] = test };
		if (outcome != null)
			values ["outcome"] = outcome;
		return new () { Values = values };
	}

	[Test]
	public void PartialResultsPreserveObservedOutcomesAndAllUnfinishedTests ()
	{
		var state = new StreamingState ();
		foreach (var outcome in new [] { "passed", "failed", "skipped", "unexpected" }) {
			state.Observe (Status ("start", "Tests." + outcome));
			state.Observe (Status ("finish", "Tests." + outcome, outcome));
		}
		state.Observe (Status ("start", "Tests.ActiveOne"));
		state.Observe (Status ("start", "Tests.ActiveTwo"));
		state.Observe (Status ("finish", "Tests.ActiveOne")); // Missing outcome is not a completion.
		state.Observe (Status ("other", "Tests.Ignored", "passed"));
		state.WritePartialResults ();
		Assert.That (error.ToString (), Does.Contain ("passed=1, failed=1, skipped=1, unknown=1"));
		foreach (var outcome in new [] { "passed", "failed", "skipped", "unexpected" })
			Assert.That (error.ToString (), Does.Contain ($"[{outcome}] Tests.{outcome}"));
		Assert.That (error.ToString (), Does.Contain ("[unfinished] Tests.ActiveOne").And.Contain ("[unfinished] Tests.ActiveTwo"));
		Assert.That (error.ToString (), Does.Contain ("not necessarily the cause"));
		Assert.That (state.ReportedFinal, Is.Empty, "Observation must not depend on MTP publication.");
	}

	[Test]
	public void ParserObservesCompleteBlocksImmediatelyButNeverInventsACompletion ()
	{
		var state = new StreamingState ();
		var parser = new StatusStreamParser (state.Observe, new StringBuilder (), verbose: false);
		parser.OnLine ("INSTRUMENTATION_STATUS: event=start");
		parser.OnLine ("INSTRUMENTATION_STATUS: test=Tests.One");
		parser.OnLine ("INSTRUMENTATION_STATUS_CODE: 1");
		Assert.That (state.HasUnfinishedTests, Is.True, "Observed before the instrumentation process exits.");
		parser.OnLine ("INSTRUMENTATION_STATUS: event=finish");
		parser.OnLine ("INSTRUMENTATION_STATUS: test=Tests.One");
		parser.OnLine ("INSTRUMENTATION_STATUS: outcome=passed");
		parser.Complete ();
		state.WritePartialResults ();
		Assert.That (error.ToString (), Does.Contain ("passed=0").And.Contain ("[unfinished] Tests.One"));
	}

	const string Progress = """
		INSTRUMENTATION_STATUS: event=start
		INSTRUMENTATION_STATUS: test=Tests.One
		INSTRUMENTATION_STATUS: class=Tests
		INSTRUMENTATION_STATUS_CODE: 1
		INSTRUMENTATION_STATUS: event=finish
		INSTRUMENTATION_STATUS: test=Tests.One
		INSTRUMENTATION_STATUS: class=Tests
		INSTRUMENTATION_STATUS: outcome=passed
		INSTRUMENTATION_STATUS_CODE: 0
		INSTRUMENTATION_STATUS: event=start
		INSTRUMENTATION_STATUS: test=Tests.Two
		INSTRUMENTATION_STATUS: class=Tests
		INSTRUMENTATION_STATUS_CODE: 1
		""";

	[TestCase (false, false)]
	[TestCase (false, true)]
	[TestCase (true, false)]
	[TestCase (true, true)]
	public async Task AdapterPreservesNormalTrxAndPrintsPartialResultsWithoutTrx (bool crash, bool reportFailure)
	{
		string transcript = Progress + (crash ? "\nINSTRUMENTATION_RESULT: shortMsg=Process crashed.\nINSTRUMENTATION_CODE: 0" : """

			INSTRUMENTATION_STATUS: event=finish
			INSTRUMENTATION_STATUS: test=Tests.Two
			INSTRUMENTATION_STATUS: class=Tests
			INSTRUMENTATION_STATUS: outcome=passed
			INSTRUMENTATION_STATUS_CODE: 0
			INSTRUMENTATION_RESULT: passed=2
			INSTRUMENTATION_RESULT: failed=0
			INSTRUMENTATION_RESULT: skipped=0
			INSTRUMENTATION_RESULT: resultsPath=/device/results.trx
			INSTRUMENTATION_CODE: -1
			""");
		using var application = await CreateApplication (transcript, reportFailure);
		if (reportFailure) {
			Assert.ThrowsAsync<UnauthorizedAccessException> (async () => { await application.RunAsync (); });
			Assert.That (File.Exists (Path.Combine (directory, "results.trx")), Is.False);
		} else {
			Assert.That (await application.RunAsync () == 0, Is.EqualTo (!crash));
			var trx = XDocument.Load (Path.Combine (directory, "results.trx"));
			XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
			var results = trx.Descendants (ns + "UnitTestResult").ToArray ();
			Assert.That (results, Has.Length.EqualTo (2));
			var preceding = results.Single (e => e.Attribute ("testName")?.Value == "Tests.One");
			var last = results.Single (e => e.Attribute ("testName")?.Value == "Tests.Two");
			Assert.That (preceding.Attribute ("outcome")?.Value, Is.EqualTo ("Passed"));
			Assert.That (last.Attribute ("outcome")?.Value, Is.EqualTo (crash ? "Failed" : "Passed"));
			if (crash) {
				var message = last.Element (ns + "Output")?.Element (ns + "ErrorInfo")?.Element (ns + "Message")?.Value;
				Assert.That (message, Is.EqualTo (
					"This was the last observed active test, not necessarily the cause of the incomplete run.\nThe test process crashed: Process crashed."));
			}
		}
		if (crash) {
			Assert.That (error.ToString (), Does.Contain ("Incomplete test run").And.Contain ("passed=1, failed=0, skipped=0, unknown=0"));
			Assert.That (error.ToString (), Does.Contain ("[passed] Tests.One").And.Contain ("[unfinished] Tests.Two"));
		} else {
			Assert.That (error.ToString (), Does.Not.Contain ("Incomplete test run").And.Not.Contain ("[unfinished]"));
		}
	}

	[TestCase ("", 0)]
	[TestCase ("error: device offline", 1)]
	[TestCase (Progress, 0)]
	public async Task MissingBundleOrTransportFailureIsNotLabeledACrash (string transcript, int exitCode)
	{
		using var application = await CreateApplication (transcript, exitCode: exitCode);
		if (exitCode == 0 && transcript.Length == 0)
			Assert.ThrowsAsync<InvalidOperationException> (async () => { await application.RunAsync (); });
		else
			Assert.That (await application.RunAsync (), Is.Not.Zero);
		Assert.That (error.ToString (), Does.Contain ("Incomplete test run"));
		Assert.That (error.ToString (), Does.Contain (transcript == Progress ? "passed=1" : "passed=0"));
		Assert.That (error.ToString (), Does.Not.Contain ("process crashed").And.Not.Contain ("native crash"));
	}

	[Test]
	public async Task ProcessStartupExceptionStillPrintsPartialSummary ()
	{
		using var application = await CreateApplication ("", missingAdb: true);
		Assert.ThrowsAsync<System.ComponentModel.Win32Exception> (async () => { await application.RunAsync (); });
		Assert.That (error.ToString (), Does.Contain ("Incomplete test run").And.Contain ("passed=0"));
	}

	async Task<ITestApplication> CreateApplication (string transcript, bool reportFailure = false, int exitCode = 0, bool missingAdb = false)
	{
		if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows))
			Assert.Ignore ("The fake adb is a Unix shell executable.");
		string adb = Path.Combine (directory, "fake-adb");
		File.WriteAllText (adb, $"#!/bin/sh\ncat <<'TRANSCRIPT'\n{transcript}\nTRANSCRIPT\nexit {exitCode}\n");
		if (!OperatingSystem.IsWindows ())
			File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		if (reportFailure)
			Directory.CreateDirectory (Path.Combine (directory, "results.trx"));
		var builder = await TestApplication.CreateBuilderAsync ([
			"--report-trx", "--report-trx-filename", "results.trx", "--results-directory", directory,
		]);
		var adapter = new AndroidTestAdapter (missingAdb ? Path.Combine (directory, "missing-adb") : adb, null, "tests", "runner", false);
		builder.RegisterTestFramework (_ => new AndroidTestCapabilities (), (_, _) => adapter);
		builder.AddTrxReportProvider ();
		return await builder.BuildAsync ();
	}
}
