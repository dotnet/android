using Android.App;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
	[Activity (Label = "Onboarding")]
	public class OnboardingActivity : Activity
	{
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.onboarding_intro);
			var items = new Binding.onboarding_intro (this);

			// Compile time: check by assignment if properties have correct types

			// Test: widget has the same name as the layout - append _View to the name for Views
			LinearLayout onboarding_intro_View = items.onboarding_intro_View;
			TextView title = items.title;
			TextView welcome = items.welcome;

			// Test: multiple layout versions, different types of elements with the same ID
#if NOT_CONFLICTING_TEXTVIEW
			TextView different_view_types = items.different_view_types;
#elif NOT_CONFLICTING_BUTTON
			Button different_view_types = items.different_view_types;
#else
			global::Android.Views.View different_view_types = items.different_view_types;
#endif

			// Test: multiple layout versions, different types of elements with the same ID
#if NOT_CONFLICTING_LINEARLAYOUT
			LinearLayout onboarding_info = items.onboarding_info;
#elif NOT_CONFLICTING_RELATIVELAYOUT
			RelativeLayout onboarding_info = items.onboarding_info;
#else
			global::Android.Views.View onboarding_info = items.onboarding_info;
#endif

			TextView intro_highlighted_text = items.intro_highlighted_text;
			TextView intro_primary_text = items.intro_primary_text;
			TextView intro_secondary_text = items.intro_secondary_text;
			RelativeLayout more_info = items.more_info;
			TextView more_highlighted_text = items.more_highlighted_text;
			TextView more_intro_primary_text = items.more_intro_primary_text;
			TextView more_intro_secondary_text = items.more_intro_secondary_text;
		}
	}
}

