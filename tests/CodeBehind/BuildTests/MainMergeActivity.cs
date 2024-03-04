using Android.App;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
	[Activity (Label = "MainMergeActivity")]
	public partial class MainMergeActivity : Activity
	{
		int count = 1;
		bool onSetContentViewCalled_01 = false;
		bool onSetContentViewCalled_02 = false;
		bool onSetContentViewCalled_03 = false;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.MainMerge);

			// Compile time: check by assignment if correct types were generated
			CommonSampleLibrary.LogFragment log = log_fragment;

#if NOT_CONFLICTING_FRAGMENT
			CommonSampleLibrary.LogFragment log2 = secondary_log_fragment;
#elif __HAVE_ANDROIDX__
			global::AndroidX.Fragment.App.Fragment log2 = secondary_log_fragment;
#else
			global::Android.App.Fragment log2 = secondary_log_fragment;
#endif
			TextView t = Text1;

			Button button = myButton;
			button.Click += delegate { button.Text = $"{count++} clicks!"; };
		}

		partial void OnSetContentView (global::Android.Views.View view, ref bool callBaseAfterReturn)
		{
			onSetContentViewCalled_01 = true;
		}

                partial void OnSetContentView (global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn)
		{
			onSetContentViewCalled_02 = true;
		}

                partial void OnSetContentView (int layoutResID, ref bool callBaseAfterReturn)
		{
			onSetContentViewCalled_03 = true;
		}
	}
}

