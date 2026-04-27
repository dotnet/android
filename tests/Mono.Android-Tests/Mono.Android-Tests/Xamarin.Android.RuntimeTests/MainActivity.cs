using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Xamarin.Android.RuntimeTests
{
	[Activity (Label = "Mono.Android Tests", MainLauncher = true,
			Name="xamarin.android.runtimetests.MainActivity")]
	public partial class MainActivity : Activity
	{
		protected override void OnCreate (Bundle? bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.Main);

			first_text_view.Click += delegate {
				// ignore
			};

			second_text_view.Click += delegate {
				// ignore
			};

			my_scroll_view.FillViewport = true;

			first_text_view.Click += delegate {
				// ignore
			};

			second_text_view.Click += delegate {
				// ignore
			};

			// test https://github.com/xamarin/xamarin-android/issues/3263
			edit_text.TextChanged += delegate { };

			csharp_simple_fragment.AllowEnterTransitionOverlap = true;
			csharp_partial_assembly.AllowEnterTransitionOverlap = true;
			csharp_full_assembly.AllowEnterTransitionOverlap = true;
		}
	}

#if __ANDROID_11__
	public class MyFragment : Fragment {
		public override View OnCreateView (LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
		{
			return new TextView (Activity) {
				Text = "via fragment!",
			};
		}
	}
#endif  // ANDROID_11
}

