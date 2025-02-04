using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace NativeAOT;

// Name required for typemap in NativeAotTypeManager
[Activity (Name = "my.MainActivity", Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate (Bundle? savedInstanceState)
	{
		Log.Debug ("NativeAOT", "MainActivity.OnCreate()");

		base.OnCreate (savedInstanceState);
	}
}
