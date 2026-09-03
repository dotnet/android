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
	/// A user (ACW) subclass overriding a legacy connector-style binding method in the same
	/// marked assembly. It must keep the generated forwarding wrapper.
	/// </summary>
	[Register ("com/example/uco/MyLegacyWidget")]
	public class MyLegacyWidget : LegacyWidget
	{
		public MyLegacyWidget (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }

		public override int Flags => 2;
	}
}
