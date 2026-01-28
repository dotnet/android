using Android.App;
using Android.OS;

namespace HelloWorld;

[Activity (Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
protected override void OnCreate (Bundle? savedInstanceState)
{
base.OnCreate (savedInstanceState);
SetContentView (Resource.Layout.Main);
}
}
