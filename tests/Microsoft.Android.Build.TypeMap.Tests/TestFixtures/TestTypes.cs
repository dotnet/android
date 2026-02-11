// Test fixture types that exercise all scanner code paths.
// Each type is annotated with comments explaining which classification
// and behavior the scanner should produce.

using System;
using Android.App;
using Android.Content;
using Android.Runtime;

// ============================================================
// Base type: simulates Java.Lang.Object
// ============================================================
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

// ============================================================
// Exception/Throwable hierarchy (DoNotGenerateAcw = true)
// These are MCW bindings for Java exception types.
// ============================================================
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

// ============================================================
// MCW binding types (DoNotGenerateAcw = true)
// These get TypeMap entries but NO JCW/RegisterNatives.
// ============================================================
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

// ============================================================
// Interface types (trimmable, no JCW, no RegisterNatives)
// Invoker types should be INCLUDED in scanner output (filtered by generators later)
// ============================================================
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

// ============================================================
// User types (unconditional: has [Activity])
// Gets JCW + RegisterNatives
// ============================================================
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

	// User type without component attribute: TRIMMABLE
	[Register ("my/app/MyHelper")]
	public class MyHelper : Java.Lang.Object
	{
		[Register ("doSomething", "()V", "GetDoSomethingHandler")]
		public virtual void DoSomething ()
		{
		}
	}

	// User service: UNCONDITIONAL — gets JNI name from [Service(Name = "...")]
	[Service (Name = "my.app.MyService")]
	public class MyService : Android.App.Service
	{
		protected MyService (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	// User broadcast receiver: UNCONDITIONAL — gets JNI name from [BroadcastReceiver(Name = "...")]
	[BroadcastReceiver (Name = "my.app.MyReceiver")]
	public class MyReceiver : Java.Lang.Object
	{
	}

	// User content provider: UNCONDITIONAL — gets JNI name from [ContentProvider(Name = "...")]
	[ContentProvider (new [] { "my.app.provider" }, Name = "my.app.MyProvider")]
	public class MyProvider : Java.Lang.Object
	{
	}
}

// ============================================================
// Generic types (trimmable, gets TypeMap entry for open definition)
// CreateInstance should throw NotSupportedException
// ============================================================
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

// ============================================================
// Abstract type (trimmable, no JCW since abstract)
// ============================================================
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

// ============================================================
// Type without its own activation ctor — should inherit from base
// ============================================================
namespace MyApp
{
	[Register ("my/app/SimpleActivity")]
	public class SimpleActivity : Android.App.Activity
	{
		// No (IntPtr, JniHandleOwnership) ctor — scanner should
		// resolve to Activity's activation ctor
	}
}

// ============================================================
// Type implementing a Java interface (tests ImplementedInterfaceJavaNames)
// ============================================================
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

// ============================================================
// Type with registered constructors (tests JavaConstructors)
// ============================================================
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

// ============================================================
// Nested type within another Java peer (inner class)
// e.g. Java: android.view.View$OnClickListener
// ============================================================
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

// ============================================================
// Nested type within a Java interface
// ============================================================
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

// ============================================================
// Marshal methods with non-blittable types (bool)
// and various JNI signatures
// ============================================================
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

// ============================================================
// Method with [Export] attribute (no connector string)
// ============================================================
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
}

// ============================================================
// [Application] and [Instrumentation] attributes
// ============================================================
namespace MyApp
{
	[Application (Name = "my.app.MyApplication")]
	public class MyApplication : Java.Lang.Object
	{
	}

	[Instrumentation (Name = "my.app.MyInstrumentation")]
	public class MyInstrumentation : Java.Lang.Object
	{
	}
}

// ============================================================
// Deep hierarchy: View → Button → MyButton (3+ levels)
// ============================================================
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

// ============================================================
// Type implementing multiple Java interfaces
// ============================================================
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
