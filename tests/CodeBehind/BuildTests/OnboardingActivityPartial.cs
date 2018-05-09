using Android.App;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
	[Activity (Label = "OnboardingPartial")]
	public partial class OnboardingActivityPartial : Activity
	{
		bool onSetContentViewCalled_01 = false;
		bool onSetContentViewCalled_02 = false;
		bool onSetContentViewCalled_03 = false;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			LayoutInflater inflater = LayoutInflater;
			View contentView = inflater.Inflate (Resource.Layout.onboarding_intro, null);
			SetContentView (contentView);

			// Compile time: check by assignment if properties have correct types

			// Test: widget has the same name as the layout - append _View to the name for Views
			LinearLayout onboarding_intro_View_w = onboarding_intro_View;
			TextView title_w = title;
			TextView welcome_w = welcome;

			// Test: multiple layout versions, different types of elements with the same ID
#if NOT_CONFLICTING_TEXTVIEW
			TextView different_view_types_w = different_view_types;
#elif NOT_CONFLICTING_BUTTON
			Button different_view_types_w = different_view_types;
#else
			global::Android.Views.View different_view_types_w = different_view_types;
#endif

			// Test: multiple layout versions, different types of elements with the same ID
#if NOT_CONFLICTING_LINEARLAYOUT
			LinearLayout onboarding_info_w = onboarding_info;
#elif NOT_CONFLICTING_RELATIVELAYOUT
			RelativeLayout onboarding_info_w = onboarding_info;
#else
			global::Android.Views.View onboarding_info_w = onboarding_info;
#endif

			TextView intro_highlighted_text_w = intro_highlighted_text;
			TextView intro_primary_text_w = intro_primary_text;
			TextView intro_secondary_text_w = intro_secondary_text;
			RelativeLayout more_info_w = more_info;
			TextView more_highlighted_text_w = more_highlighted_text;
			TextView more_intro_primary_text_w = more_intro_primary_text;
			TextView more_intro_secondary_text_w = more_intro_secondary_text;
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
