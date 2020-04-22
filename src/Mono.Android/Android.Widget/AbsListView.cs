using System;
using System.Collections.Generic;

using Java.Interop;

using Android.Runtime;

namespace Android.Widget {

	partial class AbsListView {

		static Delegate cb_getAdapter;
#pragma warning disable 0169
		static Delegate GetGetAdapterHandler ()
		{
			if (cb_getAdapter == null)
				cb_getAdapter = JNINativeWrapper.CreateDelegate ((Func<IntPtr, IntPtr, IntPtr>) n_GetAdapter);
			return cb_getAdapter;
		}

		static IntPtr id_getAdapter;
		static IntPtr n_GetAdapter (IntPtr jnienv, IntPtr native__this)
		{
			var __this = global::Java.Lang.Object.GetObject<Android.Widget.AbsListView> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			return JNIEnv.ToLocalJniHandle (__this.Adapter);
		}

		static Delegate cb_setAdapter_Landroid_widget_Adapter_;
#pragma warning disable 0169
		static Delegate GetSetAdapter_Landroid_widget_ListAdapter_Handler ()
		{
			if (cb_setAdapter_Landroid_widget_Adapter_ == null)
				cb_setAdapter_Landroid_widget_Adapter_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetAdapter_Landroid_widget_ListAdapter_);
			return cb_setAdapter_Landroid_widget_Adapter_;
		}

		static void n_SetAdapter_Landroid_widget_ListAdapter_ (IntPtr jnienv, IntPtr native__this, IntPtr native_adapter)
		{
			AbsListView __this  = Java.Lang.Object.GetObject<Android.Widget.AbsListView> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			__this.Adapter      = Java.Interop.JavaConvert.FromJniHandle<IListAdapter> (native_adapter, JniHandleOwnership.DoNotTransfer);
		}
#pragma warning restore 0169

		public abstract override IListAdapter Adapter {
			[Register ("getAdapter", "()Landroid/widget/ListAdapter;", "GetGetAdapterHandler")]
			get;
			[Register ("setAdapter", "(Landroid/widget/ListAdapter;)V", "GetSetAdapter_Landroid_widget_ListAdapter_Handler")]
			set;
		}

#if ANDROID_12
		static IntPtr id_setAdapter_Landroid_widget_ListAdapter_;
		[Obsolete ("Please use the Adapter property setter")]
		[Register ("setAdapter", "(Landroid/widget/ListAdapter;)V", "GetSetAdapter_Landroid_widget_ListAdapter_Handler")]
		public virtual void SetAdapter (Android.Widget.IListAdapter adapter)
		{
			if (id_setAdapter_Landroid_widget_ListAdapter_ == IntPtr.Zero)
				id_setAdapter_Landroid_widget_ListAdapter_ = JNIEnv.GetMethodID (class_ref, "setAdapter", "(Landroid/widget/ListAdapter;)V");

			if (GetType () == ThresholdType)
				JNIEnv.CallVoidMethod  (Handle, id_setAdapter_Landroid_widget_ListAdapter_, new JValue (adapter));
			else
				JNIEnv.CallNonvirtualVoidMethod  (Handle, ThresholdClass, id_setAdapter_Landroid_widget_ListAdapter_, new JValue (adapter));
		}
#endif
	}

	internal partial class AbsListViewInvoker {

		public unsafe override IListAdapter Adapter {
			get {
				IntPtr value;
#if JAVA_INTEROP
				const string __id = "getAdapter.()Landroid/widget/Adapter;";
				value = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null).Handle;
#else   // !JAVA_INTEROP
				if (id_getAdapter == IntPtr.Zero)
					id_getAdapter = JNIEnv.GetMethodID (class_ref, "getAdapter", "()Landroid/widget/Adapter;");
				value = JNIEnv.CallObjectMethod (Handle, id_getAdapter);
#endif	// !JAVA_INTEROP
				return Java.Lang.Object.GetObject<IListAdapter> (value, JniHandleOwnership.TransferLocalRef);
			}
			set {
#if JAVA_INTEROP
				const string __id = "setAdapter.(Landroid/widget/Adapter;)V";
				IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (native_value);
					_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
				} finally {
					JNIEnv.DeleteLocalRef (native_value);
				}
#else   // !JAVA_INTEROP
				if (id_setAdapter_Landroid_widget_Adapter_ == IntPtr.Zero)
					id_setAdapter_Landroid_widget_Adapter_ = JNIEnv.GetMethodID (class_ref, "setAdapter", "(Landroid/widget/ListAdapter;)V");
				JNIEnv.CallVoidMethod (Handle, id_setAdapter_Landroid_widget_Adapter_, new JValue (JNIEnv.ToJniHandle (value)));
#endif	// !JAVA_INTEROP
			}
		}
	}

}
