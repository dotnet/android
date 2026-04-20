using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Util;

using NUnitLite.Runner;
using NUnit.Framework.Api;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.WorkItems;
using NUnit.Framework.Internal.Filters;

using NUnitTest = NUnit.Framework.Internal.Test;

namespace Xamarin.Android.UnitTests.NUnit
{
	public class NUnitTestRunner : TestRunner, ITestListener
	{
		const string DryRunSkipReason = "Dry run: discovery only.";

		Dictionary<string, object> builderSettings;
		TestSuiteResult results;
		readonly Dictionary<string, string> excludedCategories = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<string, string> excludedTestNames = new Dictionary<string, string> (StringComparer.Ordinal);

		public ITestFilter Filter { get; set; } = TestFilter.Empty;
		public bool GCAfterEachFixture { get; set; }

		protected override string ResultsFileName { get; set; } = "TestResults.NUnit.xml";

		public NUnitTestRunner (Context context, LogWriter logger, Bundle bundle) : base (context, logger, bundle)
		{
			builderSettings = new Dictionary<string, object> (StringComparer.OrdinalIgnoreCase);
		}

		public void AddExcludedCategory (string category, string reason)
		{
			if (String.IsNullOrEmpty (category)) {
				return;
			}

			excludedCategories [category] = reason;
		}

		public void AddExcludedTestName (string testName, string reason)
		{
			if (String.IsNullOrEmpty (testName)) {
				return;
			}

			excludedTestNames [testName] = reason;
		}

		public override void Run (IList<TestAssemblyInfo> testAssemblies)
		{
			if (testAssemblies == null)
				throw new ArgumentNullException (nameof (testAssemblies));
			
			var builder = new NUnitLiteTestAssemblyBuilder ();
			var runner = new NUnitLiteTestAssemblyRunner (builder);
			var testSuite = new TestSuite (global::Android.App.Application.Context.PackageName);
			results = new TestSuiteResult (testSuite);

			foreach (TestAssemblyInfo assemblyInfo in testAssemblies) {
				if (assemblyInfo == null || assemblyInfo.Assembly == null)
					continue;
				
				if (!runner.Load (assemblyInfo.Assembly, builderSettings)) {
					OnWarning ($"Failed to load tests from assembly '{assemblyInfo.Assembly}");
					continue;
				}
				if (runner.LoadedTest is NUnitTest tests) {
					testSuite.Add (tests);
					ApplyIgnoredExclusions (tests);
					UpdateDiscoveredTestCounts (tests);
					if (DryRun) {
						ApplyDryRunToMatchingTests (tests);
					}
				}
				
				// Messy API. .Run returns ITestResult which is, in reality, an instance of TestResult since that's
				// what WorkItem returns and we need an instance of TestResult to add it to TestSuiteResult. So, cast
				// the return to TestResult and hope for the best.
				ITestResult result = null;
				try {
					OnAssemblyStart (assemblyInfo.Assembly);
					result = runner.Run (this, Filter);
				} finally {
					OnAssemblyFinish (assemblyInfo.Assembly);
				}

				if (result == null)
					continue;

				var testResult = result as TestResult;
				if (testResult == null)
					throw new InvalidOperationException ($"Unexpected test result type '{result.GetType ()}'");
				results.AddResult (testResult);
				UpdateSummaryCounts ();
			}

			LogFailureSummary ();
		}

		void UpdateSummaryCounts ()
		{
			if (results == null) {
				return;
			}

			PassedTests = results.PassCount;
			FailedTests = results.FailCount;
			SkippedTests = results.SkipCount;
			InconclusiveTests = results.InconclusiveCount;
			ExecutedTests = PassedTests + FailedTests + SkippedTests + InconclusiveTests;
		}

		void ApplyIgnoredExclusions (NUnitTest test)
		{
			if (test.RunState != RunState.Runnable && test.RunState != RunState.Explicit) {
				return;
			}

			if (!TryGetSkipReason (test, out string reason)) {
				if (test is TestSuite suite) {
					foreach (NUnitTest child in suite.Tests) {
						ApplyIgnoredExclusions (child);
					}
				}
				return;
			}

			test.RunState = RunState.Ignored;
			test.Properties.Set (PropertyNames.SkipReason, reason);
		}

		void UpdateDiscoveredTestCounts (NUnitTest test)
		{
			if (test is TestSuite suite) {
				foreach (NUnitTest child in suite.Tests) {
					UpdateDiscoveredTestCounts (child);
				}
				return;
			}

			TotalTests++;
			if (Filter == null || Filter.IsEmpty || Filter.Pass (test)) {
				FilteredTests++;
			}
		}

		void ApplyDryRunToMatchingTests (NUnitTest test)
		{
			if (test is TestSuite suite) {
				foreach (NUnitTest child in suite.Tests) {
					ApplyDryRunToMatchingTests (child);
				}
				return;
			}

			if (Filter != null && !Filter.IsEmpty && !Filter.Pass (test)) {
				return;
			}

			if (test.RunState == RunState.Runnable || test.RunState == RunState.Explicit) {
				test.RunState = RunState.Ignored;
				test.Properties.Set (PropertyNames.SkipReason, DryRunSkipReason);
			}

			string reason = test.Properties.Get (PropertyNames.SkipReason) as string;
			if (String.IsNullOrEmpty (reason)) {
				Logger.OnInfo (LogTag, $"[DRY-RUN] {test.FullName}");
			} else {
				Logger.OnInfo (LogTag, $"[DRY-RUN] {test.FullName} [{reason}]");
			}
		}

		bool TryGetSkipReason (NUnitTest test, out string reason)
		{
			if (TryGetNamedSkipReason (test, out reason)) {
				return true;
			}

			return TryGetCategorySkipReason (test, out reason);
		}

		bool TryGetNamedSkipReason (NUnitTest test, out string reason)
		{
			foreach (var kvp in excludedTestNames) {
				if (TestNameMatches (test.FullName, kvp.Key)) {
					reason = kvp.Value;
					return true;
				}
			}

			reason = String.Empty;
			return false;
		}

		static bool TestNameMatches (string fullName, string excludedName)
		{
			if (String.IsNullOrEmpty (fullName) || String.IsNullOrEmpty (excludedName)) {
				return false;
			}

			if (fullName == excludedName ||
				fullName.StartsWith (excludedName + ".", StringComparison.Ordinal) ||
				fullName.StartsWith (excludedName + "+", StringComparison.Ordinal) ||
				fullName.Contains ("." + excludedName + ".", StringComparison.Ordinal) ||
				fullName.Contains ("." + excludedName + "+", StringComparison.Ordinal) ||
				fullName.Contains (", " + excludedName, StringComparison.Ordinal)) {
				return true;
			}

			return false;
		}

		bool TryGetCategorySkipReason (NUnitTest test, out string reason)
		{
			if (test.Properties [PropertyNames.Category] is IList categories) {
				foreach (object value in categories) {
					if (value is string category && excludedCategories.TryGetValue (category, out reason)) {
						return true;
					}
				}
			}

			reason = String.Empty;
			return false;
		}

		public bool Pass (ITest test)
		{
			return true;
		}

		public void TestFinished (ITestResult result)
		{
			if (result.Test is TestSuite) {
				//if (!result.IsError && !result.IsFailure && !result.IsSuccess && !result.Executed)
				//Writer.WriteLine ("\t[INFO] {0}", result.Message);
				if (result.ResultState.Status != TestStatus.Failed &&
					result.ResultState.Status != TestStatus.Skipped &&
					result.ResultState.Status != TestStatus.Passed &&
					result.ResultState.Status != TestStatus.Inconclusive) {
						Logger.OnInfo ("\t[INFO] {0}", result.Message);
				}

				Logger.OnInfo (LogTag, $"{result.Test.FullName} : {result.Duration.TotalMilliseconds} ms");
				if (GCAfterEachFixture)
					GC.Collect ();
			} else {
				Action<string, string> log = Logger.OnInfo;
				StringBuilder failedMessage = null;

				if (result.ResultState.Status == TestStatus.Passed) {
					Logger.OnInfo (LogTag, $"\t{result.ResultState.ToString ()}");
				} else if (result.ResultState.Status == TestStatus.Failed) {
					Logger.OnError (LogTag, "\t[FAIL]");
					log = Logger.OnError;
					failedMessage = new StringBuilder ();
					failedMessage.Append (result.Test.FullName);
					if (result.Test.FixtureType != null)
						failedMessage.Append ($" ({result.Test.FixtureType.Assembly.GetName ().Name})");
					failedMessage.AppendLine ();
				} else {
					string status = result.ResultState.Status switch {
						TestStatus.Skipped => "SKIPPED",
						TestStatus.Inconclusive => "INCONCLUSIVE",
						_ => "UNKNOWN",
					};
					Logger.OnInfo (LogTag, $"\t[{status}]");
				}

				string message = result.Message?.Replace ("\r\n", "\\r\\n");
				if (!String.IsNullOrEmpty (message)) {
					log (LogTag, $" : {message}");
					if (failedMessage != null)
						failedMessage.AppendLine (message);
				}

				string stacktrace = result.StackTrace;
				if (!String.IsNullOrEmpty (result.StackTrace)) {
					log (LogTag, result.StackTrace);
					if (failedMessage != null) {
						failedMessage.AppendLine ();
						failedMessage.AppendLine (result.StackTrace);
					}
				}

				if (failedMessage != null) {
					FailureInfos.Add (new TestFailureInfo {
						TestName = result.Test.FullName,
						Message = failedMessage.ToString ()
					});
				}
			}
		}

		public void TestOutput (TestOutput testOutput)
		{
			if (testOutput == null || String.IsNullOrEmpty (testOutput.Text))
				return;
			
			string kind = testOutput.Type.ToString ();
			foreach (string l in testOutput.Text.Split ('\n')) {
				Logger.OnInfo (LogTag, $"  {kind}: {l}");
			}
		}

		public void TestStarted (ITest test)
		{
			if (test == null)
				return;

			if (!String.IsNullOrEmpty (TestsRootDirectory))
				System.Environment.CurrentDirectory = TestsRootDirectory;

			if (test is TestSuite) {
				Logger.OnInfo (LogTag, test.Name);
			} else
				Logger.OnInfo (LogTag, $"{test.Name} ");
		}

		public override string WriteResultsToFile ()
		{
			if (results == null)
				return String.Empty;

			string ret = GetResultsFilePath ();
			if (String.IsNullOrEmpty (ret))
				return String.Empty;
			
			var resultsXml = new NUnit2XmlOutputWriter (DateTime.UtcNow);
			resultsXml.WriteResultFile (results, ret);

			return ret;
		}
	}
}
