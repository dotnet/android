using System;
using System.Runtime.InteropServices;
using Android.Runtime;
using Java.Interop;

// Marks this assembly as using the experimental [UnmanagedCallersOnly] binding callback format.
// The trimmable typemap uses this marker to opt into resolving every n_* callback so it can
// discover which ones must be registered directly instead of through a generated wrapper.
[assembly: JavaPeerCallbackFormat (JavaPeerCallbackFormatAttribute.UnmanagedCallersOnlyCallbacks)]

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = false)]
	public sealed class JavaPeerCallbackFormatAttribute : Attribute
	{
		public const int ConnectorDelegates = 1;
		public const int UnmanagedCallersOnlyCallbacks = 2;

		public JavaPeerCallbackFormatAttribute (int version) => Version = version;

		public int Version { get; }
	}
}

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures
{
	/// <summary>
	/// Models a binding generated with <c>--lang-features=unmanaged-callers-only-callbacks</c>:
	/// the <c>n_*</c> callbacks are <c>[UnmanagedCallersOnly]</c> and there are no
	/// <c>Get*Handler ()</c> connector methods, so managed code can never call them.
	/// </summary>
	[Register ("com/example/uco/UcoWidget", DoNotGenerateAcw = true)]
	public class UcoWidget : Java.Lang.Object
	{
		public UcoWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("onLayout", "(ZIIII)V", "GetOnLayout_ZIIIIHandler")]
		public virtual void OnLayout (bool changed, int left, int top, int right, int bottom) { }

		[UnmanagedCallersOnly]
		static void n_OnLayout_ZIIII (IntPtr jnienv, IntPtr native__this, sbyte native_changed, int left, int top, int right, int bottom) { }

		[Register ("getCount", "()I", "GetGetCountHandler")]
		public virtual int Count => 0;

		[UnmanagedCallersOnly]
		static int n_GetCount (IntPtr jnienv, IntPtr native__this) => 0;
	}

	/// <summary>
	/// Models a binding in the same marked assembly whose callback is *not* eligible for
	/// <c>[UnmanagedCallersOnly]</c> (here: a generic declaring type). It keeps the legacy
	/// connector shape, so the typemap must still emit a forwarding wrapper for it.
	/// </summary>
	[Register ("com/example/uco/LegacyWidget", DoNotGenerateAcw = true)]
	public class LegacyWidget : Java.Lang.Object
	{
		public LegacyWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("getFlags", "()I", "GetGetFlagsHandler")]
		public virtual int Flags => 0;

		static int n_GetFlags (IntPtr jnienv, IntPtr native__this) => 0;
	}

	/// <summary>
	/// A user (ACW) subclass overriding a binding method whose callback is
	/// <c>[UnmanagedCallersOnly]</c>. RegisterNatives must <c>ldftn</c> the base
	/// <c>n_OnLayout_ZIIII</c> directly — a generated forwarder would be an illegal managed call.
	/// </summary>
	[Register ("com/example/uco/MyWidget")]
	public class MyWidget : UcoWidget
	{
		public MyWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void OnLayout (bool changed, int left, int top, int right, int bottom) { }

		public override int Count => 1;
	}

	/// <summary>
	/// Models the compact callback names a marked assembly emits: the callback is named after the
	/// managed member instead of its Java signature, and duplicate base names are numbered.  With
	/// no connector method left to name, the connector stores the callback name itself.
	/// </summary>
	[Register ("com/example/uco/CompactWidget", DoNotGenerateAcw = true)]
	public class CompactWidget : Java.Lang.Object
	{
		public CompactWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("remove", "(I)V", "n_Remove")]
		public virtual void Remove (int index) { }

		[UnmanagedCallersOnly]
		static void n_Remove (IntPtr jnienv, IntPtr native__this, int index) { }

		[Register ("remove", "(J)V", "n_Remove_1")]
		public virtual void Remove (long id) { }

		[UnmanagedCallersOnly]
		static void n_Remove_1 (IntPtr jnienv, IntPtr native__this, long id) { }
	}

	/// <summary>
	/// A compact connector which also carries the owner qualifier an interface invoker or default
	/// interface method needs.  Both halves have to survive: the qualifier still resolves the
	/// declaring type, and the callback name is read verbatim from the segment before it.
	/// </summary>
	[Register ("com/example/uco/QualifiedWidget", DoNotGenerateAcw = true)]
	public class QualifiedWidget : Java.Lang.Object
	{
		public QualifiedWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[Register ("handle", "()V", "n_Handle:Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.CallbackHost, TestUcoFixtures")]
		public virtual void Handle () { }

		[Register ("handleTypeOnly", "()V", "n_HandleTypeOnly:Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.CallbackHost")]
		public virtual void HandleTypeOnly () { }

		[Register ("getCount", "()I", "n_Count:Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.CallbackHost, TestUcoFixtures")]
		public virtual int Count => 0;

		public virtual bool Enabled {
			[Register ("isEnabled", "()Z", "n_IsEnabled:Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.CallbackHost, TestUcoFixtures")]
			get => false;
			[Register ("setEnabled", "(Z)V", "n_SetEnabled:Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.CallbackHost, TestUcoFixtures")]
			set { }
		}
	}

	/// <summary>
	/// Stands in for the invoker type an owner-qualified connector points at.
	/// </summary>
	[Register ("com/example/uco/CallbackHost", DoNotGenerateAcw = true)]
	public class CallbackHost : Java.Lang.Object
	{
		public CallbackHost (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		[UnmanagedCallersOnly]
		internal static void n_Handle (IntPtr jnienv, IntPtr native__this) { }

		[UnmanagedCallersOnly]
		static void n_HandleTypeOnly (IntPtr jnienv, IntPtr native__this) { }

		[UnmanagedCallersOnly]
		static int n_Count (IntPtr jnienv, IntPtr native__this) => 0;

		[UnmanagedCallersOnly]
		static sbyte n_IsEnabled (IntPtr jnienv, IntPtr native__this) => 0;

		[UnmanagedCallersOnly]
		static void n_SetEnabled (IntPtr jnienv, IntPtr native__this, sbyte enabled) { }
	}

	/// <summary>
	/// ACW subclasses of the compact-format bindings.
	/// </summary>
	[Register ("com/example/uco/MyCompactWidget")]
	public class MyCompactWidget : CompactWidget
	{
		public MyCompactWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void Remove (int index) { }

		public override void Remove (long id) { }
	}

	[Register ("com/example/uco/MyQualifiedWidget")]
	public class MyQualifiedWidget : QualifiedWidget
	{
		public MyQualifiedWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override void Handle () { }

		public override void HandleTypeOnly () { }

		public override int Count => 1;

		public override bool Enabled { get; set; }
	}

	/// <summary>
	/// A user (ACW) subclass overriding a legacy connector-style binding method in the same
	/// marked assembly. It must keep the generated forwarding wrapper.
	/// </summary>
	[Register ("com/example/uco/MyLegacyWidget")]
	public class MyLegacyWidget : LegacyWidget
	{
		public MyLegacyWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override int Flags => 2;
	}

	[Register ("com/example/uco/LegacyOverrideWidget", DoNotGenerateAcw = true)]
	public abstract class LegacyOverrideWidget : LegacyWidget
	{
		public LegacyOverrideWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public abstract override int Flags {
			[Register ("getFlags", "()I", "GetGetFlagsHandler:Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.LegacyWidget, TestUcoFixtures")]
			get;
		}
	}

	[Register ("com/example/uco/MyLegacyOverrideWidget")]
	public class MyLegacyOverrideWidget : LegacyOverrideWidget
	{
		public MyLegacyOverrideWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override int Flags => 3;
	}

	[Register ("com/example/uco/QualifiedLegacyWidget", DoNotGenerateAcw = true)]
	public class QualifiedLegacyWidget : MyApp.MyHelper
	{
		// MyHelper models legacy reference metadata without its private callback.
		[Register ("doSomething", "()V", "GetDoSomethingHandler:MyApp.MyHelper, TestFixtures")]
		public override void DoSomething () { }
	}

	[Register ("com/example/uco/MyQualifiedLegacyWidget")]
	public class MyQualifiedLegacyWidget : QualifiedLegacyWidget
	{
		public override void DoSomething () { }
	}

	[Register ("com/example/uco/DirectWidget")]
	public class DirectWidget : Java.Lang.Object
	{
		[Register ("direct", "()I", "")]
		public int Direct () => 42;
	}
}
