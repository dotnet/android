using Android.App;
using Android.Widget;
using Android.OS;

namespace HelloWorld
{
[Activity (
Icon            = "@mipmap/icon",
Label           = "HelloWorld",
MainLauncher    = true,
Name            = "example.MainActivity")]
public class MainActivity : Activity
{
protected override void OnCreate (Bundle? savedInstanceState)
{
base.OnCreate (savedInstanceState);
SetContentView (Resource.Layout.Main);

Button button = FindViewById<Button> (Resource.Id.myButton);
button.Text = "Hello from Trimmable TypeMap!";
}
}
}
