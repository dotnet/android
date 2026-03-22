using Android.App;
using Android.OS;
using Android.Widget;

namespace HelloWorld
{
[Activity (
Icon            = "@mipmap/icon",
Label           = "HelloWorld",
MainLauncher    = true,
Name            = "example.MainActivity")]
public class MainActivity : Activity
{
int count = 1;

protected override void OnCreate (Bundle? savedInstanceState)
{
base.OnCreate (savedInstanceState);

SetContentView (Resource.Layout.Main);

// Use non-generic FindViewById to avoid JavaCast
var button = FindViewById (Resource.Id.myButton) as Button;
if (button is null) {
var tv = new TextView (this);
tv.Text = "Button not found! But Activity works.";
SetContentView (tv);
return;
}

button.Click += delegate {
button.Text = string.Format ("{0} clicks!", count++);
};
}
}
}
