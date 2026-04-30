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
		[Register (".ctor", "()V", "")]
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

	[Register ("android/app/Application", DoNotGenerateAcw = true)]
	public class Application : Java.Lang.Object
	{
		public Application () { }
		protected Application (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("android/app/Instrumentation", DoNotGenerateAcw = true)]
	public class Instrumentation : Java.Lang.Object
	{
		public Instrumentation () { }
		protected Instrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
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

	/// <summary>
	/// Interface with a registered property (for testing interface property implementation detection).
	/// </summary>
	[Register ("android/view/View$IHasName", "", "Android.Views.IHasNameInvoker")]
	public interface IHasName
	{
		[Register ("getName", "()Ljava/lang/String;", "GetGetNameHandler:Android.Views.IHasNameInvoker, TestFixtures, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")]
		string? Name { get; }
	}

	/// <summary>
	/// Interface that extends another registered interface.
	/// Tests that implementing types get methods from the parent interface too.
	/// </summary>
	[Register ("android/view/View$INamedClickListener", "", "Android.Views.INamedClickListenerInvoker")]
	public interface INamedClickListener : IOnClickListener
	{
		[Register ("getLabel", "()Ljava/lang/String;", "GetGetLabelHandler:Android.Views.INamedClickListenerInvoker")]
		string? Label { get; }
	}

	[Register ("mono/android/view/View_IOnClickListenerImplementor")]
	public class View_IOnClickListenerImplementor : Java.Lang.Object
	{
		public View_IOnClickListenerImplementor (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	[Register ("mono/android/view/View_ClickEventDispatcher")]
	public class View_ClickEventDispatcher : Java.Lang.Object
	{
		public View_ClickEventDispatcher (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
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
		public CustomView () : base (default, default) { }

		[Register ("<init>", "(Landroid/content/Context;)V", "")]
		public CustomView (Context context) : base (default, default) { }
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

	// --- Covariant return test types ---

	/// <summary>
	/// Base type with a method returning Java.Lang.Object.
	/// </summary>
	[Register ("my/app/CovariantBase", DoNotGenerateAcw = true)]
	public class CovariantBase : Java.Lang.Object
	{
		protected CovariantBase (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("getResult", "()Ljava/lang/Object;", "GetGetResultHandler")]
		public virtual Java.Lang.Object? GetResult () => null;
	}

	/// <summary>
	/// Derived type that overrides GetResult with a narrower C# return type.
	/// The JCW should use the base's JNI signature "()Ljava/lang/Object;".
	/// </summary>
	[Register ("my/app/CovariantDerived")]
	public class CovariantDerived : CovariantBase
	{
		protected CovariantDerived (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// C# allows covariant returns — return type narrows from Object to string
		// but no [Register] on the override. The base's JNI sig should be used.
		public override Java.Lang.Object? GetResult () => null;
	}

	[Register ("my/app/ExportExample")]
	public class ExportExample : Java.Lang.Object
	{
		[Java.Interop.Export ("myExportedMethod")]
		public void MyExportedMethod () { }
	}

	/// <summary>
	/// Has [Export] methods with non-primitive Java-bound parameter types.
	/// The JCW should resolve parameter types via [Register] instead of falling back to Object.
	/// </summary>
	[Register ("my/app/ExportWithJavaBoundParams")]
	public class ExportWithJavaBoundParams : Java.Lang.Object
	{
		[Java.Interop.Export ("processView")]
		public void ProcessView (Android.Views.View view) { }

		[Java.Interop.Export ("handleClick")]
		public bool HandleClick (Android.Views.View view, int action) { return false; }

		[Java.Interop.Export ("getViewName")]
		public string GetViewName (Android.Views.View view) { return ""; }
	}

	/// <summary>
	/// Has [Export] methods with different access modifiers.
	/// The JCW should respect the C# visibility for [Export] methods.
	/// </summary>
	[Register ("my/app/ExportAccessTest")]
	public class ExportAccessTest : Java.Lang.Object
	{
		protected ExportAccessTest (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Java.Interop.Export ("publicMethod")]
		public void PublicMethod () { }

		[Java.Interop.Export ("protectedMethod")]
		protected void ProtectedMethod () { }
	}

	[Register ("my/app/BaseApplication")]
	public abstract class BaseApplication : Android.App.Application { }

	[Application (Name = "my.app.MyApplication", BackupAgent = typeof (MyBackupAgent), ManageSpaceActivity = typeof (MyManageSpaceActivity))]
	public class MyApplication : BaseApplication { }

	/// <summary>
	/// Has [ExportField] methods that should produce Java field declarations.
	/// </summary>
	[Register ("my/app/ExportFieldExample")]
	public class ExportFieldExample : Java.Lang.Object
	{
		protected ExportFieldExample (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Java.Interop.ExportField ("STATIC_INSTANCE")]
		public static ExportFieldExample? GetInstance () => default;

		[Java.Interop.ExportField ("VALUE")]
		public string GetValue () => "";
	}

	[Register ("my/app/BaseInstrumentation")]
	public abstract class BaseInstrumentation : Android.App.Instrumentation { }

	[Register ("my/app/IntermediateInstrumentation")]
	public abstract class IntermediateInstrumentation : BaseInstrumentation { }

	[Instrumentation (Name = "my.app.MyInstrumentation")]
	public class MyInstrumentation : IntermediateInstrumentation { }

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

	// --- Constructor super() argument test types ---

	/// <summary>
	/// Has a ctor with custom params that don't match any base registered ctor.
	/// Activity has parameterless [Register(".ctor","()V",...)] so the fallback
	/// should produce super() (empty super args).
	/// </summary>
	[Register ("my/app/CustomParamActivity")]
	public class CustomParamActivity : Android.App.Activity
	{
		protected CustomParamActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// Custom ctor with params that don't match Activity's ()V ctor
		public CustomParamActivity (string title, int count) : base () { }
	}

	// --- Interface implementation without [Register] test types ---
	// These mimic real user code where a class implements a Java interface
	// but doesn't have [Register] on the implementing method.

	/// <summary>
	/// Implements IOnClickListener.OnClick without [Register] on the method.
	/// The scanner must detect this from the interface definition.
	/// </summary>
	[Register ("my/app/ImplicitClickListener")]
	public class ImplicitClickListener : Java.Lang.Object, Android.Views.IOnClickListener
	{
		protected ImplicitClickListener (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// No [Register] — real user code doesn't have it
		public void OnClick (Android.Views.View v) { }
	}

	/// <summary>
	/// Implements an interface with a registered property without [Register] on the property.
	/// </summary>
	[Register ("my/app/ImplicitPropertyImpl")]
	public class ImplicitPropertyImpl : Java.Lang.Object, Android.Views.IHasName
	{
		protected ImplicitPropertyImpl (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// No [Register] — should be detected from interface property
		public string? Name => "test";
	}

	/// <summary>
	/// Implements multiple interfaces without [Register] on any method.
	/// </summary>
	[Register ("my/app/ImplicitMultiListener")]
	public class ImplicitMultiListener : Java.Lang.Object, Android.Views.IOnClickListener, Android.Views.IOnLongClickListener
	{
		protected ImplicitMultiListener (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public void OnClick (Android.Views.View v) { }
		public bool OnLongClick (Android.Views.View v) => false;
	}

	/// <summary>
	/// Has one interface method with [Register] and one without.
	/// </summary>
	[Register ("my/app/MixedInterfaceImpl")]
	public class MixedInterfaceImpl : Java.Lang.Object, Android.Views.IOnClickListener, Android.Views.IOnLongClickListener
	{
		protected MixedInterfaceImpl (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("onClick", "(Landroid/view/View;)V", "")]
		public void OnClick (Android.Views.View v) { }

		// No [Register] — should be detected from interface
		public bool OnLongClick (Android.Views.View v) => false;
	}

	/// <summary>
	/// Implements an interface that extends another registered interface.
	/// Should get methods from both the child and parent interface.
	/// </summary>
	[Register ("my/app/NamedClickListenerImpl")]
	public class NamedClickListenerImpl : Java.Lang.Object, Android.Views.INamedClickListener
	{
		protected NamedClickListenerImpl (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public void OnClick (Android.Views.View v) { }
		public string? Label => "test";
	}

	// --- Override detection test types ---
	// These types override registered base methods WITHOUT [Register] on the override,
	// mimicking real user code where the attribute is only on the base class in Mono.Android.

	/// <summary>
	/// Overrides Activity.OnCreate without [Register] — the scanner must detect this
	/// by walking the base type hierarchy and finding [Register] on Activity.OnCreate.
	/// </summary>
	[Register ("my/app/UserActivity")]
	public class UserActivity : Android.App.Activity
	{
		protected UserActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// No [Register] here — real user code doesn't have it
		protected override void OnCreate (object? savedInstanceState) => base.OnCreate (savedInstanceState);
	}

	/// <summary>
	/// Overrides multiple registered base methods without [Register].
	/// </summary>
	[Register ("my/app/FullActivity")]
	public class FullActivity : Android.App.Activity
	{
		protected FullActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		protected override void OnCreate (object? savedInstanceState) { }
		protected override void OnStart () { }
	}

	/// <summary>
	/// Deep inheritance: overrides a method registered two levels up.
	/// Activity has [Register("onCreate",...)], UserActivity overrides it (no [Register]),
	/// DeeplyDerived overrides it again (no [Register]).
	/// </summary>
	[Register ("my/app/DeeplyDerived")]
	public class DeeplyDerived : UserActivity
	{
		protected DeeplyDerived (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		protected override void OnCreate (object? savedInstanceState) { }
	}

	/// <summary>
	/// Has both a direct [Register] method AND an override of a base registered method.
	/// The override should be detected; the direct one should not be duplicated.
	/// </summary>
	[Register ("my/app/MixedMethods")]
	public class MixedMethods : Android.App.Activity
	{
		protected MixedMethods (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// Override without [Register] — should be detected from base
		protected override void OnCreate (object? savedInstanceState) { }

		// Direct [Register] — should be collected normally
		[Register ("customMethod", "()V", "GetCustomMethodHandler")]
		public virtual void CustomMethod () { }
	}

	/// <summary>
	/// Uses 'new' keyword (IsNewSlot) — should NOT be detected as an override.
	/// </summary>
	[Register ("my/app/NewSlotActivity")]
	public class NewSlotActivity : Android.App.Activity
	{
		protected NewSlotActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// 'new' hides the base method rather than overriding it
		protected new void OnCreate (object? savedInstanceState) { }
	}

	/// <summary>
	/// Overrides a registered property getter without [Register] on the override.
	/// </summary>
	[Register ("my/app/CustomException")]
	public class CustomException : Java.Lang.Throwable
	{
		protected CustomException (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// Overrides Throwable.Message which has [Register("getMessage",...)]
		public override string? Message => "custom";
	}

	// --- Constructor chaining test types ---

	/// <summary>
	/// Uses [JniConstructorSignature] instead of [Register] on its constructors.
	/// Mimics Java.Interop-style binding types (e.g., Java.Base-ref.cs).
	/// </summary>
	[Register ("my/app/JiStyleView")]
	public class JiStyleView : Android.Views.View
	{
		protected JiStyleView (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Java.Interop.JniConstructorSignature ("()V")]
		public JiStyleView () : base (default, default) { }

		[Java.Interop.JniConstructorSignature ("(Landroid/content/Context;)V")]
		public JiStyleView (Context context) : base (default, default) { }
	}

	/// <summary>
	/// Has a ctor with a parameter type that doesn't exist on any base registered ctor,
	/// but since Activity has a registered parameterless ctor, legacy CecilImporter accepts
	/// this ctor via the parameterless fallback (CecilImporter.cs:394-397).
	/// </summary>
	[Register ("my/app/ActivityWithCustomCtor")]
	public class ActivityWithCustomCtor : Android.App.Activity
	{
		protected ActivityWithCustomCtor (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// string param doesn't match any base registered ctor's params,
		// but Activity has a registered ()V ctor → fallback accepts this.
		public ActivityWithCustomCtor (string label) { }
	}

	// --- Additional deep hierarchy test types ---

	/// <summary>
	/// User ACW base type that adds its own registered method. Used to test
	/// multi-level user type hierarchies (ACW → ACW → MCW).
	/// </summary>
	[Register ("my/app/BaseFragment")]
	public class BaseFragment : Android.App.Activity
	{
		protected BaseFragment (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("onViewCreated", "()V", "GetOnViewCreatedHandler")]
		protected virtual void OnViewCreated () { }
	}

	/// <summary>
	/// Overrides both a method registered on a user ACW base (BaseFragment.OnViewCreated)
	/// and one on an MCW base (Activity.OnCreate). Tests that DeclaringTypeName points
	/// to the correct type for each.
	/// </summary>
	[Register ("my/app/DerivedFragment")]
	public class DerivedFragment : BaseFragment
	{
		protected DerivedFragment (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		protected override void OnCreate (object? savedInstanceState) { }
		protected override void OnViewCreated () { }
	}

	/// <summary>
	/// Three levels deep: GrandchildFragment → DerivedFragment → BaseFragment → Activity.
	/// OnCreate [Register] is on Activity (3 levels up). Tests deep recursive walk.
	/// </summary>
	[Register ("my/app/GrandchildFragment")]
	public class GrandchildFragment : DerivedFragment
	{
		protected GrandchildFragment (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		protected override void OnCreate (object? savedInstanceState) { }
	}

	// --- Constructor chaining: same-arity type mismatch ---

	/// <summary>
	/// MCW base with a registered ctor that takes a Context parameter.
	/// Used to test that ctor parameter type matching is strict (same arity,
	/// different types should NOT match).
	/// </summary>
	[Register ("my/app/DialogBase", DoNotGenerateAcw = true)]
	public class DialogBase : Java.Lang.Object
	{
		[Register (".ctor", "()V", "")]
		public DialogBase () { }

		[Register (".ctor", "(Landroid/content/Context;)V", "")]
		public DialogBase (Context context) { }

		protected DialogBase (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	/// <summary>
	/// Derived type with a ctor(string) that has the same arity as
	/// DialogBase's registered ctor(Context) but a different parameter type.
	/// The scanner must NOT treat it as "already covered" — it should fall
	/// through to the parameterless fallback and compute the JNI signature.
	/// </summary>
	[Register ("my/app/CustomDialog")]
	public class CustomDialog : DialogBase
	{
		protected CustomDialog (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		// Same arity as DialogBase(Context) but different type → not covered.
		// Parameterless fallback: super() + nctor_N(string).
		public CustomDialog (string title) { }
	}

	// --- Constructor chaining: multi-parameter fallback ---

	/// <summary>
	/// Has a ctor with multiple primitive/string params to test that
	/// BuildJniCtorSignature correctly handles multi-parameter signatures.
	/// </summary>
	[Register ("my/app/ActivityWithMultiParamCtor")]
	public class ActivityWithMultiParamCtor : Android.App.Activity
	{
		protected ActivityWithMultiParamCtor (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public ActivityWithMultiParamCtor (string name, int count, bool enabled) { }
	}

	// --- Test gap fixtures ---

	/// <summary>
	/// Has a ctor taking an array of Java peer objects.
	/// Tests that TryResolveJniObjectDescriptor + array recursion produces [Landroid/view/View;
	/// </summary>
	[Register ("my/app/ViewArrayActivity")]
	public class ViewArrayActivity : Android.App.Activity
	{
		protected ViewArrayActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public ViewArrayActivity (Android.Views.View[] views) { }
	}

	/// <summary>
	/// Overrides an abstract base method without [Register].
	/// AbstractBase.DoWork has [Register("doWork", "()V", "")].
	/// </summary>
	[Register ("my/app/ConcreteImpl")]
	public class ConcreteImpl : AbstractBase
	{
		protected ConcreteImpl (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void DoWork () { }
	}

	/// <summary>
	/// MCW base with two overloaded methods — same name, different params.
	/// Tests that override detection picks the correct overload.
	/// </summary>
	[Register ("my/app/OverloadBase", DoNotGenerateAcw = true)]
	public class OverloadBase : Java.Lang.Object
	{
		protected OverloadBase (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("process", "()V", "GetProcessHandler")]
		public virtual void Process () { }

		[Register ("process", "(I)V", "GetProcess_IHandler")]
		public virtual void Process (int value) { }
	}

	/// <summary>
	/// Overrides only one of two overloads — Process(int) but not Process().
	/// </summary>
	[Register ("my/app/OverloadDerived")]
	public class OverloadDerived : OverloadBase
	{
		protected OverloadDerived (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void Process (int value) { }
	}

	/// <summary>
	/// Declares a registered abstract method above an intermediate MCW base type.
	/// Mirrors AdapterView.SetSelection(int) for AbsListView-derived test fixtures.
	/// </summary>
	[Register ("my/app/SelectionHost", DoNotGenerateAcw = true)]
	public abstract class SelectionHost : Java.Lang.Object
	{
		protected SelectionHost (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("setSelection", "(I)V", "GetSetSelection_IHandler")]
		public abstract void SetSelection (int position);
	}

	/// <summary>
	/// Intermediate MCW base that inherits the registered method without redeclaring it.
	/// </summary>
	[Register ("my/app/SelectionContainer", DoNotGenerateAcw = true)]
	public abstract class SelectionContainer : SelectionHost
	{
		protected SelectionContainer (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	/// <summary>
	/// Generic base used to verify override discovery through a generic-instantiated base type.
	/// Mirrors AdapterView&lt;T&gt; in the real Mono.Android hierarchy.
	/// </summary>
	[Register ("my/app/GenericSelectionHost", DoNotGenerateAcw = true)]
	public abstract class GenericSelectionHost<T> : Java.Lang.Object where T : class
	{
		protected GenericSelectionHost (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("setSelection", "(I)V", "GetSetSelection_IHandler")]
		public abstract void SetSelection (int position);
	}

	/// <summary>
	/// Intermediate MCW base that closes the generic base.
	/// </summary>
	[Register ("my/app/GenericSelectionContainer", DoNotGenerateAcw = true)]
	public abstract class GenericSelectionContainer : GenericSelectionHost<string>
	{
		protected GenericSelectionContainer (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	/// <summary>
	/// Overrides a registered method declared above the first MCW base in the hierarchy.
	/// </summary>
	[Register ("my/app/SelectableList")]
	public class SelectableList : SelectionContainer
	{
		protected SelectableList (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void SetSelection (int position) { }
	}

	/// <summary>
	/// Overrides a registered method declared above a generic-instantiated MCW base.
	/// </summary>
	[Register ("my/app/GenericSelectableList")]
	public class GenericSelectableList : GenericSelectionContainer
	{
		protected GenericSelectableList (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void SetSelection (int position) { }
	}

	/// <summary>
	/// Has a ctor with unsigned primitive params to test JNI mapping.
	/// </summary>
	[Register ("my/app/UnsignedParamActivity")]
	public class UnsignedParamActivity : Android.App.Activity
	{
		protected UnsignedParamActivity (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public UnsignedParamActivity (ushort a, uint b, ulong c) { }
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

	[Register ("my/app/ExportWithThrows")]
	public class ExportWithThrows : Java.Lang.Object
	{
		protected ExportWithThrows (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Java.Interop.Export ("riskyMethod", ThrownNames = new [] { "java.io.IOException", "java.lang.IllegalStateException" })]
		public void RiskyMethod () { }
	}

	[Register ("my/app/JiStylePeer", DoNotGenerateAcw = true)]
	public class JiStylePeer : Java.Lang.Object
	{
		protected JiStylePeer (ref Java.Interop.JniObjectReference reference, Java.Interop.JniObjectReferenceOptions options)
			: base ((IntPtr)0, JniHandleOwnership.DoNotTransfer) { }
	}
}

[Register ("my/app/GlobalType")]
public class GlobalType : Java.Lang.Object
{
	protected GlobalType (IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base (handle, transfer) { }
}
public class GlobalUnregisteredType : Java.Lang.Object { }

// Matches the Android app project template pattern from
// Tests/Xamarin.ProjectTools/Resources/DotNet/MainActivity.cs:
//   [Register ("${JAVA_PACKAGENAME}.MainActivity"), Activity (Label = "...", MainLauncher = true, Icon = "@drawable/icon")]
[Register ("com.example.dotformat.MainActivity"), Activity (Label = "DotFormat", MainLauncher = true)]
public class DotFormatActivity : Android.App.Activity
{
	protected DotFormatActivity (IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base (handle, transfer) { }
}

// --- Alias test types ---
// Multiple .NET types mapping to the same JNI name (e.g., generic + non-generic collection wrappers).

namespace MyApp.Aliases
{
	/// <summary>
	/// Non-generic type registered as "test/AliasTarget" — forms the primary entry.
	/// </summary>
	[Register ("test/AliasTarget", DoNotGenerateAcw = true)]
	public class AliasTarget : Java.Lang.Object
	{
		protected AliasTarget (IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	/// <summary>
	/// Generic type also registered as "test/AliasTarget" — forms an alias entry.
	/// Mirrors the real-world pattern of JavaCollection/JavaCollection&lt;T&gt;.
	/// </summary>
	[Register ("test/AliasTarget", DoNotGenerateAcw = true)]
	public class AliasTargetGeneric<T> : Java.Lang.Object
	{
		protected AliasTargetGeneric (IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base (handle, transfer) { }
	}

	/// <summary>
	/// Third type also registered as "test/AliasTarget" — tests 3-way alias groups.
	/// </summary>
	[Register ("test/AliasTarget", DoNotGenerateAcw = true)]
	public class AliasTargetExtended : Java.Lang.Object
	{
		protected AliasTargetExtended (IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base (handle, transfer) { }
	}
}

// [JniTypeSignature] types — Java.Interop's JavaObject hierarchy
namespace Java.Interop.TestTypes
{
	[Java.Interop.JniTypeSignature ("java/lang/Object", GenerateJavaPeer = false)]
	public class JavaObject
	{
		public JavaObject () { }
	}

	[Java.Interop.JniTypeSignature ("net/dot/jni/test/JavaDisposedObject")]
	public class JavaDisposedObject : JavaObject
	{
		public JavaDisposedObject () { }
	}

	[Java.Interop.JniTypeSignature ("net/dot/jni/test/MyJavaObject", GenerateJavaPeer = false)]
	public class NonGeneratedJavaObject : JavaObject
	{
		public NonGeneratedJavaObject () { }
	}

	/// <summary>
	/// Mimics Java.Interop.JavaBooleanArray — a primitive array type with IsKeyword=true.
	/// The scanner must skip all ArrayRank > 0 types because they are handled by the
	/// built-in tables in JniRuntime.JniTypeManager.
	/// </summary>
	[Java.Interop.JniTypeSignature ("Z", IsKeyword = true, ArrayRank = 1, GenerateJavaPeer = false)]
	public sealed class KeywordPrimitiveArray : JavaObject
	{
	}

	/// <summary>
	/// Mimics Java.Interop.JavaObjectArray — a non-keyword array type with ArrayRank=1.
	/// The scanner must also skip these to avoid adding unnecessary aliases.
	/// </summary>
	[Java.Interop.JniTypeSignature ("java/lang/Object", ArrayRank = 1, GenerateJavaPeer = false)]
	public class NonKeywordArrayType : JavaObject
	{
	}
}
