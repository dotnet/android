using System;
using System.Collections.Generic;
using System.Reflection;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Util;

using NUnit.Framework.Internal;

namespace Xamarin.Android.NUnitLite {

	public abstract class TestSuiteInstrumentation : Instrumentation {
		const string TAG = "NUnitLite";

		protected bool GCAfterEachFixture {
			get { return AndroidRunner.Runner.GCAfterEachFixture; }
			set { AndroidRunner.Runner.GCAfterEachFixture = value; }
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

			AndroidRunner.Runner.Initialized = true;
			var results = new Bundle ();
			int failed = 0;
			try {
				Log.Info (TAG, "NUnit automated tests started");
				AndroidRunner.Runner.Run (AndroidRunner.GetSetupTestTarget (arguments), TargetContext);
				Log.Info (TAG, "NUnit automated tests completed");
				int passed = 0, skipped = 0, inconclusive = 0;
				foreach (TestResult result in AndroidRunner.Results.Values) {
					if (result.HasChildren)
						continue;
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

		protected abstract void AddTests ();

		protected void AddTest (Assembly assembly)
		{
			AndroidRunner.Runner.AddTest (assembly);
		}

		protected virtual IEnumerable <string> GetIncludedCategories ()
		{
			return null;
		}

		protected virtual IEnumerable <string> GetExcludedCategories ()
		{
			return null;
		}
	}
}
