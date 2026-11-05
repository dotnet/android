using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace NativeMtpProbe;

[TestClass]
public class ContractTests
{
	public TestContext TestContext { get; set; }

	[TestMethod]
	[TestCategory ("sourcegen-contract")]
	public void GeneratedMetadataActive ()
	{
		RecordExecution ();
		var assembly = Assembly.Load ("MSTestAdapter.PlatformServices");
		var providerType = assembly.GetType ("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.PlatformServiceProvider", throwOnError: true);
		Assert.IsNotNull (providerType);
		var instanceProperty = providerType.GetProperty ("Instance", BindingFlags.Static | BindingFlags.NonPublic);
		Assert.IsNotNull (instanceProperty);
		var provider = instanceProperty.GetValue (null);
		Assert.IsNotNull (provider);
		var operationsProperty = providerType.GetProperty ("ReflectionOperations");
		Assert.IsNotNull (operationsProperty);
		var operations = operationsProperty.GetValue (provider);
		Assert.IsNotNull (operations);
		Assert.AreEqual ("SourceGeneratedReflectionOperations", operations.GetType ().Name);

		var classes = MSTest.SourceGenerated.MSTestReflectionMetadata.TestClasses;
		Assert.AreEqual (1, classes.Count);
		var generated = classes [0];
		Assert.AreEqual (typeof (ContractTests), generated.Type);
		var expected = typeof (ContractTests).GetMethods ()
			.Where (method => method.GetCustomAttribute<TestMethodAttribute> () != null)
			.Select (method => method.Name).OrderBy (name => name).ToArray ();
		var actual = generated.Methods.Where (method => method.IsTestMethod)
			.Select (method => method.Name).OrderBy (name => name).ToArray ();
		CollectionAssert.AreEqual (expected, actual);
		Assert.IsTrue (generated.Methods.All (method => method.Invoke != null));
		Assert.AreEqual (2, generated.Methods.Sum (method => method.DynamicDataSources.Count));
		Console.WriteLine ("active provider: " + operations.GetType ().FullName);
		Console.WriteLine ("generated test methods: " + string.Join (",", actual));
	}

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

	public static IEnumerable<object []> DynamicCases {
		get {
			yield return ["duplicate"];
			yield return ["duplicate"];
			yield return ["different"];
			yield return ["a&b|(c)=!~\\\"; percent%2C comma, space"];
		}
	}

	public static IEnumerable<object []> DynamicRetryCases {
		get {
			yield return [0];
			yield return [1];
		}
	}

	public static string DynamicDisplayName (MethodInfo method, object [] data)
	{
		return method.Name == nameof (DynamicRows) ? "dynamic duplicate display" : "dynamic retry display";
	}

	[TestMethod]
	[TestCategory ("dynamic-selection")]
	[DynamicData (nameof (DynamicCases), DynamicDataDisplayName = nameof (DynamicDisplayName))]
	public void DynamicRows (string value)
	{
		RecordExecution ();
		Assert.IsFalse (string.IsNullOrEmpty (value));
		Console.WriteLine ("dynamic row value: " + value);
	}

	[TestMethod]
	[TestCategory ("dynamic-retry")]
	[DynamicData (nameof (DynamicRetryCases), DynamicDataDisplayName = nameof (DynamicDisplayName))]
	public void DynamicRetryRows (int row)
	{
		RecordExecution ();
		if (row != 0)
			return;
		var directory = Environment.GetEnvironmentVariable ("MTP_PROBE_STATE");
		Assert.IsFalse (string.IsNullOrEmpty (directory));
		var marker = Path.Combine (directory, "dynamic-retry-marker");
		bool first = !File.Exists (marker);
		File.WriteAllText (marker, "attempted");
		Assert.IsFalse (first, "deliberate first-dynamic-row failure");
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
	[TestCategory ("child-timeout")]
	public void SpawnChildAndWait ()
	{
		RecordExecution ();
		var executable = Environment.GetEnvironmentVariable ("MTP_PROBE_CHILD_EXECUTABLE");
		var script = Environment.GetEnvironmentVariable ("MTP_PROBE_CHILD_SCRIPT");
		var state = Environment.GetEnvironmentVariable ("MTP_PROBE_STATE");
		Assert.IsFalse (string.IsNullOrEmpty (executable));
		Assert.IsFalse (string.IsNullOrEmpty (script));
		Assert.IsFalse (string.IsNullOrEmpty (state));
		var start = new ProcessStartInfo (executable) { UseShellExecute = false };
		start.ArgumentList.Add (script);
		start.ArgumentList.Add (state);
		start.ArgumentList.Add ("child");
		using var child = Process.Start (start);
		Assert.IsNotNull (child);
		child.WaitForExit ();
		Assert.Fail ("timeout child exited before the native test process was stopped");
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
