using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Views;
using JLO = Java.Lang.Object;

using Android.Runtime;

using Java.Interop;

namespace Android.Widget {
	[Obsolete ("Use AdapterView.ItemClickEventArgs instead", error: true)]
	public class ItemEventArgs : AdapterView.ItemClickEventArgs {

		public ItemEventArgs (Android.Views.View view, int position, long id)
			: base (null, view, position, id)
		{
		}

		public ItemEventArgs (Android.Widget.AdapterView parent, Android.Views.View view, int position, long id)
			: base (parent, view, position, id)
		{
		}
	}

	public abstract partial class AdapterView {	
		
		List<EventHandler>? selection_cleared;
		
		void OnSelectionCleared (object? o, Android.Widget.AdapterView.NothingSelectedEventArgs args)
		{
			if (selection_cleared != null)
				foreach (var h in selection_cleared)
					h (o, EventArgs.Empty);
		}
		
		[Obsolete ("Use NothingSelected event instead")]
		public event EventHandler ItemSelectionCleared {
			add {
				if (selection_cleared == null) {
					selection_cleared = new List<EventHandler> ();
					NothingSelected += OnSelectionCleared;
				}
				selection_cleared.Add (value);
			}
			remove {
				selection_cleared?.Remove (value);
			}
		}
	}

	[Register ("android/widget/AdapterView", DoNotGenerateAcw=true)]
	public abstract class AdapterView<
			[DynamicallyAccessedMembers (Constructors)]
			T
	> : AdapterView  where T : IAdapter {

		/// <safety>
		/// A nonzero <paramref name="handle"/> must be a live AdapterView-compatible reference from this VM
		/// usable on this thread until construction returns or throws. Own the requested reference transfer,
		/// exclude concurrent release/use of consumed aliases, and use zero only for empty-peer initialization.
		/// Caller-unsafe enforcement on raw peer constructors is deferred; existing initialization APIs are unchanged.
		/// </safety>
		public AdapterView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register (".ctor", "(Landroid/content/Context;)V", "")]
		public AdapterView (Android.Content.Context context)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			unsafe {
				// SAFETY: StartCreateInstance supplies this peer's owned handle; FinishCreateInstance invokes the (Landroid/content/Context;)V constructor with the matching arguments before this constructor returns.
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;)V", new JValue (context)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;)V", new JValue (context));
				GC.KeepAlive (context);
			}
		}

		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;)V", "")]
		public AdapterView (Android.Content.Context context, Android.Util.IAttributeSet attrs)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			unsafe {
				// SAFETY: StartCreateInstance supplies this peer's owned handle; FinishCreateInstance invokes the (Landroid/content/Context;Landroid/util/AttributeSet;)V constructor with the matching arguments before this constructor returns.
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;Landroid/util/AttributeSet;)V", new JValue (context), new JValue (attrs)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;Landroid/util/AttributeSet;)V", new JValue (context), new JValue (attrs));
				GC.KeepAlive (context);
				GC.KeepAlive (attrs);
			}
		}

		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", "")]
		public AdapterView (Android.Content.Context context, Android.Util.IAttributeSet attrs, int defStyle)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			unsafe {
				// SAFETY: StartCreateInstance supplies this peer's owned handle; FinishCreateInstance invokes the (Landroid/content/Context;Landroid/util/AttributeSet;I)V constructor with the matching arguments before this constructor returns.
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", new JValue (context), new JValue (attrs), new JValue (defStyle)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", new JValue (context), new JValue (attrs), new JValue (defStyle));
				GC.KeepAlive (context);
				GC.KeepAlive (attrs);
			}
		}

		protected override Java.Lang.Object? RawAdapter {
			get { return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (Adapter)); }
			set { Adapter = JavaConvert.FromJavaObject<T>(value)!; }
		}

		public abstract T Adapter {
			[Register ("getAdapter", "()Landroid/widget/Adapter;", "GetGetAdapterHandler")] get;
			[Register ("setAdapter", "(Landroid/widget/Adapter;)V", "GetSetAdapter_Landroid_widget_Adapter_Handler")] set;
		}
	}
}
