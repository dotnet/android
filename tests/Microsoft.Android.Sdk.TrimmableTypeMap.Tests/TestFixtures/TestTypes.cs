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

	[Register ("java/lang/CharSequence", DoNotGenerateAcw = true)]
	public interface ICharSequence
	{
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

	// User type WITHOUT [Register] — gets CRC64-computed JNI name.
	// CompatJniName should use raw namespace instead of CRC64.
	public class UnregisteredHelper : Java.Lang.Object
	{
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

	// Implementor types are generated by the binding generator.
	// They are NOT DoNotGenerateAcw because they ARE ACW types, but they should still
	// be trimmable because they are only instantiated from .NET (e.g., when subscribing to an event).
	[Register ("android/view/View_IOnClickListenerImplementor")]
	public class IOnClickListenerImplementor : Java.Lang.Object, IOnClickListener
	{
		public IOnClickListenerImplementor ()
		{
		}

		protected IOnClickListenerImplementor (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (View v)
		{
		}
	}

	// EventDispatcher types are used for the event-based pattern in Android bindings.
	// Like Implementor types, they should be trimmable.
	[Register ("android/view/View_ClickEventDispatcher")]
	public class ClickEventDispatcher : Java.Lang.Object
	{
		public ClickEventDispatcher ()
		{
		}

		protected ClickEventDispatcher (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("dispatch", "()V", "")]
		public void Dispatch ()
		{
		}
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

	// User type with a custom IJniNameProviderAttribute — the scanner
	// should detect this via interface resolution, not hardcoded attribute names.
	[CustomJniName ("com.example.CustomWidget")]
	public class CustomWidget : Java.Lang.Object
	{
	}
}

// ================================================================
// Edge case: generic base type (TypeSpecification resolution)
// ================================================================
namespace MyApp.Generic
{
	[Register ("my/app/GenericBase", DoNotGenerateAcw = true)]
	public class GenericBase<T> : Java.Lang.Object where T : class
	{
		protected GenericBase (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}

	[Register ("my/app/ConcreteFromGeneric")]
	public class ConcreteFromGeneric : GenericBase<string>
	{
		protected ConcreteFromGeneric (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

// ================================================================
// Edge case: generic interface (TypeSpecification resolution)
// ================================================================
namespace MyApp.Generic
{
	[Register ("my/app/IGenericCallback", "", "")]
	public interface IGenericCallback<T>
	{
	}

	[Register ("my/app/GenericCallbackImpl")]
	public class GenericCallbackImpl : Java.Lang.Object, IGenericCallback<string>
	{
		protected GenericCallbackImpl (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}
	}
}

// ================================================================
// Edge case: component-only base detection
// ================================================================
namespace MyApp
{
	[Activity (Name = "my.app.BaseActivityNoRegister")]
	public class BaseActivityNoRegister : Android.App.Activity
	{
	}

	public class DerivedFromComponentBase : BaseActivityNoRegister
	{
	}
}

// ================================================================
// Edge case: unregistered nested type inside [Register] parent
// ================================================================
namespace MyApp
{
	[Register ("my/app/RegisteredParent")]
	public class RegisteredParent : Java.Lang.Object
	{
		public class UnregisteredChild : Java.Lang.Object
		{
		}
	}
}

// ================================================================
// Edge case: 3-level deep nesting
// ComputeTypeNameParts must walk multiple levels, collecting names.
// ================================================================
namespace MyApp
{
	[Register ("my/app/DeepOuter")]
	public class DeepOuter : Java.Lang.Object
	{
		public class Middle : Java.Lang.Object
		{
			public class DeepInner : Java.Lang.Object
			{
			}
		}
	}
}

// ================================================================
// Edge case: plain Java peer subclass — no [Register], no component attribute
// ExtendsJavaPeer must detect it via base type chain, gets CRC64 name.
// ================================================================
namespace MyApp
{
	public class PlainActivitySubclass : Android.App.Activity
	{
	}
}

// ================================================================
// Edge case: component attribute WITHOUT Name property
// HasComponentAttribute = true but ComponentAttributeJniName = null.
// Type should still get a CRC64 JNI name (not null).
// ================================================================
namespace MyApp
{
	[Activity (Label = "Unnamed")]
	public class UnnamedActivity : Android.App.Activity
	{
	}
}

// ================================================================
// Edge case: interface implementation on unregistered type
// Type gets CRC64 JNI name but still resolves interface names.
// ================================================================
namespace MyApp
{
	public class UnregisteredClickListener : Java.Lang.Object, Android.Views.IOnClickListener
	{
		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v)
		{
		}
	}
}

// ================================================================
// Edge case: [Export] method on unregistered type
// ParseExportAttribute runs on a type that gets CRC64 JNI name.
// ================================================================
namespace MyApp
{
	public class UnregisteredExporter : Java.Lang.Object
	{
		[Java.Interop.Export ("doExportedWork")]
		public void DoExportedWork ()
		{
		}
	}
}

// ================================================================
// Edge case: type in empty namespace
// ================================================================
[Register ("my/app/GlobalType")]
public class GlobalType : Java.Lang.Object
{
	protected GlobalType (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
		: base (handle, transfer)
	{
	}
}

public class GlobalUnregisteredType : Java.Lang.Object
{
}

// ================================================================
// [Export] constructor scenarios — ported from legacy SupportDeclarations.cs
// ================================================================
namespace MyApp
{
	public enum ExportSampleEnum
	{
		None,
		One,
	}

	/// <summary>
	/// Type with [Export] constructors (no [Register] on ctors).
	/// Legacy JCW: TypeManager.Activate pattern, not nctor_N.
	/// </summary>
	[Register ("my/app/ExportsConstructors")]
	public class ExportsConstructors : Java.Lang.Object
	{
		protected ExportsConstructors (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.Export]
		public ExportsConstructors () { }

		[Java.Interop.Export]
		public ExportsConstructors (int value) { }
	}

	/// <summary>
	/// Type with [Export] constructors that throw.
	/// </summary>
	[Register ("my/app/ExportsThrowsConstructors")]
	public class ExportsThrowsConstructors : Java.Lang.Object
	{
		protected ExportsThrowsConstructors (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.Export (ThrownNames = new [] { "java.lang.Throwable" })]
		public ExportsThrowsConstructors () { }

		[Java.Interop.Export (ThrownNames = new [] { "java.lang.Throwable" })]
		public ExportsThrowsConstructors (int value) { }

		[Java.Interop.Export]
		public ExportsThrowsConstructors (string value) { }
	}

	/// <summary>
	/// Type with [Export] methods with parameters (not just parameterless).
	/// Ported from legacy ExportsMembers.
	/// </summary>
	[Register ("my/app/ExportMethodWithParams")]
	public class ExportMethodWithParams : Java.Lang.Object
	{
		protected ExportMethodWithParams (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.Export ("doWork")]
		public void DoWork (int count) { }

		[Java.Interop.Export ("computeName")]
		public string ComputeName (string prefix, int index) { return ""; }
	}

	/// <summary>
	/// Complex [Export] marshal scenarios: arrays, enums, and CharSequence.
	/// </summary>
	[Register ("my/app/ExportMarshalComplex")]
	public class ExportMarshalComplex : Java.Lang.Object
	{
		protected ExportMarshalComplex (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.Export ("mutateInts")]
		public void MutateInts (int[] values) { }

		[Java.Interop.Export ("roundTripEnum")]
		public ExportSampleEnum RoundTripEnum (ExportSampleEnum value) { return value; }

		[Java.Interop.Export ("echoCharSequence")]
		public Java.Lang.ICharSequence EchoCharSequence (Java.Lang.ICharSequence value) { return value; }

		[Java.Interop.Export ("echoViews")]
		public Android.Views.View[] EchoViews (Android.Views.View[] values) { return values; }

		[Java.Interop.Export ("echoStrings")]
		public string[] EchoStrings (string[] values) { return values; }
	}

	/// <summary>
	/// Comprehensive [Export] member scenarios ported from legacy ExportsMembers.
	/// Tests: name override, throws, empty throws, static methods.
	/// </summary>
	[Register ("my/app/ExportMembersComprehensive")]
	public class ExportMembersComprehensive : Java.Lang.Object
	{
		protected ExportMembersComprehensive (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.Export]
		public void methodNamesNotMangled () { }

		[Java.Interop.Export ("attributeOverridesNames")]
		public string CompletelyDifferentName (string value, int count) { return value; }

		[Java.Interop.Export (ThrownNames = new [] { "java.lang.Throwable" })]
		public void methodThatThrows () { }

		[Java.Interop.Export (ThrownNames = new string [0])]
		public void methodThatThrowsEmptyArray () { }
	}

	/// <summary>
	/// [Export] constructor with SuperArgumentsString.
	/// The super() call should use the custom args, not forward all params.
	/// </summary>
	[Register ("my/app/ExportCtorWithSuperArgs")]
	public class ExportCtorWithSuperArgs : Java.Lang.Object
	{
		protected ExportCtorWithSuperArgs (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.Export (SuperArgumentsString = "")]
		public ExportCtorWithSuperArgs (int value) { }
	}

	/// <summary>
	/// Static [Export] method and [ExportField] declarations.
	/// </summary>
	[Register ("my/app/ExportStaticAndFields")]
	public class ExportStaticAndFields : Java.Lang.Object
	{
		protected ExportStaticAndFields (IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Java.Interop.ExportField ("STATIC_INSTANCE")]
		public static ExportStaticAndFields GetInstance ()
		{
			return null!;
		}

		[Java.Interop.ExportField ("VALUE")]
		public string GetValue ()
		{
			return "value";
		}

		[Java.Interop.Export]
		public static void staticMethodNotMangled ()
		{
		}

		[Java.Interop.Export]
		public void instanceMethod () { }
	}
}
