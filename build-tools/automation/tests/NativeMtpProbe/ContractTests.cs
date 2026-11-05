using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace NativeMtpProbe;

[TestClass]
public class ContractTests
{
	public TestContext TestContext { get; set; }

	[TestMethod]
	[TestCategory ("selection")]
	public void Ordinary ()
	{
		RecordExecution ();
	}

	[TestMethod]
	[TestCategory ("selection")]
	[DataRow ("a&b|(c)=!~\\\"; percent%2C comma, space", DisplayName = "same display")]
	[DataRow ("different", DisplayName = "same display")]
	[DataRow ("escaped", DisplayName = "a&b|(c)=!~\\\"; percent%2C comma, space")]
	[DataRow ("duplicate", DisplayName = "duplicate data")]
	[DataRow ("duplicate", DisplayName = "duplicate data")]
	public void Rows (string value)
	{
		RecordExecution ();
		Assert.IsFalse (string.IsNullOrEmpty (value));
		Console.WriteLine ("row value: " + value);
	}

	[TestMethod]
	[TestCategory ("escaped&category")]
	public void EscapedCategory ()
	{
		RecordExecution ();
	}

	[TestMethod]
	[TestCategory ("retry")]
	public void FailsOnce ()
	{
		RecordExecution ();
		var directory = Environment.GetEnvironmentVariable ("MTP_PROBE_STATE");
		Assert.IsFalse (string.IsNullOrEmpty (directory));
		var marker = Path.Combine (directory, "retry-marker");
		bool first = !File.Exists (marker);
		File.WriteAllText (marker, "attempted");
		Assert.IsFalse (first, "deliberate first-attempt failure");
	}

	[TestMethod]
	[TestCategory ("data-retry")]
	[DataRow (0, DisplayName = "retry duplicate display")]
	[DataRow (1, DisplayName = "retry duplicate display")]
	public void RetryRows (int row)
	{
		RecordExecution ();
		if (row != 0)
			return;
		var directory = Environment.GetEnvironmentVariable ("MTP_PROBE_STATE");
		Assert.IsFalse (string.IsNullOrEmpty (directory));
		var marker = Path.Combine (directory, "retry-row-marker");
		bool first = !File.Exists (marker);
		File.WriteAllText (marker, "attempted");
		Assert.IsFalse (first, "deliberate first-row failure");
	}

	[TestMethod]
	[TestCategory ("failure")]
	public void AlwaysFails ()
	{
		RecordExecution ();
		Assert.Fail ("deliberate terminal failure");
	}

	[TestMethod]
	[TestCategory ("infrastructure")]
	public void Crashes ()
	{
		Environment.Exit (7);
	}

	[TestMethod]
	[TestCategory ("retry-infrastructure")]
	public void FailsThenCrashes ()
	{
		RecordExecution ();
		var directory = Environment.GetEnvironmentVariable ("MTP_PROBE_STATE");
		Assert.IsFalse (string.IsNullOrEmpty (directory));
		var marker = Path.Combine (directory, "crash-marker");
		if (File.Exists (marker))
			Environment.Exit (7);
		File.WriteAllText (marker, "attempted");
		Assert.Fail ("deliberate failure before retry crash");
	}

	[TestMethod]
	[TestCategory ("timeout")]
	public void Sleeps ()
	{
		System.Threading.Thread.Sleep (30_000);
	}

	[TestMethod]
	[TestCategory ("skipped")]
	[Ignore ("selection contract")]
	public void Skipped ()
	{
		Assert.Fail ("ignored test executed");
	}

	void RecordExecution ()
	{
		var directory = Environment.GetEnvironmentVariable ("MTP_PROBE_STATE");
		if (!string.IsNullOrEmpty (directory)) {
			Directory.CreateDirectory (directory);
			File.AppendAllText (Path.Combine (directory, "executions.txt"), TestContext.TestName + Environment.NewLine);
			var attachment = Path.Combine (directory, Guid.NewGuid () + ".txt");
			File.WriteAllText (attachment, TestContext.TestName);
			TestContext.AddResultFile (attachment);
		}
		Console.WriteLine ("probe output: " + TestContext.TestName);
	}
}
