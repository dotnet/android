using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Runtime;

using Xamarin.Android.NUnitLite;

namespace Xamarin.Android.BclTests {

	[Instrumentation (Name="xamarin.android.bcltests.TestInstrumentation")]
	public class TestInstrumentation : TestSuiteInstrumentation {

		public TestInstrumentation (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
			GCAfterEachFixture = true;
		}

		protected override void AddTests ()
		{
			App.ExtractBclTestFiles ();

			foreach (var tests in App.GetTestAssemblies ()) {
				AddTest (tests);
			}
		}

		protected override IEnumerable<string> GetExcludedCategories ()
		{
			return App.GetExcludedCategories ();
		}

		protected override void UpdateFilter ()
		{
			Filter = App.UpdateFilter (Filter);
		}
	}
}

