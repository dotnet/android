// User-type test fixture assembly that references REAL Mono.Android.
// Exercises edge cases that MCW binding assemblies don't have:
// - User types extending Java peers without [Register]
// - Component attributes ([Activity], [Service], etc.)
// - [Export] methods
// - Nested user types
// - Generic user types

using System;
using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Runtime;
using Java.Interop;

[assembly: SupportedOSPlatform ("android21.0")]

// --- User Activity with explicit Name ---

namespace UserApp
{
	[Activity (Name = "com.example.userapp.MainActivity", MainLauncher = true, Label = "User App")]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Android.OS.Bundle? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
		}
	}

	// Activity WITHOUT explicit Name — should get CRC64-based JNI name
	[Activity (Label = "Settings")]
	public class SettingsActivity : Activity
	{
	}

	// Simple Activity subclass — no attributes at all, just extends a Java peer
	public class PlainActivity : Activity
	{
	}
}

// --- Services ---

namespace UserApp.Services
{
	[Service (Name = "com.example.userapp.MyBackgroundService")]
	public class MyBackgroundService : Android.App.Service
	{
		public override Android.OS.IBinder? OnBind (Android.Content.Intent? intent) => null;
	}

	// Service without explicit Name
	[Service]
	public class UnnamedService : Android.App.Service
	{
		public override Android.OS.IBinder? OnBind (Android.Content.Intent? intent) => null;
	}
}

// --- BroadcastReceiver ---

namespace UserApp.Receivers
{
	[BroadcastReceiver (Name = "com.example.userapp.BootReceiver", Exported = false)]
	public class BootReceiver : BroadcastReceiver
	{
		public override void OnReceive (Context? context, Intent? intent)
		{
		}
	}
}

// --- Application with BackupAgent ---

namespace UserApp
{
	public class MyBackupAgent : Android.App.Backup.BackupAgent
	{
		public override void OnBackup (Android.OS.ParcelFileDescriptor? oldState,
			Android.App.Backup.BackupDataOutput? data,
			Android.OS.ParcelFileDescriptor? newState)
		{
		}

		public override void OnRestore (Android.App.Backup.BackupDataInput? data,
			int appVersionCode,
			Android.OS.ParcelFileDescriptor? newState)
		{
		}
	}

	[Application (Name = "com.example.userapp.MyApp", BackupAgent = typeof (MyBackupAgent))]
	public class MyApp : Application
	{
		public MyApp (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

// --- Nested types ---

namespace UserApp.Nested
{
	[Register ("com/example/userapp/OuterClass")]
	public class OuterClass : Java.Lang.Object
	{
		// Nested class inheriting from Java peer — no [Register]
		public class InnerHelper : Java.Lang.Object
		{
		}

		// Deeply nested
		public class MiddleClass : Java.Lang.Object
		{
			public class DeepHelper : Java.Lang.Object
			{
			}
		}
	}
}

// --- Plain Java.Lang.Object subclasses (no attributes) ---

namespace UserApp.Models
{
	// These should all get CRC64-based JNI names
	public class UserModel : Java.Lang.Object
	{
	}

	public class DataManager : Java.Lang.Object
	{
	}
}

// --- Explicit [Register] on user type ---

namespace UserApp
{
	[Register ("com/example/userapp/CustomView")]
	public class CustomView : Android.Views.View
	{
		protected CustomView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

// --- Interface implementation ---

namespace UserApp.Listeners
{
	public class MyClickListener : Java.Lang.Object, Android.Views.View.IOnClickListener
	{
		public void OnClick (Android.Views.View? v)
		{
		}
	}
}

// --- [Export] method ---

namespace UserApp
{
	public class ExportedMethodHolder : Java.Lang.Object
	{
		[Export ("doWork")]
		public void DoWork ()
		{
		}
	}
}
