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

			// Get our button from the layout resource
			Button button = FindViewById<Button> (Resource.Id.myButton);
			
			Android.Util.Log.Info ("BUTTON_SETUP", $"Button found: {button != null}, button.Handle=0x{button?.Handle ?? IntPtr.Zero:X}");

			// Test the standard Click event pattern - this uses View_OnClickListenerImplementor internally
			Android.Util.Log.Info ("BUTTON_SETUP", "Adding Click event handler...");
			button.Click += Button_Click;
			Android.Util.Log.Info ("BUTTON_SETUP", "Click event handler added!");
		}
		
		void Button_Click (object? sender, EventArgs e)
		{
			Android.Util.Log.Error ("BUTTON_CLICK", $"Button_Click called! sender={sender?.GetType().FullName ?? "null"}");
			if (sender is Button btn) {
				btn.Text = $"{count++} clicks!";
			}
		}

		protected override void OnResume ()
		{
			base.OnResume ();

			// Test TypeMap Marshal Method Stub
			try {
				Android.Util.Log.Info ("monodroid-test", "Calling native stub Java_example_MainActivity_n_1onCreate__Landroid_os_Bundle_2V...");
				native_invoke_onCreate (
					JniEnvironment.EnvironmentPointer,
					this.Handle,
					IntPtr.Zero);
				Android.Util.Log.Info ("monodroid-test", "Called native stub successfully!");
			} catch (Exception ex) {
				Android.Util.Log.Error ("monodroid-test", $"Error calling native stub: {ex}");
			}
		}

		[DllImport ("xamarin-app", EntryPoint = "Java_example_MainActivity_n_1onCreate__Landroid_os_Bundle_2V")]
		static extern void native_invoke_onCreate (IntPtr jnienv, IntPtr thiz, IntPtr bundle);

		// Note: This method must NOT have [UnmanagedCallersOnly] because the TypeMap proxy's
		// UCO wrapper calls this method via IL 'call' instruction. The UCO wrapper handles
		// the native-to-managed transition; this callback is called from managed code.
		static void n_onCreate (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
		{
			Android.Util.Log.Error ("n_onCreate", $"jnienv=0x{jnienv:x}, native__this=0x{native__this:x}, native_savedInstanceState=0x{native_savedInstanceState:x}");
			var __this = Java.Lang.Object.GetObject<MainActivity> (native__this, JniHandleOwnership.DoNotTransfer);
			Android.Util.Log.Error ("n_onCreate", $"Got __this={__this?.GetType()?.FullName ?? "null"}");
			var bundle = Java.Lang.Object.GetObject<Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
			Android.Util.Log.Error ("n_onCreate", $"Got bundle={bundle?.GetType()?.FullName ?? "null"}");
			__this.OnCreate (bundle);
		}
	}
}


