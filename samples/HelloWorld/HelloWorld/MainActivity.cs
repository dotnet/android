using Android.App;
using Android.Widget;
using Android.OS;
using System;
using Android.Runtime;
using Java.Interop;
using System.Runtime.InteropServices;

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

		// Manual connector to test TypeMaps marshal methods
		[Register ("onCreate", "(Landroid/os/Bundle;)V", "n_onCreate")]
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.myButton);

			button.Click += delegate {
				button.Text = string.Format ("{0} clicks!", count++);
			};
		}

		[System.Runtime.InteropServices.UnmanagedCallersOnly]
		static void n_onCreate (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
		{
			var __this = Java.Lang.Object.GetObject<MainActivity> (native__this, JniHandleOwnership.DoNotTransfer);
			var bundle = Java.Lang.Object.GetObject<Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
			__this.OnCreate (bundle);
		}
	}
}


