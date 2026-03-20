using Android.App;
using Android.OS;

namespace HelloTrimmable;

[Activity (Label = "HelloTrimmable", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate (Bundle? savedInstanceState)
    {
        base.OnCreate (savedInstanceState);
        SetContentView (new Android.Widget.TextView (this) { Text = "Hello from Trimmable TypeMap!" });
    }
}
