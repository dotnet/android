using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Views;
using JLO = Java.Lang.Object;

using Android.Runtime;

using Java.Interop;

namespace Android.Widget {
	[Obsolete ("Use AdapterView.ItemClickEventArgs instead")]
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
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			T
	> : AdapterView  where T : IAdapter {

		public AdapterView (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static IntPtr id_ctor_Landroid_content_Context_;
		[Register (".ctor", "(Landroid/content/Context;)V", "")]
		public AdapterView (Android.Content.Context context)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (AdapterView<T>)) {
				if (id_ctor_Landroid_content_Context_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_, new JValue (context)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_, new JValue (context));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;)V", new JValue (context)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;)V", new JValue (context));
			}
		}

		static IntPtr id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_;
		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;)V", "")]
		public AdapterView (Android.Content.Context context, Android.Util.IAttributeSet attrs)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (AdapterView<T>)) {
				if (id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;Landroid/util/AttributeSet;)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_, new JValue (context), new JValue (attrs)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_, new JValue (context), new JValue (attrs));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;Landroid/util/AttributeSet;)V", new JValue (context), new JValue (attrs)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;Landroid/util/AttributeSet;)V", new JValue (context), new JValue (attrs));
			}
		}

		static IntPtr id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I;
		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", "")]
		public AdapterView (Android.Content.Context context, Android.Util.IAttributeSet attrs, int defStyle)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (AdapterView<T>)) {
				if (id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I == IntPtr.Zero)
					id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;Landroid/util/AttributeSet;I)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I, new JValue (context), new JValue (attrs), new JValue (defStyle)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I, new JValue (context), new JValue (attrs), new JValue (defStyle));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", new JValue (context), new JValue (attrs), new JValue (defStyle)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", new JValue (context), new JValue (attrs), new JValue (defStyle));
			}
		}

		protected override Java.Lang.Object RawAdapter {
			get { return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (Adapter))!; }
			set { Adapter = JavaConvert.FromJavaObject<T>(value)!; }
		}

		public abstract T Adapter {
			[Register ("getAdapter", "()Landroid/widget/Adapter;", "GetGetAdapterHandler")] get;
			[Register ("setAdapter", "(Landroid/widget/Adapter;)V", "GetSetAdapter_Landroid_widget_Adapter_Handler")] set;
		}
	}
}

