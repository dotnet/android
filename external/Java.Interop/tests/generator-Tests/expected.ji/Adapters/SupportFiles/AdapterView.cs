// Java allows a type to redeclare a member that a base type or base interface already
// declares, including members that are hand-bound instead of generated.
#pragma warning disable 0108, 0114
// Java types may declare a `finalize()` method, which is bound as `Finalize()`.
#pragma warning disable 0465
// Java deprecates types and members independently of the APIs that use, declare or
// override them, so a binding that is not deprecated can still reference one that is.
#pragma warning disable 0618, 0672, 0809
// Java nullness annotations are not required to agree between a member and the member
// it overrides or implements, and are absent from much of the API surface.
#pragma warning disable 8603, 8604, 8625, 8764, 8765, 8766, 8767, 8768

using System;
using Android.Runtime;
using Java.Interop;

namespace Xamarin.Test
{
	public abstract class AdapterView<T> : AdapterView  where T : IAdapter
	{
#if JAVA_INTEROP1

			public AdapterView (ref JniObjectReference reference, JniObjectReferenceOptions options)
				: base (ref reference, options)
			{
			}

			protected override Java.Lang.Object RawAdapter {
				get => throw new NotImplementedException ();
				set {}
			}

			public abstract T Adapter {
				get;
				set;
			}

#else   // !JAVA_INTEROP1

			public AdapterView (IntPtr handle, JniHandleOwnership transfer)
				: base (handle, transfer)
			{
			}

			protected override Java.Lang.Object RawAdapter {
				get { return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (Adapter)); }
				set { Adapter = JavaConvert.FromJavaObject<T>(value); }
			}

			public abstract T Adapter {
				[Register ("getAdapter", "()Landroid/widget/Adapter;", "GetGetAdapterHandler")] get;
				[Register ("setAdapter", "(Landroid/widget/Adapter;)V", "GetSetAdapter_Landroid_widget_Adapter_Handler")] set;
			}

#endif  // !JAVA_INTEROP1
	}
}

