using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NUnit.Framework.Api;
using NUnit.Framework.Internal;

using NUnitTest = NUnit.Framework.Internal.Test;
using Android.Text;
using Java.Lang;
using Android.Graphics;

namespace Xamarin.Android.NUnitLite
{
	[Activity (Label = "NUnitLite runner")]
	public class TestSuiteActivity : Activity
	{
		static readonly string [] from_cols = {"id", "passed", "failed", "ignored", "inconclusive", "result", "message"};
		static readonly int [] to_ids = { Resource.Id.ResultsId, Resource.Id.ResultsPassed, Resource.Id.ResultsFailed, Resource.Id.ResultsIgnored, Resource.Id.ResultsInconclusive, Resource.Id.ResultsResult, Resource.Id.ResultsMessage };

		protected bool GCAfterEachFixture {
			get { return AndroidRunner.Runner.GCAfterEachFixture; }
			set { AndroidRunner.Runner.GCAfterEachFixture = value; }
		}

		protected ITestFilter Filter {
			get { return AndroidRunner.Runner.Filter; }
			set { AndroidRunner.Runner.Filter = value; }
		}

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			SetContentView (Resource.Layout.test_suite);

			var lv = FindViewById<ListView> (Resource.Id.TestSuiteListView);
			var data = new JavaList<IDictionary<string,object>> ();

			UpdateData (data, lv);

			var automated = this.Intent.GetBooleanExtra ("automated", false);
			if (automated) {
				AndroidRunner.Runner.Options.LoadFromBundle (Intent.Extras);
			}
			AndroidRunner.Runner.AddTestFilters (GetIncludedCategories (), GetExcludedCategories ());

			UpdateFilter ();

			FindViewById<TextView> (Resource.Id.RunTestsButton).Click += (o, e) => {
				AndroidRunner.Runner.Run (current_test, this);
				UpdateData (data, lv);
			};
			FindViewById<TextView> (Resource.Id.OptionsButton).Click += (o, e) => {
				var intent = new Intent (this, typeof (OptionsActivity));
				intent.AddFlags (ActivityFlags.NewTask);
				this.StartActivityForResult (intent, (int) Result.Ok);
			};

			lv.ItemClick += (sender, e) => {
				var item = (TestData) ((SimpleAdapter) lv.Adapter).GetItem (e.Position);
				if (AndroidRunner.Suites.ContainsKey (item.Id)) {
					var intent = new Intent (this, typeof (TestSuiteActivity));
					intent.AddFlags (ActivityFlags.NewTask);
					intent.PutExtra ("suite", item.Id);
					this.StartActivityForResult (intent, (int) Result.Ok);
				} else {
					var intent = new Intent (this, typeof (TestResultActivity));
					intent.AddFlags (ActivityFlags.NewTask);
					intent.PutExtra ("test", item.Id);
					this.StartActivityForResult (intent, (int) Result.Ok);
				}
			};

			AndroidRunner.Runner.Initialized = true;
			if (automated) {
				System.Threading.ThreadPool.QueueUserWorkItem (delegate {
					Log.Info ("NUnitLite", "NUnit automated tests started");
					AndroidRunner.Runner.Run (current_test, this);
					Log.Info ("NUnitLite", "NUnit automated tests completed");
					Finish ();
				});
			}
			Log.Info ("NUnitLite", "NUnit automated tests loaded.");
		}

		protected override void OnResume ()
		{
			base.OnResume ();
			var lv = FindViewById<ListView> (Resource.Id.TestSuiteListView);
			var data = new JavaList<IDictionary<string,object>> ();
			UpdateData (data, lv);
		}

		protected virtual IEnumerable <string> GetIncludedCategories ()
		{
			yield break;
		}

		protected virtual IEnumerable <string> GetExcludedCategories ()
		{
			yield break;
		}

		// Subclasses can override this method to update the test filtering that the runner will use.
		// Subclasses should set the `Filter` property to their new filter value
		protected virtual void UpdateFilter ()
		{
		}

		public void AddTest (Assembly assembly)
		{
			AndroidRunner.Runner.AddTest (assembly);
		}

		IEnumerable<NUnitTest> GetChildTests (NUnitTest test)
		{
			if (test is TestSuite)
				foreach (NUnitTest child in ((TestSuite) test).Tests)
					yield return child;
			else
				yield return test;
		}

		void UpdateData (JavaList<IDictionary<string,object>> data, ListView lv)
		{
			data.Clear ();
			var testTarget = SetupTestTarget ();
			foreach (var test in GetChildTests (testTarget)) {
				TestResult res;
				AndroidRunner.Results.TryGetValue (test.FullName, out res);
				if (test is TestSuite) {
					var suite = test as TestSuite;
					data.Add (new TestData (suite.FullName,
					                        res != null && res.PassCount > 0 ? res.PassCount + " passed" : null,
					                        res != null && res.FailCount > 0 ? res.FailCount + " failed" : null,
					                        res != null && res.SkipCount > 0 ? res.SkipCount + " ignored" : null,
					                        res != null && res.InconclusiveCount > 0 ? res.InconclusiveCount + " inconclusive" : null,
					                        res == null ? null : res.ResultState.Status.ToString (), res == null ? null : res.Message));
				} else if (test is NUnitTest)
					data.Add (new TestData (test.FullName, null, null, null, null, res == null ? null : res.ResultState.Status.ToString (), res == null ? null : res.Message));
			}
			lv.Adapter = new TestDataAdapter (this, data, Resource.Layout.results, from_cols, to_ids);
		}

		class TestDataAdapter : SimpleAdapter
		{
			JavaList<IDictionary<string,object>> data;

			public TestDataAdapter (Context ctx, JavaList<IDictionary<string,object>> data, int resId, string [] fromCols, int [] toIds)
				: base (ctx, data, resId, fromCols, toIds)
			{
				this.data = data;
			}

			public override View GetView (int position, View convertView, ViewGroup parent)
			{
				var view = base.GetView (position, convertView, parent);
				var tv = view.FindViewById<TextView> (Resource.Id.ResultsResult);
				TestStatus status;
				System.Enum.TryParse<TestStatus> ((string) data [position] ["result"], out status);
				tv.SetTextColor (GetStatusColor (status));
				return view;
			}

			static Color GetStatusColor (TestStatus status)
			{
				switch (status) {
				case TestStatus.Passed:
					return Color.Green;
				case TestStatus.Failed:
					return Color.Red;
				case TestStatus.Skipped:
					return Color.Yellow;
				case TestStatus.Inconclusive:
					return Color.Blue;
				default:
					return Color.White;
				}
			}
		}

		class TestData : JavaDictionary<string,object>
		{
			public TestData (string id, string passed, string failed, string ignored, string inconclusive, string result, string message)
			{
				Id = id;
				Passed = passed;
				Failed = failed;
				Ignored = ignored;
				Inconclusive = inconclusive;
				Result = result;
				Message = message;
			}

			public string Id {
				get { return (string) this ["id"]; }
				set { this ["id"] = value; }
			}

			public string Passed {
				get { return (string) this ["passed"]; }
				set { this ["passed"] = value; }
			}

			public string Failed {
				get { return (string) this ["failed"]; }
				set { this ["failed"] = value; }
			}

			public string Ignored {
				get { return (string) this ["ignored"]; }
				set { this ["ignored"] = value; }
			}

			public string Inconclusive {
				get { return (string) this ["inconclusive"]; }
				set { this ["inconclusive"] = value; }
			}

			public string Result {
				get { return (string) this ["result"]; }
				set { this ["result"] = value; }
			}

			public string Message {
				get { return (string) this ["message"]; }
				set { this ["message"] = value; }
			}
		}

		NUnitTest current_test;

		NUnitTest SetupTestTarget ()
		{
			if (current_test == null)
				current_test = AndroidRunner.GetSetupTestTarget (Intent);
			return current_test;
		}
	}
}

