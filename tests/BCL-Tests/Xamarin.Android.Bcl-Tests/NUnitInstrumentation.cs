using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Android.App;
using Android.Runtime;
using Android.Util;

using Xamarin.Android.UnitTests;
using Xamarin.Android.UnitTests.NUnit;

namespace UnitTestRunner
{
	[Instrumentation (Name = "xamarin.android.bcltests.NUnitInstrumentation")]
	public class NUnitInstrumentation : NUnitTestInstrumentation
	{
		const string DefaultLogTag = "NUnit";

		string logTag = DefaultLogTag;

		protected override string LogTag { 
			get { return logTag; } 
			set { logTag = value ?? DefaultLogTag; }
		}

		protected NUnitInstrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
			CommonInit ();
		}

		public NUnitInstrumentation ()
		{
			CommonInit ();
		}

		void CommonInit ()
		{
			string cacheDir = Path.Combine (Application.Context.CacheDir.AbsolutePath, DefaultLogTag);
			using (var files = typeof (NUnitInstrumentation).Assembly.GetManifestResourceStream ("bcl-tests.zip")) {
				ExtractAssemblies (cacheDir, files);
			}

			TestsDirectory = cacheDir;
			Environment.CurrentDirectory = cacheDir;
			var excluded = new List<string> {
				"AndroidNotWorking",
				"CAS",
				"InetAccess",
				"MobileNotWorking",
				"NotWorking",
			};

			if (!Environment.Is64BitOperatingSystem)
				excluded.Add ("LargeFileSupport");

			ExcludedCategories = excluded;
		}

		protected override void ConfigureFilters(NUnitTestRunner runner)
		{
			HashSet<string> excludedTestNames = null;
			using (var s = typeof (NUnitInstrumentation).Assembly.GetManifestResourceStream ("nunit-excluded-tests.txt")) {
				using (var sr = new StreamReader (s, Encoding.UTF8)) {
					excludedTestNames = LoadExcludedTests (sr);
				}
			}

			ExcludedTestNames = excludedTestNames;

			base.ConfigureFilters(runner);
		}

		protected override IList<TestAssemblyInfo> GetTestAssemblies()
		{
			var ret = new List<TestAssemblyInfo> ();

			using (var stream = typeof (NUnitInstrumentation).Assembly.GetManifestResourceStream ("nunit-assemblies.txt"))
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

			{
				Assembly asm = typeof (BclTests.HttpClientTest).Assembly;
				ret.Add (new TestAssemblyInfo (asm, asm.Location ?? String.Empty));
			}

			return ret;
		}
	}
}
