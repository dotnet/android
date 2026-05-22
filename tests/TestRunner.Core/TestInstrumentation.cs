using System.Reflection;
using System.Xml.Linq;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Xamarin.Android.UnitTests;

/// <summary>
/// Base instrumentation class that runs NUnit tests on device using
/// NUnit's NUnitTestAssemblyRunner API and generates TRX results for
/// Microsoft.Android.Run to pull and report through MTP.
/// </summary>
public abstract class TestInstrumentation : Instrumentation
{
	const string LogTag = "TestInstrumentation";

	protected TestInstrumentation (IntPtr handle, JniHandleOwnership ownership)
		: base (handle, ownership) { }

	/// <summary>
	/// Override to return categories that should be excluded from test runs.
	/// </summary>
	protected virtual IEnumerable<string>? ExcludedCategories => null;

	/// <summary>
	/// Override to return fully-qualified test names that should be excluded.
	/// Useful for skipping tests from submodules (e.g. Java.Interop) where
	/// adding attributes is not practical.
	/// </summary>
	protected virtual IEnumerable<string>? ExcludedTestNames => null;

	Bundle? instrumentationArguments;

	public override void OnCreate (Bundle? arguments)
	{
		instrumentationArguments = arguments;
		base.OnCreate (arguments);
		Start ();
	}

	/// <summary>
	/// Override to preload native libraries before tests run.
	/// Called on the instrumentation thread which has a valid Java ClassLoader.
	/// </summary>
	protected virtual void PreloadNativeLibraries ()
	{
	}

	public override void OnStart ()
	{
		base.OnStart ();

		var bundle = new Bundle ();
		try {
			try {
				PreloadNativeLibraries ();
			} catch (Exception ex) {
				Log.Warn (LogTag, $"PreloadNativeLibraries failed (continuing anyway): {ex}");
			}

			var writeablePath = Application.Context.GetExternalFilesDir (null)?.AbsolutePath ?? Path.GetTempPath ();
			var resultsDir = Path.Combine (writeablePath, "TestResults");
			Directory.CreateDirectory (resultsDir);

			var filter = BuildNUnitFilter ();
			int passed = 0, failed = 0, skipped = 0;
			var allResults = new List<ITestResult> ();
			var listener = new TestListener (this);

			foreach (var assembly in GetTestAssemblies ()) {
				Log.Info (LogTag, $"Loading tests from: {assembly.GetName ().Name}");
				var runner = new NUnitTestAssemblyRunner (new AndroidTestAssemblyBuilder ());
				runner.Load (assembly, new Dictionary<string, object> ());

				var result = runner.Run (listener, filter);
				CountResults (result, ref passed, ref failed, ref skipped);
				allResults.Add (result);
			}

			var trxPath = Path.Combine (resultsDir, "TestResults.trx");
			WriteTrxFile (trxPath, allResults);
			Log.Info (LogTag, $"TRX written to: {trxPath}");
			Log.Info (LogTag, $"Results: passed={passed}, failed={failed}, skipped={skipped}");

			bundle.PutInt ("passed", passed);
			bundle.PutInt ("failed", failed);
			bundle.PutInt ("skipped", skipped);
			bundle.PutString ("resultsPath", trxPath);
			Finish (Result.Ok, bundle);
		} catch (Exception ex) {
			Log.Error (LogTag, $"Test run failed: {ex}");
			bundle.PutString ("error", ex.ToString ());
			Finish (Result.Canceled, bundle);
		}
	}

	/// <summary>
	/// Override to return the assemblies containing NUnit tests to run.
	/// </summary>
	protected abstract IEnumerable<Assembly> GetTestAssemblies ();

	/// <summary>
	/// Builds an NUnit TestFilter from excluded categories, excluded test names,
	/// and instrumentation extras (include/exclude).
	/// </summary>
	TestFilter BuildNUnitFilter ()
	{
		bool noExclusions = GetBoolExtra ("noexclusions");
		var filterElements = new List<XElement> ();

		// Include categories from extras: am instrument -e include "Cat1,Cat2"
		var includeExtras = GetListExtra ("include");
		if (includeExtras.Count > 0) {
			var orElement = new XElement ("or");
			foreach (var cat in includeExtras) {
				orElement.Add (new XElement ("cat", cat));
				Log.Info (LogTag, $"Including category: {cat}");
			}
			filterElements.Add (includeExtras.Count == 1 ? orElement.Elements ().First () : orElement);
		}

		if (!noExclusions) {
			if (ExcludedCategories is not null) {
				foreach (var cat in ExcludedCategories) {
					filterElements.Add (new XElement ("not", new XElement ("cat", cat)));
					Log.Info (LogTag, $"Excluding category: {cat}");
				}
			}

			if (ExcludedTestNames is not null) {
				foreach (var name in ExcludedTestNames) {
					filterElements.Add (new XElement ("not", new XElement ("test", name)));
					Log.Info (LogTag, $"Excluding test: {name}");
				}
			}
		} else {
			Log.Info (LogTag, "Skipping built-in exclusions due to noexclusions=true");
		}

		// Exclude categories from extras: am instrument -e exclude "Cat1,Cat2"
		var excludeExtras = GetListExtra ("exclude");
		foreach (var cat in excludeExtras) {
			filterElements.Add (new XElement ("not", new XElement ("cat", cat)));
			Log.Info (LogTag, $"Excluding category (from extras): {cat}");
		}

		if (filterElements.Count == 0)
			return TestFilter.Empty;

		// Wrap in <filter><and>...</and></filter> for multiple conditions
		XElement filterXml;
		if (filterElements.Count == 1) {
			filterXml = new XElement ("filter", filterElements [0]);
		} else {
			var andElement = new XElement ("and");
			foreach (var el in filterElements) {
				andElement.Add (el);
			}
			filterXml = new XElement ("filter", andElement);
		}

		var xmlStr = filterXml.ToString ();
		Log.Info (LogTag, $"NUnit filter XML: {xmlStr}");
		return TestFilter.FromXml (xmlStr);
	}

	string? GetStringExtra (string key)
	{
		if (instrumentationArguments is null)
			return null;
		return instrumentationArguments.GetString (key);
	}

	bool GetBoolExtra (string key)
	{
		var value = GetStringExtra (key);
		if (value is null)
			return false;
		return string.Equals (value.Trim (), "true", StringComparison.OrdinalIgnoreCase);
	}

	List<string> GetListExtra (string key)
	{
		var value = GetStringExtra (key);
		if (value is null)
			return [];
		return value.Split ([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList ();
	}

	static void CountResults (ITestResult result, ref int passed, ref int failed, ref int skipped)
	{
		if (result.Test.IsSuite) {
			foreach (var child in result.Children) {
				CountResults (child, ref passed, ref failed, ref skipped);
			}
		} else {
			switch (result.ResultState.Status) {
			case TestStatus.Passed:
				passed++;
				break;
			case TestStatus.Failed:
				failed++;
				break;
			default:
				skipped++;
				break;
			}
		}
	}

	/// <summary>
	/// Collects individual (non-suite) test results from the result tree.
	/// </summary>
	static void CollectTestResults (ITestResult result, List<ITestResult> results)
	{
		if (result.Test.IsSuite) {
			foreach (var child in result.Children) {
				CollectTestResults (child, results);
			}
		} else {
			results.Add (result);
		}
	}

	/// <summary>
	/// Writes test results in TRX format that AndroidTestAdapter can parse.
	/// </summary>
	static void WriteTrxFile (string path, List<ITestResult> assemblyResults)
	{
		var ns = XNamespace.Get ("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");
		var allTests = new List<ITestResult> ();
		foreach (var ar in assemblyResults) {
			CollectTestResults (ar, allTests);
		}

		var runId = Guid.NewGuid ().ToString ();
		var testDefinitions = new XElement (ns + "TestDefinitions");
		var results = new XElement (ns + "Results");

		foreach (var test in allTests) {
			var testId = Guid.NewGuid ().ToString ();
			var className = test.Test.ClassName ?? "";
			var testName = test.Test.Name;
			var outcome = test.ResultState.Status switch {
				TestStatus.Passed => "Passed",
				TestStatus.Failed => "Failed",
				_ => "NotExecuted",
			};

			testDefinitions.Add (new XElement (ns + "UnitTest",
				new XAttribute ("id", testId),
				new XAttribute ("name", testName),
				new XElement (ns + "TestMethod",
					new XAttribute ("className", className),
					new XAttribute ("name", testName))));

			var unitTestResult = new XElement (ns + "UnitTestResult",
				new XAttribute ("testId", testId),
				new XAttribute ("testName", testName),
				new XAttribute ("outcome", outcome));

			if (test.ResultState.Status == TestStatus.Failed && test.Message is not null) {
				unitTestResult.Add (new XElement (ns + "Output",
					new XElement (ns + "ErrorInfo",
						new XElement (ns + "Message", test.Message),
						new XElement (ns + "StackTrace", test.StackTrace ?? ""))));
			}

			results.Add (unitTestResult);
		}

		var doc = new XDocument (
			new XElement (ns + "TestRun",
				new XAttribute ("id", runId),
				results,
				testDefinitions));

		doc.Save (path);
	}

	/// <summary>
	/// Sends test status updates through the instrumentation protocol.
	/// </summary>
	class TestListener (Instrumentation instrumentation) : ITestListener
	{
		public void TestStarted (ITest test)
		{
			if (test.IsSuite)
				return;
			Log.Info (LogTag, $"[START] {test.FullName}");
		}

		public void TestFinished (ITestResult result)
		{
			if (result.Test.IsSuite)
				return;

			var outcome = result.ResultState.Status switch {
				TestStatus.Passed => "passed",
				TestStatus.Failed => "failed",
				_ => "skipped",
			};

			Log.Info (LogTag, $"[{outcome.ToUpperInvariant ()}] {result.FullName}");

			var b = new Bundle ();
			b.PutString ("test", result.FullName);
			b.PutString ("outcome", outcome);
			instrumentation.SendStatus (0, b);
		}

		public void TestOutput (TestOutput output) { }
		public void SendMessage (TestMessage message) { }
	}

	/// <summary>
	/// Custom ITestAssemblyBuilder that avoids calling Assembly.CodeBase.
	/// NUnit 3.13.3's DefaultTestAssemblyBuilder.Build(Assembly, ...) calls
	/// AssemblyHelper.GetAssemblyPath() which accesses Assembly.CodeBase —
	/// this throws NotSupportedException on .NET Android single-file bundles.
	/// This builder redirects through the string overload using the assembly's
	/// simple name, which NUnit resolves via Assembly.Load(AssemblyName).
	/// </summary>
	class AndroidTestAssemblyBuilder : ITestAssemblyBuilder
	{
		readonly DefaultTestAssemblyBuilder inner = new ();

		public ITest Build (Assembly assembly, IDictionary<string, object> options)
			=> inner.Build (assembly.GetName ().Name!, options);

		public ITest Build (string assemblyNameOrPath, IDictionary<string, object> options)
			=> inner.Build (assemblyNameOrPath, options);
	}
}
