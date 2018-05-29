using Android.App;
using Android.Widget;
using Android.OS;

namespace Xamarin.Android.Tests.CodeBehindBuildTests
{
	[Activity (Label = "Settings")]
	public class SettingsActivity : Activity
	{
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.settings);
			var items = new Binding.settings (this);

			// Compile time: check by assignment if properties have correct types
			ScrollView scroll = items.settings_container;
			TextView title = items.title;
			TextView account_type = items.account_type;
			TextView account_type_subtitle = items.account_type_subtitle;
			TextView account_email = items.account_email;
			Button subscribe_button = items.subscribe_button;
			TextView stream_quality_item_title = items.stream_quality_item_title;
			ImageButton streaming_options_button = items.streaming_options_button;
			TextView auto_sync_status = items.auto_sync_status;

			// Included with `android:id` override
			LinearLayout subscribe_progress_indicator = items.subscribe_progress_indicator;
		}
	}
}

