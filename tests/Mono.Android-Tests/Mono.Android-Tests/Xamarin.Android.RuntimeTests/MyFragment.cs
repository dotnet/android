#nullable enable

using System;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Xamarin.Android.RuntimeTests
{
#if __ANDROID_11__
	public class MyFragment : Fragment {
		public override View OnCreateView (LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
		{
#pragma warning disable CA1422 // Fragment.Activity is obsolete since API 28.
			var activity = Activity ?? throw new InvalidOperationException ("Fragment is not attached to an activity.");
#pragma warning restore CA1422
			return new TextView (activity) {
				Text = "via fragment!",
			};
		}
	}
#endif  // __ANDROID_11__
}
