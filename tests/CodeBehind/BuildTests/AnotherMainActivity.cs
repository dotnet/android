using Android.App;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
	[Activity (Label = "AnotherMainActivity")]
	public partial class AnotherMainActivity : Activity
	{
		int count = 1;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Compile time: check by assignment if correct types were generated
			CommonSampleLibrary.LogFragment log = log_fragment;

#if NOT_CONFLICTING_FRAGMENT
			CommonSampleLibrary.LogFragment log2 = secondary_log_fragment;
#else
			global::Android.App.Fragment log2 = secondary_log_fragment;
#endif
			Button button = myButton;
			button.Click += delegate { button.Text = $"{count++} clicks!"; };
		}
	}
}

