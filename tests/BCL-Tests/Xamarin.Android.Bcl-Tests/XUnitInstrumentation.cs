using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Android.App;
using Android.Runtime;
using Android.Util;

using Xamarin.Android.UnitTests;
using Xamarin.Android.UnitTests.XUnit;

namespace xUnitTestRunner
{
	[Instrumentation (Name = "xamarin.android.bcltests.XUnitInstrumentation")]
	public class XUnitInstrumentation : XUnitTestInstrumentation
	{
		const string DefaultLogTag = "xUnit";

		string logTag = DefaultLogTag;

		protected override string LogTag { 
			get { return logTag; } 
			set { logTag = value ?? DefaultLogTag; }
		}

		protected XUnitInstrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
			CommonInit ();
		}

		public XUnitInstrumentation ()
		{
			CommonInit ();
		}

		void CommonInit ()
		{
			string cacheDir = Path.Combine (Application.Context.CacheDir.AbsolutePath, DefaultLogTag);
			using (var files = typeof (XUnitInstrumentation).Assembly.GetManifestResourceStream ("bcl-tests.zip")) {
				ExtractAssemblies (cacheDir, files);
			}

			TestsDirectory = cacheDir;
		}

		protected override void ConfigureFilters (XUnitTestRunner runner)
		{
			base.ConfigureFilters (runner);

			if (runner == null)
				throw new ArgumentNullException (nameof (runner));
			
			HashSet<string> excludedTestNames = null;
			using (var s = typeof (XUnitInstrumentation).Assembly.GetManifestResourceStream ("xunit-excluded-tests.txt")) {
				using (var sr = new StreamReader (s, Encoding.UTF8)) {
					excludedTestNames = LoadExcludedTests (sr);
				}
			}
			// Known filters for CoreFX tests
			var filters = new List<XUnitFilter> {
				// From the Mono runtime (https://github.com/mono/mono/blob/master/mcs/build/tests.make#L255)
				new XUnitFilter ("category", "failing", true),
				new XUnitFilter ("category", "nonnetcoreapptests", true),
				new XUnitFilter ("category", "outerloop", true),
				new XUnitFilter ("Benchmark", "true", true),

				// From some failing corefx tests
				new XUnitFilter ("category", "nonlinuxtests", true),
				new XUnitFilter ("category", "nonmonotests", true),
				new XUnitFilter ("category", "nonnetfxtests", true),
#if !DEBUG  // aka "Release"
				new XUnitFilter ("category", "nonuapaottests", true),
#endif  // !DEBUG
			};

			if (excludedTestNames != null && excludedTestNames.Count > 0) {
				foreach (string typeName in excludedTestNames) {
					filters.Add (new XUnitFilter (typeName, true));
				}
			}

			runner.SetFilters (filters);
		}

		protected override IList<TestAssemblyInfo> GetTestAssemblies()
		{
			var ret = new List<TestAssemblyInfo> ();

			using (var stream = typeof (XUnitInstrumentation).Assembly.GetManifestResourceStream ("xunit-assemblies.txt"))
			using (var reader = new StreamReader (stream)) {
				string line;
				while ((line = reader.ReadLine ()) != null) {
					string file = Path.Combine (TestsDirectory, line);

					try {
						Log.Info (LogTag, $"Adding test assembly: {file}");

						Assembly asm = LoadTestAssembly (file);
						if (asm == null)
							continue;

						// We store full path since Assembly.Location is not reliable on Android - it may hold a relative
						// path or no path at all
						ret.Add (new TestAssemblyInfo (asm, file));
					} catch (Exception e) {
						throw new InvalidOperationException ($"Unable to load test assembly: {file}", e);
					}
				}
			}

			return ret;
		}
	}
}
