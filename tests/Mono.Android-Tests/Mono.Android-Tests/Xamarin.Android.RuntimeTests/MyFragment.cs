using Android.App;
using Android.Views;
using Android.Widget;

namespace Xamarin.Android.RuntimeTests
{
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
