using System;
using Android.App;
using Android.Content;
using Android.Runtime;

namespace Java.Lang
{
	[Register ("java/lang/Object", DoNotGenerateAcw = true)]
	public class Object
	{
		public Object () { }
		protected Object (IntPtr handle, JniHandleOwnership transfer) { }
	}

	[Register ("java/lang/Throwable", DoNotGenerateAcw = true)]
	public class Throwable : Object
	{
		protected Throwable (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("getMessage", "()Ljava/lang/String;", "GetGetMessageHandler")]
		public virtual string? Message { get; }
	}

	[Register ("java/lang/Exception", DoNotGenerateAcw = true)]
	public class Exception : Throwable
	{
		protected Exception (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

namespace Android.App
{
	[Register ("android/app/Activity", DoNotGenerateAcw = true)]
	public class Activity : Java.Lang.Object
	{
		public Activity () { }
		protected Activity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
		protected virtual void OnCreate (object? savedInstanceState) { }

		[Register ("onStart", "()V", "")]
		protected virtual void OnStart () { }
	}

	[Register ("android/app/Service", DoNotGenerateAcw = true)]
	public class Service : Java.Lang.Object
	{
		protected Service (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

namespace Android.App.Backup
{
	[Register ("android/app/backup/BackupAgent", DoNotGenerateAcw = true)]
	public class BackupAgent : Java.Lang.Object
	{
		protected BackupAgent (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

namespace Android.Content
{
	[Register ("android/content/Context", DoNotGenerateAcw = true)]
	public class Context : Java.Lang.Object
	{
		protected Context (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

namespace Android.Views
{
	[Register ("android/view/View", DoNotGenerateAcw = true)]
	public class View : Java.Lang.Object
	{
		protected View (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("android/view/View$OnClickListener", "", "Android.Views.IOnClickListenerInvoker")]
	public interface IOnClickListener
	{
		[Register ("onClick", "(Landroid/view/View;)V", "GetOnClick_Landroid_view_View_Handler:Android.Views.IOnClickListenerInvoker")]
		void OnClick (View v);
	}

	[Register ("android/view/View$OnClickListener", DoNotGenerateAcw = true)]
	internal sealed class IOnClickListenerInvoker : Java.Lang.Object, IOnClickListener
	{
		public IOnClickListenerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
		public void OnClick (View v) { }
	}

	[Register ("android/view/View$OnLongClickListener", "", "Android.Views.IOnLongClickListenerInvoker")]
	public interface IOnLongClickListener
	{
		[Register ("onLongClick", "(Landroid/view/View;)Z", "GetOnLongClick_Landroid_view_View_Handler:Android.Views.IOnLongClickListenerInvoker")]
		bool OnLongClick (View v);
	}
}

namespace Android.Widget
{
	[Register ("android/widget/Button", DoNotGenerateAcw = true)]
	public class Button : Android.Views.View
	{
		protected Button (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("android/widget/TextView", DoNotGenerateAcw = true)]
	public class TextView : Android.Views.View
	{
		protected TextView (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

namespace MyApp
{
	[Activity (MainLauncher = true, Label = "My App", Name = "my.app.MainActivity")]
	public class MainActivity : Android.App.Activity
	{
		public MainActivity () { }

		[Register ("onCreate", "(Landroid/os/Bundle;)V", "GetOnCreate_Landroid_os_Bundle_Handler")]
		protected override void OnCreate (object? savedInstanceState) => base.OnCreate (savedInstanceState);
	}

	[Register ("my/app/MyHelper")]
	public class MyHelper : Java.Lang.Object
	{
		[Register ("doSomething", "()V", "GetDoSomethingHandler")]
		public virtual void DoSomething () { }
	}

	[Service (Name = "my.app.MyService")]
	public class MyService : Android.App.Service
	{
		protected MyService (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[BroadcastReceiver (Name = "my.app.MyReceiver")]
	public class MyReceiver : Java.Lang.Object { }

	[ContentProvider (new [] { "my.app.provider" }, Name = "my.app.MyProvider")]
	public class MyProvider : Java.Lang.Object { }

	[Register ("my/app/AbstractBase")]
	public abstract class AbstractBase : Java.Lang.Object
	{
		protected AbstractBase (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("doWork", "()V", "")]
		public abstract void DoWork ();
	}

	[Register ("my/app/SimpleActivity")]
	public class SimpleActivity : Android.App.Activity { }

	[Register ("my/app/ClickableView")]
	public class ClickableView : Android.Views.View, Android.Views.IOnClickListener
	{
		protected ClickableView (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v) { }
	}

	[Register ("my/app/CustomView")]
	public class CustomView : Android.Views.View
	{
		protected CustomView (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("<init>", "()V", "")]
		public CustomView () : base (default!, default) { }

		[Register ("<init>", "(Landroid/content/Context;)V", "")]
		public CustomView (Context context) : base (default!, default) { }
	}

	[Register ("my/app/Outer")]
	public class Outer : Java.Lang.Object
	{
		[Register ("my/app/Outer$Inner")]
		public class Inner : Java.Lang.Object
		{
			protected Inner (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
		}
	}

	[Register ("my/app/ICallback", "", "MyApp.ICallbackInvoker")]
	public interface ICallback
	{
		[Register ("my/app/ICallback$Result")]
		public class Result : Java.Lang.Object
		{
			protected Result (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
		}
	}

	[Register ("my/app/TouchHandler")]
	public class TouchHandler : Java.Lang.Object
	{
		[Register ("onTouch", "(Landroid/view/View;I)Z", "GetOnTouchHandler")]
		public virtual bool OnTouch (Android.Views.View v, int action) => false;

		[Register ("onFocusChange", "(Landroid/view/View;Z)V", "GetOnFocusChangeHandler")]
		public virtual void OnFocusChange (Android.Views.View v, bool hasFocus) { }

		[Register ("onScroll", "(IFJD)V", "GetOnScrollHandler")]
		public virtual void OnScroll (int x, float y, long timestamp, double velocity) { }

		[Register ("getText", "()Ljava/lang/String;", "GetGetTextHandler")]
		public virtual string? GetText () => null;

		[Register ("setItems", "([Ljava/lang/String;)V", "GetSetItemsHandler")]
		public virtual void SetItems (string[]? items) { }
	}

	[Register ("my/app/ExportExample")]
	public class ExportExample : Java.Lang.Object
	{
		[Java.Interop.Export ("myExportedMethod")]
		public void MyExportedMethod () { }
	}

	[Application (Name = "my.app.MyApplication", BackupAgent = typeof (MyBackupAgent), ManageSpaceActivity = typeof (MyManageSpaceActivity))]
	public class MyApplication : Java.Lang.Object { }

	[Instrumentation (Name = "my.app.MyInstrumentation")]
	public class MyInstrumentation : Java.Lang.Object { }

	[Register ("my/app/MyBackupAgent")]
	public class MyBackupAgent : Android.App.Backup.BackupAgent
	{
		protected MyBackupAgent (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("my/app/MyManageSpaceActivity")]
	public class MyManageSpaceActivity : Android.App.Activity
	{
		protected MyManageSpaceActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	public class UnregisteredHelper : Java.Lang.Object { }

	[Register ("my/app/MyButton")]
	public class MyButton : Android.Widget.Button
	{
		protected MyButton (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("my/app/MultiInterfaceView")]
	public class MultiInterfaceView : Android.Views.View, Android.Views.IOnClickListener, Android.Views.IOnLongClickListener
	{
		protected MultiInterfaceView (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v) { }

		[Register ("onLongClick", "(Landroid/view/View;)Z", "")]
		public bool OnLongClick (Android.Views.View v) => false;
	}

	[CustomJniName ("com.example.CustomWidget")]
	public class CustomWidget : Java.Lang.Object { }

	[Activity (Name = "my.app.BaseActivityNoRegister")]
	public class BaseActivityNoRegister : Android.App.Activity { }

	public class DerivedFromComponentBase : BaseActivityNoRegister { }

	[Register ("my/app/RegisteredParent")]
	public class RegisteredParent : Java.Lang.Object
	{
		public class UnregisteredChild : Java.Lang.Object { }
	}

	[Register ("my/app/DeepOuter")]
	public class DeepOuter : Java.Lang.Object
	{
		public class Middle : Java.Lang.Object
		{
			public class DeepInner : Java.Lang.Object { }
		}
	}

	public class PlainActivitySubclass : Android.App.Activity { }

	[Activity (Label = "Unnamed")]
	public class UnnamedActivity : Android.App.Activity { }

	public class UnregisteredClickListener : Java.Lang.Object, Android.Views.IOnClickListener
	{
		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v) { }
	}

	public class UnregisteredExporter : Java.Lang.Object
	{
		[Java.Interop.Export ("doExportedWork")]
		public void DoExportedWork () { }
	}
}

namespace MyApp.Generic
{
	[Register ("my/app/GenericHolder")]
	public class GenericHolder<T> : Java.Lang.Object where T : Java.Lang.Object
	{
		[Register ("getItem", "()Ljava/lang/Object;", "GetGetItemHandler")]
		public virtual T? GetItem () => default;
	}

	[Register ("my/app/GenericBase", DoNotGenerateAcw = true)]
	public class GenericBase<T> : Java.Lang.Object where T : class
	{
		protected GenericBase (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("my/app/ConcreteFromGeneric")]
	public class ConcreteFromGeneric : GenericBase<string>
	{
		protected ConcreteFromGeneric (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("my/app/IGenericCallback", "", "")]
	public interface IGenericCallback<T> { }

	[Register ("my/app/GenericCallbackImpl")]
	public class GenericCallbackImpl : Java.Lang.Object, IGenericCallback<string>
	{
		protected GenericCallbackImpl (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

[Register ("my/app/GlobalType")]
public class GlobalType : Java.Lang.Object
{
	protected GlobalType (IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base (handle, transfer) { }
}

public class GlobalUnregisteredType : Java.Lang.Object { }
