using Android.App;
using Android.Widget;
using Android.OS;
using System;
using Android.Runtime;
using Android.Views;

namespace HelloWorld
{
	// Test: User-defined class implementing a Java interface
	[Register ("example/MyClickListener")]
	public class MyClickListener : Java.Lang.Object, View.IOnClickListener
	{
		int count = 1;
		
		public MyClickListener ()
		{
			Android.Util.Log.Info ("MY_LISTENER", "MyClickListener created");
		}
		
		public MyClickListener (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
		
		public void OnClick (View? v)
		{
			Android.Util.Log.Error ("MY_LISTENER", $"OnClick! count={count}");
			if (v is Button btn) {
				btn.Text = $"{count++} custom clicks!";
			}
		}
	}

	[Activity (
		Icon            = "@mipmap/icon",
		Label           = "HelloWorld",
		MainLauncher    = true,
		Name            = "example.MainActivity")]
	public class MainActivity : Activity
	{
		// Default constructor required by Android
		public MainActivity ()
		{
		}

		// Activation constructor for TypeMap v2
		protected MainActivity (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("onCreate", "(Landroid/os/Bundle;)V", "n_onCreate")]
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			SetContentView (Resource.Layout.Main);

			Button button = FindViewById<Button> (Resource.Id.myButton);
			
			Android.Util.Log.Info ("BUTTON_SETUP", $"Button found: Handle=0x{button?.Handle ?? IntPtr.Zero:X}");
			Android.Util.Log.Info ("BUTTON_SETUP", "Setting custom click listener...");
			
			// Test user-defined IOnClickListener implementation
			button.SetOnClickListener (new MyClickListener ());
			
			Android.Util.Log.Info ("BUTTON_SETUP", "Custom click listener set!");
		}

		static void n_onCreate (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
		{
			var __this = Java.Lang.Object.GetObject<MainActivity> (native__this, JniHandleOwnership.DoNotTransfer);
			var bundle = Java.Lang.Object.GetObject<Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
			__this.OnCreate (bundle);
		}
	}
}
