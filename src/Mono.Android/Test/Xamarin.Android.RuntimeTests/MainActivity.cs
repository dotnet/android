using System.Reflection;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Xamarin.Android.NUnitLite;

namespace Xamarin.Android.RuntimeTests
{
	[Activity (Label = "runtime", MainLauncher = true,
			Name="xamarin.android.runtimetests.MainActivity")]
	public partial class MainActivity : TestSuiteActivity
	{
		protected override void OnCreate (Bundle bundle)
		{
			// Note; for testing <fragment/> fixup only.
			// The actual view is set/replaced in `TestSuiteActivity.OnCreate()`
			SetContentView (Resource.Layout.FragmentFixup);

			first_text_view.Click += delegate {
				// ignore
			};

			// tests can be inside the main assembly
			AddTest (Assembly.GetExecutingAssembly ());
			// or in any reference assemblies
			// AddTest (typeof (Your.Library.TestClass).Assembly);

			// Once you called base.OnCreate(), you cannot add more assemblies.
			base.OnCreate (bundle);
		}
	}

#if __ANDROID_11__
	public class MyFragment : Fragment {

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			return new TextView (Activity) {
				Text = "via fragment!",
			};
		}
	}
#endif  // ANDROID_15
}

