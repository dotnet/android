using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;

using NUnitLite.Runner;
using NUnit.Framework.Api;
using NUnit.Framework.Internal;

namespace Xamarin.Android.NUnitLite {

	public abstract class TestSuiteInstrumentation : Instrumentation {
		const string TAG = "NUnitLite";

		protected bool GCAfterEachFixture {
			get { return AndroidRunner.Runner.GCAfterEachFixture; }
			set { AndroidRunner.Runner.GCAfterEachFixture = value; }
		}

		protected ITestFilter Filter {
			get { return AndroidRunner.Runner.Filter; }
			set { AndroidRunner.Runner.Filter = value; }
		}

		protected TestSuiteInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		Bundle arguments;

		public override void OnCreate (Bundle arguments)
		{
			base.OnCreate (arguments);
			this.arguments = arguments;
			Start ();
		}

		public override void OnStart ()
		{
			base.OnStart ();

			AddTests ();

			AndroidRunner.Runner.Options.LoadFromBundle (arguments);
			AndroidRunner.Runner.AddTestFilters (GetIncludedCategories (), GetExcludedCategories ());

			UpdateFilter ();

			AndroidRunner.Runner.Initialized = true;
			var results = new Bundle ();
			int failed = 0;
			try {
				Log.Info (TAG, "NUnit automated tests started");
				var testResult  = AndroidRunner.Runner.Run (AndroidRunner.GetSetupTestTarget (arguments), TargetContext);
				var resultsFile = GetResultsPath ();
				if (resultsFile != null) {
					var startTime   = DateTime.Now;
					var resultsXml  = new NUnit2XmlOutputWriter (startTime);
					resultsXml.WriteResultFile (testResult, resultsFile);
					results.PutString ("nunit2-results-path", ToAdbPath (resultsFile));
				}
				Log.Info (TAG, "NUnit automated tests completed");
				int run = 0, passed = 0, skipped = 0, inconclusive = 0;
				foreach (TestResult result in AndroidRunner.Results.Values) {
					if (result.HasChildren)
						continue;
					run           += 1;
					inconclusive  += result.InconclusiveCount;
					failed        += result.FailCount;
					passed        += result.PassCount;
					skipped       += result.SkipCount;
					if (result.FailCount > 0) {
						Log.Error (TAG, "Test '{0}' failed: {1}", result.FullName, result.Message);
						// Avoid Java.Lang.NullPointerException: println needs a message
						if (!String.IsNullOrEmpty (result.StackTrace))
							Log.Error (TAG, result.StackTrace);
						results.PutString ("failure: " + result.FullName,
								result.Message + "\n" + result.StackTrace);
						Log.Error (TAG, "  "); // makes it easier to read the failures in logcat output
					}
				}
				results.PutInt ("run", run);
				results.PutInt ("passed", passed);
				results.PutInt ("failed", failed);
				results.PutInt ("skipped", skipped);
				results.PutInt ("inconclusive", inconclusive);
				string message = string.Format ("Passed: {0}, Failed: {1}, Skipped: {2}, Inconclusive: {3}",
						passed, failed, skipped, inconclusive);
				Log.Info (TAG, message);
			} catch (Exception e) {
				Log.Error (TAG, "Error: {0}", e);
				results.PutString ("error", e.ToString ());
			}
			Finish (failed == 0 ? Result.Ok : Result.Canceled, results);
		}

		string GetResultsPath ()
		{
			Java.IO.File resultsPathFile = GetExternalFilesDir ();
			var usePathFile = resultsPathFile != null && resultsPathFile.Exists ();
			var resultsPath = usePathFile
				? resultsPathFile.AbsolutePath
				: Path.Combine (Context.FilesDir.AbsolutePath, ".__override__");
			if (!usePathFile && !Directory.Exists (resultsPath))
				Directory.CreateDirectory (resultsPath);
			return Path.Combine (resultsPath, "TestResults.xml");
		}

		Java.IO.File GetExternalFilesDir ()
		{
			if (((int)Build.VERSION.SdkInt) < 19)
				return null;
			string type = null;
#if __ANDROID_19__
			type = global::Android.OS.Environment.DirectoryDocuments;
#else   // !__ANDROID_19__
			type = global::Android.OS.Environment.DirectoryDownloads;
#endif  // !__ANDROID_19__
			return Context.GetExternalFilesDir (type);
		}

		// On some Android targets, the external storage directory is "emulated",
		// in which case the paths used on-device by the application are *not*
		// paths that can be used off-device with `adb pull`.
		// For example, `Contxt.GetExternalFilesDir()` may return `/storage/emulated/foo`,
		// but `adb pull /storage/emulated/foo` will *fail*; instead, we may need
		// `adb pull /mnt/shell/emulated/foo`.
		// The `$EMULATED_STORAGE_SOURCE` and `$EMULATED_STORAGE_TARGET` environment
		// variables control the "on-device" (`$EMULATED_STORAGE_TARGET`) and
		// "off-device" (`$EMULATED_STORAGE_SOURCE`) directory prefixes
		string ToAdbPath (string path)
		{
			var source  = System.Environment.GetEnvironmentVariable ("EMULATED_STORAGE_SOURCE");
			var target  = System.Environment.GetEnvironmentVariable ("EMULATED_STORAGE_TARGET");

			if (!string.IsNullOrEmpty (source) && !string.IsNullOrEmpty (target) && path.StartsWith (target, StringComparison.Ordinal)) {
				return path.Replace (target, source);
			}
			return path;
		}

		protected abstract void AddTests ();

		protected void AddTest (Assembly assembly)
		{
			AndroidRunner.Runner.AddTest (assembly);
		}

		protected virtual IEnumerable <string> GetIncludedCategories ()
		{
			string include = arguments?.GetString ("include");
			if (!string.IsNullOrEmpty (include)) {
				foreach (var category in include.Split (':')) {
					yield return category;
				}
			}
		}

		protected virtual IEnumerable <string> GetExcludedCategories ()
		{
			string exclude = arguments?.GetString ("exclude");
			if (!string.IsNullOrEmpty (exclude)) {
				foreach (var category in exclude.Split (':')) {
					yield return category;
				}
			}
		}

		protected virtual void UpdateFilter ()
		{
		}
	}
}
