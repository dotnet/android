#:property TargetFramework=net12.0-android
#:property OutputType=Exe
#:property RootNamespace=TrimmingTests
#:property ApplicationId=net.dot.android.trimmingtests.customviewfromlayout
#:property SupportedOSPlatformVersion=24
#:project TrimmingTests.Expectations/TrimmingTests.Expectations.csproj

// Regression test for https://github.com/dotnet/android/pull/8954 (and the user report in
// https://github.com/dotnet/android/issues/8975): a custom view that only a layout XML file uses has no managed references,
// so trimming removed it and inflating the layout failed with "Could not activate JNI Handle ... as managed type".

using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Widget;
using Microsoft.Android.TrimmingTests.Expectations;

namespace TrimmingTests
{
	[Activity (Label = "CustomViewFromLayout", MainLauncher = true)]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			try {
				SetContentView (Resource.Layout.main);
				var view = FindViewById (Resource.Id.custom_view);
				if (view?.GetType ().Name != "UpperCaseTextView") {
					throw new InvalidOperationException ($"The layout inflated {view?.GetType ()}.");
				}
				Log.Info ("TrimmingTests", "TRIMMING_TEST_RESULT: PASS");
			} catch (Exception e) {
				Log.Error ("TrimmingTests", $"TRIMMING_TEST_RESULT: FAIL {e}");
			}
		}
	}
}

namespace TrimmingTests.Views
{
	[Kept]
	[KeptMember (".ctor(Android.Content.Context,Android.Util.IAttributeSet)")]
	public class UpperCaseTextView : TextView
	{
		public UpperCaseTextView (Context context, IAttributeSet? attrs)
			: base (context, attrs)
		{
			SetAllCaps (true);
		}
	}
}
