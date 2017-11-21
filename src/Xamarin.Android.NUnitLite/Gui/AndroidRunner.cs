//
// Copyright 2011-2012 Xamarin Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;

using Android.Content;
using Android.OS;
using Android.Util;
using Android.Widget;

using NUnitLite;
using NUnit.Framework.Api;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.WorkItems;
using NUnit.Framework.Internal.Filters;
using System.Reflection;
using System.Collections;

using NUnitTest = NUnit.Framework.Internal.Test;

namespace Xamarin.Android.NUnitLite
{
	internal class AndroidRunner : ITestListener, ITestFilter
	{
		const string TAG = "NUnitLite";

		Options options;
		NUnitLiteTestAssemblyBuilder builder = new NUnitLiteTestAssemblyBuilder ();
		Dictionary<string, object> empty = new Dictionary<string, object> ();
		int passed, failed, skipped, inconclusive;

		public List<Assembly> Assemblies = new List<Assembly> ();

		public AndroidRunner ()
		{
		}

		public bool AutoStart { get; set; }

		public bool TerminateAfterExecution { get; set; }

		public Options Options {
			get {
				if (options == null)
					options = new Options ();
				return options;
			}
			set { options = value; }
		}

		public bool GCAfterEachFixture { get; set; }

		public TestSuite LoadAssembly (string assemblyName, IDictionary settings)
		{
			return builder.Build (assemblyName, settings ?? empty);
		}

		public TestSuite LoadAssembly (Assembly assembly, IDictionary settings)
		{
			return builder.Build (assembly, settings ?? empty);
		}

		#region writer

		public TextWriter Writer { get; set; }

		public bool OpenWriter (string message, Context activity)
		{
			passed = 0;
			failed = 0;
			skipped = 0;
			inconclusive = 0;

			DateTime now = DateTime.Now;
			// let the application provide it's own TextWriter to ease automation with AutoStart property
			if (Writer == null) {
				if (Options.ShowUseNetworkLogger) {
					Console.WriteLine ("[{0}] Sending '{1}' results to {2}:{3}", now, message, Options.HostName, Options.HostPort);
					try {
						Writer = new TcpTextWriter (Options.HostName, Options.HostPort);
					} catch (SocketException) {
						string msg = String.Format ("Cannot connect to {0}:{1}. Start network service or disable network option", options.HostName, options.HostPort);
						Toast.MakeText (activity, msg, ToastLength.Long).Show ();
						return false;
					}
				} else {
					Writer = Console.Out;
				}
			}

			Writer.WriteLine ("[Runner executing:\t{0}]", message);

			// FIXME: provide valid MFA version
			Writer.WriteLine ("[M4A Version:\t{0}]", "???");

			Writer.WriteLine ("[Board:\t\t{0}]", Build.Board);
#if __ANDROID_8__
			if (((int) Build.VERSION.SdkInt) >= 8) {
				Writer.WriteLine ("[Bootloader:\t{0}]", Build.Bootloader);
			}
#endif
			Writer.WriteLine ("[Brand:\t\t{0}]", Build.Brand);
			string cpuAbi = Build.CpuAbi;
#if __ANDROID_8__
			if (((int) Build.VERSION.SdkInt) >= 8) {
				cpuAbi += " " + Build.CpuAbi2;
			}
#endif
			Writer.WriteLine ("[CpuAbi:\t{0}]", cpuAbi);
			Writer.WriteLine ("[Device:\t{0}]", Build.Device);
			Writer.WriteLine ("[Display:\t{0}]", Build.Display);
			Writer.WriteLine ("[Fingerprint:\t{0}]", Build.Fingerprint);
#if __ANDROID_8__
			if (((int) Build.VERSION.SdkInt) >= 8) {
				Writer.WriteLine ("[Hardware:\t{0}]", Build.Hardware);
			}
#endif
			Writer.WriteLine ("[Host:\t\t{0}]", Build.Host);
			Writer.WriteLine ("[Id:\t\t{0}]", Build.Id);
			Writer.WriteLine ("[Manufacturer:\t{0}]", Build.Manufacturer);
			Writer.WriteLine ("[Model:\t\t{0}]", Build.Model);
			Writer.WriteLine ("[Product:\t{0}]", Build.Product);
#if __ANDROID_8__
			if (((int) Build.VERSION.SdkInt) >= 8) {
				Writer.WriteLine ("[Radio:\t\t{0}]", Build.Radio);
			}
#endif
			Writer.WriteLine ("[Tags:\t\t{0}]", Build.Tags);
			Writer.WriteLine ("[Time:\t\t{0}]", Build.Time);
			Writer.WriteLine ("[Type:\t\t{0}]", Build.Type);
			Writer.WriteLine ("[User:\t\t{0}]", Build.User);
			Writer.WriteLine ("[VERSION.Codename:\t{0}]", Build.VERSION.Codename);
			Writer.WriteLine ("[VERSION.Incremental:\t{0}]", Build.VERSION.Incremental);
			Writer.WriteLine ("[VERSION.Release:\t{0}]", Build.VERSION.Release);
			Writer.WriteLine ("[VERSION.Sdk:\t\t{0}]", Build.VERSION.Sdk);
			Writer.WriteLine ("[VERSION.SdkInt:\t{0}]", Build.VERSION.SdkInt);
			Writer.WriteLine ("[Device Date/Time:\t{0}]", now); // to match earlier C.WL output

			// FIXME: add data about how the app was compiled (e.g. ARMvX, LLVM, Linker options)
			return true;
		}

		public void CloseWriter ()
		{
			Writer.Close ();
			Writer = null;
		}

		#endregion

		public void TestStarted (ITest test)
		{
			if (test is TestSuite) {
				Writer.WriteLine ();
				time.Push (DateTime.UtcNow);
				Writer.WriteLine (test.Name);
			} else
				Writer.Write ("\t{0} ", test.Name);
			Writer.Flush (); // Sometimes the test fails before it completes crashing the runtime, it's good to have the name of the test on the screen
		}

		Stack<DateTime> time = new Stack<DateTime> ();

		public void TestFinished (ITestResult result)
		{
			AndroidRunner.Results [result.Test.FullName ?? result.Test.Name] = result as TestResult;

			if (result.Test is TestSuite) {
				//if (!result.IsError && !result.IsFailure && !result.IsSuccess && !result.Executed)
				//Writer.WriteLine ("\t[INFO] {0}", result.Message);
				if (result.ResultState.Status != TestStatus.Failed
					&& result.ResultState.Status != TestStatus.Skipped
					&& result.ResultState.Status != TestStatus.Passed
					&& result.ResultState.Status != TestStatus.Inconclusive)
					Writer.WriteLine ("\t[INFO] {0}", result.Message);

				var diff = DateTime.UtcNow - time.Pop ();
				Writer.WriteLine ("{0} : {1} ms", result.Test.Name, diff.TotalMilliseconds);
				if (GCAfterEachFixture)
					GC.Collect ();
			} else {
				if (result.ResultState.Status == TestStatus.Passed) {
					//Writer.Write ("\t{0} ", result.Executed ? "[PASS]" : "[IGNORED]");
					Writer.Write ("{0}", result.ResultState.ToString ());
					passed++;
				} else if (result.ResultState.Status == TestStatus.Failed) {
					Writer.Write ("[FAIL]");
					failed++;
				} else {
					Writer.Write ("[INFO]");
					if (result.ResultState.Status == TestStatus.Skipped)
						skipped++;
					else if (result.ResultState.Status == TestStatus.Inconclusive)
						inconclusive++;
				}

				string message = result.Message;
				if (!String.IsNullOrEmpty (message)) {
					Writer.Write (" : {0}", message.Replace ("\r\n", "\\r\\n"));
				}
				Writer.WriteLine ();

				string stacktrace = result.StackTrace;
				if (!String.IsNullOrEmpty (result.StackTrace)) {
					string[] lines = stacktrace.Split (new char [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (string line in lines)
						Writer.WriteLine ("\t\t{0}", line);
				}
			}
		}

		static AndroidRunner runner = new AndroidRunner ();

		static public AndroidRunner Runner {
			get { return runner; }
		}

		static List<TestSuite> top = new List<TestSuite> ();
		static Dictionary<string,TestSuite> suites = new Dictionary<string, TestSuite> ();
		static Dictionary<string,TestResult> results = new Dictionary<string, TestResult> ();

		bool ITestFilter.IsEmpty {
			get { return top.Count > 0 && suites.Count == 0 && results.Count == 0; }
		}

		public ITestFilter Filter { get; set; }

		static public IList<TestSuite> AssemblyLevel {
			get { return top; }
		}

		static public IDictionary<string,TestSuite> Suites {
			get { return suites; }
		}

		static public IDictionary<string,TestResult> Results {
			get { return results; }
		}

		public TestResult Run (NUnit.Framework.Internal.Test test)
		{
			TestExecutionContext current = TestExecutionContext.CurrentContext;
			current.WorkDirectory = System.Environment.CurrentDirectory;
			current.Listener = this;
			current.TestObject = test is TestSuite ? null : Reflect.Construct ((test as TestMethod).Method.ReflectedType, null);
			WorkItem wi = test.CreateWorkItem (Filter ?? TestFilter.Empty);
			if (test is TestMethod)
				(test.Parent as TestSuite).GetOneTimeSetUpCommand ().Execute (current);
			wi.Execute (current);
			if (test is TestMethod)
				(test.Parent as TestSuite).GetOneTimeTearDownCommand ().Execute (current);
			return wi.Result;
		}

		public void TestOutput (TestOutput testOutput)
		{
			if (!String.IsNullOrEmpty (testOutput.Text)) {
				string kind = testOutput.Type.ToString ();
				foreach (string l in testOutput.Text.Split ('\n')) {
					Writer.Write ("  {0}: ", kind);
					Writer.WriteLine (l);
				}
			}
		}

		public bool Pass (ITest pass)
		{
			return true;
		}

		#region moved from RunnerActivity

		internal bool Initialized { get; set; }

		internal void AddTest (Assembly assembly)
		{
			if (assembly == null)
				throw new ArgumentNullException ("assembly");

			// this can be called many times but we only want to load them
			// once since we need to share them across most activities
			if (!Initialized) {
				// TestLoader.Load always return a TestSuite so we can avoid casting many times
				TestSuite ts = Runner.LoadAssembly (assembly, null);
				if (ts != null) {
					AssemblyLevel.Add (ts);
					AddTest (ts, assembly.GetName ());
				}
			}
		}

		void AddTest (TestSuite suite, AssemblyName assemblyName)
		{
			string name = suite.FullName ?? suite.Name;
			if (Suites.ContainsKey (name)) {
				string newname = $"{assemblyName.Name}!{name}";
				Log.Warn (TAG, $"Duplicate test suite '{name}', assigning new name '{newname}'");
				name = newname;
			}

			Suites.Add (name, suite);
			foreach (ITest test in suite.Tests) {
				TestSuite ts = (test as TestSuite);
				if (ts != null) {
					AddTest (ts, assemblyName);
				}
			}
		}

		internal TestResult Run (NUnitTest test, Context context)
		{
			if (!OpenWriter ("Run Everything", context))
				return null;

			try {
				return Run (test);
			} finally {
				int testCount = passed + failed + skipped + inconclusive;
				Runner.Writer.WriteLine ("Tests run: {0}, Passed: {1}, Failed: {2}, Skipped: {3}, Inconclusive: {4}",
				                         testCount, passed, failed, skipped, inconclusive);
				CloseWriter ();
			}
		}
		#endregion

		internal static NUnitTest GetSetupTestTarget (Intent intent)
		{
			return GetSetupTestTarget (intent == null ? null : intent.Extras);
		}

		internal static NUnitTest GetSetupTestTarget (Bundle bundle)
		{
			var suiteName = bundle == null ? null : bundle.GetString ("suite");
			TestSuite suite = null;
			if (suiteName != null && !Suites.TryGetValue (suiteName, out suite)) {
				Console.WriteLine ("Invalid suite name: {0}", suiteName);
				Console.WriteLine ("Supported suite names:");
				foreach (KeyValuePair<string, TestSuite> e in Suites)
					Console.WriteLine ("\t{0}", e.Key);
				return new TestSuite ("__error__");
			}
			if (suite != null)
				return suite;
			else {
				var ts = new TestSuite (global::Android.App.Application.Context.PackageName);
				Console.Error.WriteLine (ts.FullName);
				foreach (var i in AssemblyLevel.Cast<NUnitTest> ())
					ts.Add (i);
				return ts;
			}
		}

		internal void AddTestFilters (IEnumerable<string> included, IEnumerable<string> excluded)
		{
			TestFilter filter = TestFilter.Empty;

			Log.Info (TAG, "Configuring test categories to include:");
			ChainCategoryFilter (included, false, ref filter);

			Log.Info (TAG, "Configuring test categories to exclude:");
			ChainCategoryFilter (excluded, true, ref filter);

			if (filter.IsEmpty)
				return;

			if (Filter == null)
				Filter = filter;
			else
				Filter = new AndFilter (Filter, filter);
		}

		static void ChainCategoryFilter (IEnumerable <string> categories, bool negate, ref TestFilter chain)
		{
			bool gotCategories = false;
			if (categories != null) {
				var filter = new CategoryFilter ();
				foreach (string c in categories) {
					Log.Info (TAG, "  {0}", c);
					filter.AddCategory (c);
					gotCategories = true;
				}

				if (gotCategories)
					chain = new AndFilter (chain, negate ? (TestFilter)new NotFilter (filter) : (TestFilter)filter);
			}

			if (!gotCategories)
				Log.Info (TAG, "  none");
		}
	}
}
