
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Text.Method;
using NUnit.Framework.Internal;
using NUnitTest = NUnit.Framework.Internal.Test;

namespace Xamarin.Android.NUnitLite
{
	[Activity (Label = "Test Result")]
	internal class TestResultActivity : Activity
	{
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Create your application here
			SetContentView (Resource.Layout.test_result);

			var testName = this.Intent.GetStringExtra ("test");
			var parentSuiteName = testName.Substring (0, testName.LastIndexOf ('.'));

			FindViewById<TextView> (Resource.Id.ResultRunSingleMethodTest).Click += delegate {
				AndroidRunner.Runner.Run ((NUnitTest) AndroidRunner.Suites [parentSuiteName].Tests.First (t => t.FullName == testName), this);
				UpdateData (AndroidRunner.Results [testName]);
			};
			if (AndroidRunner.Results.ContainsKey (testName))
				UpdateData (AndroidRunner.Results [testName]);
			else
				FindViewById<TextView> (Resource.Id.ResultFullName).Text = testName;
		}

		void UpdateData (TestResult test)
		{
			if (test == null)
				return; // no result to fill
			FindViewById<TextView> (Resource.Id.ResultFullName).Text = test.FullName;
			FindViewById<TextView> (Resource.Id.ResultResultState).Text = test.ResultState.ToString ();
			FindViewById<TextView> (Resource.Id.ResultMessage).Text= test.Message;
			FindViewById<TextView> (Resource.Id.ResultStackTrace).Text = test.StackTrace;
			FindViewById<TextView> (Resource.Id.ResultStackTrace).MovementMethod = new ScrollingMovementMethod ();
		}
	}
}

