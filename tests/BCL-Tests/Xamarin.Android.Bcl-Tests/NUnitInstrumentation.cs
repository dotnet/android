using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using Android.App;
using Android.Runtime;
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
			IList<TestAssemblyInfo> ret = base.GetTestAssemblies();

			if (ret == null)
				ret = new List<TestAssemblyInfo> ();

			Assembly asm = typeof (BclTests.HttpClientTest).Assembly;
			ret.Add (new TestAssemblyInfo (asm, asm.Location ?? String.Empty));

			return ret;
		}

		protected override IEnumerable<TestAssemblyInfo> GetTestAssembliesFromDirectory (string directoryPath)
		{
			using (var reader = new StreamReader (typeof (NUnitInstrumentation).Assembly.GetManifestResourceStream ("nunit-assemblies.txt")))
			{
				string line;
				while ((line = reader.ReadLine ()) != null)
				{
					yield return LoadTestAssembly (Path.Combine (directoryPath, line));
				}
			}
		}
	}
}
