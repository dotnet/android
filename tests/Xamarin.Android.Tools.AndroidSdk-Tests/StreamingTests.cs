#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests;

[TestClass]
[DoNotParallelize]
public class StreamingTests
{
	string directory = "";
	TextWriter previousError = Console.Error;
	StringWriter error = new ();

	[TestInitialize]
	public void SetUp ()
	{
		directory = Path.Combine (Path.GetTempPath (), "android-streaming-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
		previousError = Console.Error;
		error = new StringWriter ();
		Console.SetError (TextWriter.Synchronized (error));
	}

	[TestCleanup]
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

	[TestMethod]
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
		Assert.Contains ("passed=1, failed=1, skipped=1, unknown=1", error.ToString ());
		foreach (var outcome in new [] { "passed", "failed", "skipped", "unexpected" })
			Assert.Contains ($"[{outcome}] Tests.{outcome}", error.ToString ());
		Assert.Contains ("[unfinished] Tests.ActiveOne", error.ToString ());
		Assert.Contains ("not necessarily the cause", error.ToString ());
		Assert.IsEmpty (state.ReportedFinal, "Observation must not depend on MTP publication.");
	}

	[TestMethod]
	public void ParserObservesCompleteBlocksImmediatelyButNeverInventsACompletion ()
	{
		var state = new StreamingState ();
		var parser = new StatusStreamParser (state.Observe, new StringBuilder (), verbose: false);
		parser.OnLine ("INSTRUMENTATION_STATUS: event=start");
		parser.OnLine ("INSTRUMENTATION_STATUS: test=Tests.One");
		parser.OnLine ("INSTRUMENTATION_STATUS_CODE: 1");
		Assert.IsTrue (state.HasUnfinishedTests, "Observed before the instrumentation process exits.");
		parser.OnLine ("INSTRUMENTATION_STATUS: event=finish");
		parser.OnLine ("INSTRUMENTATION_STATUS: test=Tests.One");
		parser.OnLine ("INSTRUMENTATION_STATUS: outcome=passed");
		parser.Complete ();
		state.WritePartialResults ();
		Assert.Contains ("passed=0", error.ToString ());
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

	[TestMethod]
	[DataRow (false, false)]
	[DataRow (false, true)]
	[DataRow (true, false)]
	[DataRow (true, true)]
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
			await Assert.ThrowsExactlyAsync<UnauthorizedAccessException> (async () => { await application.RunAsync (); });
			Assert.IsFalse (File.Exists (Path.Combine (directory, "results.trx")));
		} else {
			Assert.AreEqual (!crash, await application.RunAsync () == 0);
			var trx = XDocument.Load (Path.Combine (directory, "results.trx"));
			XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
			var results = trx.Descendants (ns + "UnitTestResult").ToArray ();
			Assert.HasCount (2, results);
			var preceding = results.Single (e => e.Attribute ("testName")?.Value == "Tests.One");
			var last = results.Single (e => e.Attribute ("testName")?.Value == "Tests.Two");
			Assert.AreEqual ("Passed", preceding.Attribute ("outcome")?.Value);
			Assert.AreEqual (crash ? "Failed" : "Passed", last.Attribute ("outcome")?.Value);
			if (crash) {
				var message = last.Element (ns + "Output")?.Element (ns + "ErrorInfo")?.Element (ns + "Message")?.Value;
				Assert.AreEqual ("This was the last observed active test, not necessarily the cause of the incomplete run.\nThe test process crashed: Process crashed.", message);
			}
		}
		if (crash) {
			Assert.Contains ("Incomplete test run", error.ToString ());
			Assert.Contains ("[passed] Tests.One", error.ToString ());
		} else {
			Assert.DoesNotContain ("Incomplete test run", error.ToString ());
		}
	}

	[TestMethod]
	[DataRow ("", 0)]
	[DataRow ("error: device offline", 1)]
	[DataRow (Progress, 0)]
	public async Task MissingBundleOrTransportFailureIsNotLabeledACrash (string transcript, int exitCode)
	{
		using var application = await CreateApplication (transcript, exitCode: exitCode);
		if (exitCode == 0 && transcript.Length == 0)
			await Assert.ThrowsExactlyAsync<InvalidOperationException> (async () => { await application.RunAsync (); });
		else
			Assert.AreNotEqual (0, await application.RunAsync ());
		Assert.Contains ("Incomplete test run", error.ToString ());
		Assert.Contains (transcript == Progress ? "passed=1" : "passed=0", error.ToString ());
		Assert.DoesNotContain ("process crashed", error.ToString ());
	}

	[TestMethod]
	public async Task ProcessStartupExceptionStillPrintsPartialSummary ()
	{
		using var application = await CreateApplication ("", missingAdb: true);
		await Assert.ThrowsExactlyAsync<System.ComponentModel.Win32Exception> (async () => { await application.RunAsync (); });
		Assert.Contains ("Incomplete test run", error.ToString ());
	}

	[TestMethod]
	[DataRow (false)]
	[DataRow (true)]
	public async Task UnfinishedTestIsSavedAsFailedEvenWhenAdbExitsSuccessfully (bool finalBundle)
	{
		string transcript = """
			INSTRUMENTATION_STATUS: event=start
			INSTRUMENTATION_STATUS: test=Tests.One
			INSTRUMENTATION_STATUS: class=Tests
			INSTRUMENTATION_STATUS_CODE: 1
			""" + (finalBundle ? "\nINSTRUMENTATION_RESULT: resultsPath=/device/results.trx\nINSTRUMENTATION_CODE: -1" : "");
		using var application = await CreateApplication (transcript);
		Assert.AreNotEqual (0, await application.RunAsync ());
		XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
		var result = XDocument.Load (Path.Combine (directory, "results.trx")).Descendants (ns + "UnitTestResult").Single ();
		Assert.AreEqual ("Tests.One", result.Attribute ("testName")?.Value);
		Assert.AreEqual ("Failed", result.Attribute ("outcome")?.Value);
		Assert.AreEqual ("This was the last observed active test, not necessarily the cause of the incomplete run.\nThe test run ended before all tests completed.", result.Element (ns + "Output")?.Element (ns + "ErrorInfo")?.Element (ns + "Message")?.Value);
		Assert.Contains ("passed=0", error.ToString ());
		Assert.DoesNotContain ("process crashed", error.ToString ());
	}

	[TestMethod]
	public async Task CancellationAfterProgressPreservesPartialResults ()
	{
		using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (30));
		var adapter = new CancellableAdapter (Path.Combine (directory, "fake-adb"), cancellation.Token);
		using var application = await CreateApplication (Progress, cancellationAdapter: adapter);
		var run = application.RunAsync ();
		try {
			await adapter.ProgressObserved.Task.WaitAsync (TimeSpan.FromSeconds (15));
			Assert.IsFalse (run.IsCompleted, "Cancel only after progress from a still-running invocation.");
		} finally {
			cancellation.Cancel ();
			await Assert.ThrowsAsync<OperationCanceledException> (async () => { await run.WaitAsync (TimeSpan.FromSeconds (15)); });
		}
		Assert.Contains ("Incomplete test run", error.ToString ());
		Assert.Contains ("[passed] Tests.One", error.ToString ());
	}

	async Task<ITestApplication> CreateApplication (string transcript, bool reportFailure = false, int exitCode = 0, bool missingAdb = false,
		CancellableAdapter? cancellationAdapter = null)
	{
		if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows))
			Assert.Inconclusive ("The fake adb is a Unix shell executable.");
		string adb = Path.Combine (directory, "fake-adb");
		var ending = cancellationAdapter == null ? $"exit {exitCode}" : "exec sleep 30";
		File.WriteAllText (adb, $"#!/bin/sh\ncat <<'TRANSCRIPT'\n{transcript}\nTRANSCRIPT\n{ending}\n");
		if (!OperatingSystem.IsWindows ())
			File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		if (reportFailure)
			Directory.CreateDirectory (Path.Combine (directory, "results.trx"));
		var builder = await TestApplication.CreateBuilderAsync ([
			"--report-trx", "--report-trx-filename", "results.trx", "--results-directory", directory,
		]);
		AndroidTestAdapter adapter = cancellationAdapter ?? new AndroidTestAdapter (missingAdb ? Path.Combine (directory, "missing-adb") : adb, null, "tests", "runner", false);
		builder.RegisterTestFramework (_ => new AndroidTestCapabilities (), (_, _) => adapter);
		builder.AddTrxReportProvider ();
		return await builder.BuildAsync ();
	}

#pragma warning disable TPEXP // The pinned MTP marks request construction as experimental; inject cancellation only in tests.
	sealed class CancellableAdapter (string adb, CancellationToken cancellationToken) :
		AndroidTestAdapter (adb, null, "tests", "runner", false), ITestFramework, IMessageBus, IExecuteRequestCompletionNotifier
	{
		ExecuteRequestContext? original;
		public TaskCompletionSource ProgressObserved { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);

		Task ITestFramework.ExecuteRequestAsync (ExecuteRequestContext context)
		{
			original = context;
			return base.ExecuteRequestAsync (new ExecuteRequestContext (context.Request, this, this, cancellationToken));
		}

		void IExecuteRequestCompletionNotifier.Complete () =>
			(original ?? throw new InvalidOperationException ("No active execution request.")).Complete ();

		public async Task PublishAsync (IDataProducer dataProducer, IData data)
		{
			var context = original ?? throw new InvalidOperationException ("No active execution request.");
			await context.MessageBus.PublishAsync (dataProducer, data);
			if (data is TestNodeUpdateMessage update && update.TestNode.DisplayName == "Tests.Two")
				ProgressObserved.TrySetResult ();
		}
	}
#pragma warning restore TPEXP
}
