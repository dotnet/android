using Android.App;
using Android.Widget;
using Android.OS;
using System;
using Android.Runtime;
using Android.Views;
using System.Runtime.InteropServices;
using Java.Interop;

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

	// Test: Class with [Export] attribute for dynamic method registration
	[Register ("example/ExportedMethodsClass")]
	public class ExportedMethodsClass : Java.Lang.Object
	{
		public ExportedMethodsClass ()
		{
			Android.Util.Log.Info ("EXPORT_TEST", "ExportedMethodsClass created");
		}

		public ExportedMethodsClass (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Export ("exportedMethod")]
		public void ExportedMethod ()
		{
			Android.Util.Log.Info ("EXPORT_TEST", "ExportedMethod called successfully!");
		}

		[Export ("exportedMethodWithArgs")]
		public int ExportedMethodWithArgs (int a, int b)
		{
			int result = a + b;
			Android.Util.Log.Info ("EXPORT_TEST", $"ExportedMethodWithArgs({a}, {b}) = {result}");
			return result;
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

		// TRIMMER TRAP: This method is never called but roots types for the trimmer.
		// The trimmer sees [UnmanagedCallersOnly] with EntryPoint and keeps the method.
		// The method body references activation constructors which roots the type hierarchy.
		[UnmanagedCallersOnly (EntryPoint = "_trimmer_trap_do_not_call")]
		static void TrimmerTrap ()
		{
			// These constructor calls root the types for the trimmer.
			// The trimmer will follow the type hierarchy and keep base classes too.
			_ = new MainActivity (IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
			_ = new MyClickListener (IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
			_ = new ExportedMethodsClass (IntPtr.Zero, JniHandleOwnership.DoNotTransfer);
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

			// Test [Export] attribute functionality
			Android.Util.Log.Info ("EXPORT_TEST", "Testing [Export] attribute...");
			var exportTest = new ExportedMethodsClass ();
			exportTest.ExportedMethod ();
			int result = exportTest.ExportedMethodWithArgs (3, 7);
			Android.Util.Log.Info ("EXPORT_TEST", $"Export test complete! Result = {result}");
		}

		static void n_onCreate (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
		{
			var __this = Java.Lang.Object.GetObject<MainActivity> (native__this, JniHandleOwnership.DoNotTransfer);
			var bundle = Java.Lang.Object.GetObject<Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
			__this.OnCreate (bundle);
		}
	}
}
