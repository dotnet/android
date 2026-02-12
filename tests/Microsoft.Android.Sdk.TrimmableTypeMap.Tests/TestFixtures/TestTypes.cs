// Test fixture types that exercise all scanner code paths.
// Each type is annotated with comments explaining which classification
// and behavior the scanner should produce.

using System;
using Android.App;
using Android.Content;
using Android.Runtime;

namespace Java.Lang
{
	[Register ("java/lang/Object", DoNotGenerateAcw = true)]
	public class Object
	{
		public Object ()
		{
		}

		protected Object (IntPtr handle, JniHandleOwnership transfer)
		{
		}
	}
}

namespace Java.Lang
{
	[Register ("java/lang/Throwable", DoNotGenerateAcw = true)]
	public class Throwable : Java.Lang.Object
	{
		protected Throwable (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("getMessage", "()Ljava/lang/String;", "GetGetMessageHandler")]
		public virtual string? Message { get; }
	}

	[Register ("java/lang/Exception", DoNotGenerateAcw = true)]
	public class Exception : Throwable
	{
		protected Exception (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Android.App
{
	[Register ("android/app/Activity", DoNotGenerateAcw = true)]
	public class Activity : Java.Lang.Object
	{
		public Activity ()
		{
		}

		protected Activity (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
		protected virtual void OnCreate (/* Bundle? */ object? savedInstanceState)
		{
		}

		[Register ("onStart", "()V", "")]
		protected virtual void OnStart ()
		{
		}
	}

	[Register ("android/app/Service", DoNotGenerateAcw = true)]
	public class Service : Java.Lang.Object
	{
		protected Service (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Android.Content
{
	[Register ("android/content/Context", DoNotGenerateAcw = true)]
	public class Context : Java.Lang.Object
	{
		protected Context (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Android.Views
{
	[Register ("android/view/View", DoNotGenerateAcw = true)]
	public class View : Java.Lang.Object
	{
		protected View (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Android.Widget
{
	[Register ("android/widget/Button", DoNotGenerateAcw = true)]
	public class Button : Android.Views.View
	{
		protected Button (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	[Register ("android/widget/TextView", DoNotGenerateAcw = true)]
	public class TextView : Android.Views.View
	{
		protected TextView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Android.Views
{
	[Register ("android/view/View$OnClickListener", "", "Android.Views.IOnClickListenerInvoker")]
	public interface IOnClickListener
	{
		[Register ("onClick", "(Landroid/view/View;)V", "GetOnClick_Landroid_view_View_Handler:Android.Views.IOnClickListenerInvoker")]
		void OnClick (View v);
	}

	// Invoker types ARE internal implementation details.
	// In real Mono.Android.dll, invokers DO have [Register] with DoNotGenerateAcw=true
	// and the SAME JNI name as their interface.
	// The scanner includes them — generators filter them later.
	[Register ("android/view/View$OnClickListener", DoNotGenerateAcw = true)]
	internal sealed class IOnClickListenerInvoker : Java.Lang.Object, IOnClickListener
	{
		public IOnClickListenerInvoker (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public void OnClick (View v)
		{
		}
	}
}

namespace MyApp
{
	// User types get their JNI name from [Activity(Name = "...")]
	// NOT from [Register] — that's only on MCW binding types.
	[Activity (MainLauncher = true, Label = "My App", Name = "my.app.MainActivity")]
	public class MainActivity : Android.App.Activity
	{
		public MainActivity ()
		{
		}

		[Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
		protected override void OnCreate (object? savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
		}
	}

	// Activity with intent filters, metadata, layout, and property attributes
	[Activity (Name = "my.app.DeepLinkActivity", Theme = "@style/AppTheme", Exported = true)]
	[IntentFilter (
		new [] { "android.intent.action.VIEW" },
		Categories = new [] { "android.intent.category.DEFAULT", "android.intent.category.BROWSABLE" },
		DataScheme = "https",
		DataHost = "example.com",
		DataPathPrefix = "/deep",
		AutoVerify = true)]
	[IntentFilter (
		new [] { "my.app.CUSTOM_ACTION" },
		Categories = new [] { "android.intent.category.DEFAULT" })]
	[MetaData ("com.google.android.geo.API_KEY", Value = "test-api-key")]
	[MetaData ("com.google.android.gms.version", Resource = "@integer/google_play_services_version")]
	[Layout (DefaultWidth = "500dp", DefaultHeight = "600dp", Gravity = "center", MinWidth = "300dp", MinHeight = "400dp")]
	[Property ("custom.prop", Value = "custom-value")]
	public class DeepLinkActivity : Android.App.Activity
	{
		public DeepLinkActivity ()
		{
		}
	}

	// Abstract activity — should be skipped by manifest generation
	[Activity (Name = "my.app.BaseActivity")]
	public abstract class BaseActivity : Android.App.Activity
	{
		protected BaseActivity ()
		{
		}
	}

	// Activity without public parameterless constructor — should trigger XA4213
	[Activity (Name = "my.app.NoDefaultCtorActivity")]
	public class NoDefaultCtorActivity : Android.App.Activity
	{
		readonly string _arg;

		public NoDefaultCtorActivity (string arg)
		{
			_arg = arg;
		}
	}

	// User type without component attribute: TRIMMABLE
	[Register ("my/app/MyHelper")]
	public class MyHelper : Java.Lang.Object
	{
		[Register ("doSomething", "()V", "GetDoSomethingHandler")]
		public virtual void DoSomething ()
		{
		}
	}

	// User service with rich attributes
	[Service (Name = "my.app.MyService", Exported = true, Permission = "my.app.BIND_SERVICE", IsolatedProcess = true)]
	[IntentFilter (new [] { "my.app.START_SERVICE" })]
	[MetaData ("service.version", Value = "1")]
	public class MyService : Android.App.Service
	{
		protected MyService (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	// User broadcast receiver with attributes
	[BroadcastReceiver (Name = "my.app.MyReceiver", Exported = true, Permission = "my.app.RECEIVE_BROADCAST")]
	[IntentFilter (new [] { "android.intent.action.BOOT_COMPLETED" })]
	public class MyReceiver : Java.Lang.Object
	{
	}

	// User content provider with grant URI permissions
	[ContentProvider (new [] { "my.app.provider" }, Name = "my.app.MyProvider", Exported = true, GrantUriPermissions = true)]
	[GrantUriPermission (Path = "/data")]
	[GrantUriPermission (PathPrefix = "/files")]
	[MetaData ("provider.meta", Value = "meta-value")]
	public class MyProvider : Java.Lang.Object
	{
	}
}

namespace MyApp.Generic
{
	[Register ("my/app/GenericHolder")]
	public class GenericHolder<T> : Java.Lang.Object where T : Java.Lang.Object
	{
		[Register ("getItem", "()Ljava/lang/Object;", "GetGetItemHandler")]
		public virtual T? GetItem ()
		{
			return default;
		}
	}
}

namespace MyApp
{
	[Register ("my/app/AbstractBase")]
	public abstract class AbstractBase : Java.Lang.Object
	{
		protected AbstractBase (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("doWork", "()V", "")]
		public abstract void DoWork ();
	}
}

namespace MyApp
{
	[Register ("my/app/SimpleActivity")]
	public class SimpleActivity : Android.App.Activity
	{
		// No (IntPtr, JniHandleOwnership) ctor — scanner should
		// resolve to Activity's activation ctor
	}
}

namespace MyApp
{
	[Register ("my/app/ClickableView")]
	public class ClickableView : Android.Views.View, Android.Views.IOnClickListener
	{
		protected ClickableView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v)
		{
		}
	}
}

namespace MyApp
{
	[Register ("my/app/CustomView")]
	public class CustomView : Android.Views.View
	{
		protected CustomView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("<init>", "()V", "")]
		public CustomView ()
			: base (default!, default)
		{
		}

		[Register ("<init>", "(Landroid/content/Context;)V", "")]
		public CustomView (Context context)
			: base (default!, default)
		{
		}
	}
}

namespace MyApp
{
	[Register ("my/app/Outer")]
	public class Outer : Java.Lang.Object
	{
		[Register ("my/app/Outer$Inner")]
		public class Inner : Java.Lang.Object
		{
			protected Inner (IntPtr handle, JniHandleOwnership transfer)
				: base (handle, transfer)
			{
			}
		}
	}
}

namespace MyApp
{
	[Register ("my/app/ICallback", "", "MyApp.ICallbackInvoker")]
	public interface ICallback
	{
		[Register ("my/app/ICallback$Result")]
		public class Result : Java.Lang.Object
		{
			protected Result (IntPtr handle, JniHandleOwnership transfer)
				: base (handle, transfer)
			{
			}
		}
	}
}

namespace MyApp
{
	[Register ("my/app/TouchHandler")]
	public class TouchHandler : Java.Lang.Object
	{
		// bool return type (non-blittable, needs byte wrapper in UCO)
		[Register ("onTouch", "(Landroid/view/View;I)Z", "GetOnTouchHandler")]
		public virtual bool OnTouch (Android.Views.View v, int action)
		{
			return false;
		}

		// bool parameter (non-blittable)
		[Register ("onFocusChange", "(Landroid/view/View;Z)V", "GetOnFocusChangeHandler")]
		public virtual void OnFocusChange (Android.Views.View v, bool hasFocus)
		{
		}

		// Multiple params of different JNI types
		[Register ("onScroll", "(IFJD)V", "GetOnScrollHandler")]
		public virtual void OnScroll (int x, float y, long timestamp, double velocity)
		{
		}

		// Object return type
		[Register ("getText", "()Ljava/lang/String;", "GetGetTextHandler")]
		public virtual string? GetText ()
		{
			return null;
		}

		// Array parameter
		[Register ("setItems", "([Ljava/lang/String;)V", "GetSetItemsHandler")]
		public virtual void SetItems (string[]? items)
		{
		}
	}
}

namespace MyApp
{
	[Register ("my/app/ExportExample")]
	public class ExportExample : Java.Lang.Object
	{
		[Java.Interop.Export ("myExportedMethod")]
		public void MyExportedMethod ()
		{
		}
	}

	[Register ("my/app/ExportWithThrows")]
	public class ExportWithThrows : Java.Lang.Object
	{
		[Java.Interop.Export ("riskyMethod", ThrownNames = new [] { "java.io.IOException", "java.lang.IllegalStateException" })]
		public void RiskyMethod ()
		{
		}
	}
}

namespace Android.App.Backup
{
	[Register ("android/app/backup/BackupAgent", DoNotGenerateAcw = true)]
	public class BackupAgent : Java.Lang.Object
	{
		protected BackupAgent (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace MyApp
{
	[Application (Name = "my.app.MyApplication", BackupAgent = typeof (MyBackupAgent), ManageSpaceActivity = typeof (MyManageSpaceActivity), Theme = "@style/AppTheme", Debuggable = true, AllowBackup = true, SupportsRtl = true, Label = "My Application", Icon = "@mipmap/ic_launcher")]
	[MetaData ("app.version", Value = "2.0")]
	public class MyApplication : Java.Lang.Object
	{
	}

	[Instrumentation (Name = "my.app.MyInstrumentation", TargetPackage = "my.app", FunctionalTest = true, HandleProfiling = true, Label = "Test Runner")]
	public class MyInstrumentation : Java.Lang.Object
	{
	}

	// BackupAgent without a component attribute — would normally be trimmable,
	// but [Application(BackupAgent = typeof(...))] should force it unconditional.
	[Register ("my/app/MyBackupAgent")]
	public class MyBackupAgent : Android.App.Backup.BackupAgent
	{
		protected MyBackupAgent (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	// Activity without [Activity] attribute — would normally be trimmable,
	// but [Application(ManageSpaceActivity = typeof(...))] should force it unconditional.
	[Register ("my/app/MyManageSpaceActivity")]
	public class MyManageSpaceActivity : Android.App.Activity
	{
		protected MyManageSpaceActivity (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace MyApp
{
	[Register ("my/app/MyButton")]
	public class MyButton : Android.Widget.Button
	{
		protected MyButton (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

namespace Android.Views
{
	[Register ("android/view/View$OnLongClickListener", "", "Android.Views.IOnLongClickListenerInvoker")]
	public interface IOnLongClickListener
	{
		[Register ("onLongClick", "(Landroid/view/View;)Z", "GetOnLongClick_Landroid_view_View_Handler:Android.Views.IOnLongClickListenerInvoker")]
		bool OnLongClick (View v);
	}
}

namespace MyApp
{
	[Register ("my/app/MultiInterfaceView")]
	public class MultiInterfaceView : Android.Views.View, Android.Views.IOnClickListener, Android.Views.IOnLongClickListener
	{
		protected MultiInterfaceView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v) { }

		[Register ("onLongClick", "(Landroid/view/View;)Z", "")]
		public bool OnLongClick (Android.Views.View v) { return false; }
	}
}
