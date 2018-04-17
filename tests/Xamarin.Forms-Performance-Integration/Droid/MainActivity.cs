using System;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace Xamarin.Forms.Performance.Integration.Droid
{
	[Activity (Icon = "@drawable/icon", Theme = "@style/MyTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation, Name = "xamarin.forms.performance.integration.MainActivity")]
	public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
	{
		bool firstOnCreate = true;
		bool firstOnStart = true;
		bool firstOnResume = true;

		protected override void OnCreate (Bundle bundle)
		{
			if (firstOnCreate)
				Console.WriteLine ("startup-timing: OnCreate reached");

			TabLayoutResource = Resource.Layout.Tabbar;
			ToolbarResource = Resource.Layout.Toolbar;

			base.OnCreate (bundle);

			global::Xamarin.Forms.Forms.Init (this, bundle);

			LoadApplication (new App ());

			if (firstOnCreate) {
				Console.WriteLine ("startup-timing: OnCreate end reached");
				firstOnCreate = false;
			}
		}

		protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult (requestCode, resultCode, data);
		}

		protected override void OnStart ()
		{
			if (firstOnStart)
				Console.WriteLine ("startup-timing: OnStart reached");

			base.OnStart ();

			if (firstOnStart) {
				Console.WriteLine ("startup-timing: OnStart end reached");
				firstOnStart = false;
			}
		}

		protected override void OnResume ()
		{
			if (firstOnResume)
				Console.WriteLine ("startup-timing: OnResume reached");

			base.OnResume ();

			if (firstOnResume) {
				Console.WriteLine ("startup-timing: OnResume end reached");
				firstOnResume = false;
			}
		}
	}
}
