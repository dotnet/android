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

namespace UserApp.Providers
{
	[ContentProvider (Name = "com.example.userapp.SettingsProvider")]
	public class SettingsProvider : ContentProvider
	{
		public override bool OnCreate () => true;

		public override int Delete (Android.Net.Uri? uri, string? selection, string[]? selectionArgs) => 0;

		public override string? GetType (Android.Net.Uri? uri) => null;

		public override Android.Net.Uri? Insert (Android.Net.Uri? uri, ContentValues? values) => null;

		public override Android.Database.ICursor? Query (Android.Net.Uri? uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder) => null;

		public override int Update (Android.Net.Uri? uri, ContentValues? values, string? selection, string[]? selectionArgs) => 0;
	}
}

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

namespace UserApp.Interfaces
{
	[Register ("com/example/userapp/IWidgetListener", "", "UserApp.Interfaces.IWidgetListenerInvoker")]
	public interface IWidgetListener
	{
		[Register ("onWidgetChanged", "(Ljava/lang/String;)V", "GetOnWidgetChanged_Ljava_lang_String_Handler:UserApp.Interfaces.IWidgetListenerInvoker")]
		void OnWidgetChanged (string? value);
	}

	[Register ("com/example/userapp/IWidgetListener", DoNotGenerateAcw = true)]
	internal sealed class IWidgetListenerInvoker : Java.Lang.Object, IWidgetListener
	{
		public IWidgetListenerInvoker (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public void OnWidgetChanged (string? value)
		{
		}
	}
}

namespace UserApp.AbstractWidgets
{
	[Register ("com/example/userapp/AbstractWidget")]
	public abstract class AbstractWidget : Java.Lang.Object
	{
		protected AbstractWidget (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("performAction", "()V", "GetPerformActionHandler")]
		public abstract void PerformAction ();
	}

	[Register ("com/example/userapp/AbstractWidget", DoNotGenerateAcw = true)]
	internal sealed class AbstractWidgetInvoker : AbstractWidget
	{
		public AbstractWidgetInvoker (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public override void PerformAction ()
		{
		}
	}
}

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

namespace UserApp.Listeners
{
	public class MyClickListener : Java.Lang.Object, Android.Views.View.IOnClickListener
	{
		public void OnClick (Android.Views.View? v)
		{
		}
	}
}

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
