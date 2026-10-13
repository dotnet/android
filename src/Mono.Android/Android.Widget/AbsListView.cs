using System;
using System.Collections.Generic;

using Java.Interop;

using Android.Runtime;

namespace Android.Widget {

	partial class AbsListView {

		static Delegate? cb_getAdapter;
		static Delegate GetGetAdapterHandler ()
		{
			return cb_getAdapter ??= new _JniMarshal_PP_L (n_GetAdapter);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static IntPtr n_GetAdapter (IntPtr jnienv, IntPtr native__this)
		{
			unsafe {
				// SAFETY: the VM supplies this callback's live current-thread environment and borrowed receiver/argument references until the JNI transition ends.
				if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
					return default;

				try {
					var __this = global::Java.Lang.Object.GetObject<Android.Widget.AbsListView> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
					return JNIEnv.ToLocalJniHandle (__this.Adapter);
				} catch (global::System.Exception __e) {
					__r.OnUserUnhandledException (ref __envp, __e);
					return default;
				} finally {
					global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
				}
			}
		}

		static Delegate? cb_setAdapter_Landroid_widget_Adapter_;
		static Delegate GetSetAdapter_Landroid_widget_ListAdapter_Handler ()
		{
			return cb_setAdapter_Landroid_widget_Adapter_ ??= new _JniMarshal_PPL_V (n_SetAdapter_Landroid_widget_ListAdapter_);
		}

		[global::System.Diagnostics.DebuggerDisableUserUnhandledExceptions]
		static void n_SetAdapter_Landroid_widget_ListAdapter_ (IntPtr jnienv, IntPtr native__this, IntPtr native_adapter)
		{
			unsafe {
				// SAFETY: the VM supplies this callback's live current-thread environment and borrowed receiver/argument references until the JNI transition ends.
				if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))
					return;

				try {
					var __this  = Java.Lang.Object.GetObject<Android.Widget.AbsListView> (jnienv, native__this, JniHandleOwnership.DoNotTransfer)!;
					__this.Adapter      = Java.Interop.JavaConvert.FromJniHandle<IListAdapter> (native_adapter, JniHandleOwnership.DoNotTransfer);
				} catch (global::System.Exception __e) {
					__r.OnUserUnhandledException (ref __envp, __e);
					return;
				} finally {
					global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);
				}
			}
		}

#pragma warning disable CS8764 // AdapterView<T> predates NRT, but an AbsListView can have no adapter.
		public abstract override IListAdapter? Adapter {
			[Register ("getAdapter", "()Landroid/widget/ListAdapter;", "GetGetAdapterHandler")]
			get;
			[Register ("setAdapter", "(Landroid/widget/ListAdapter;)V", "GetSetAdapter_Landroid_widget_ListAdapter_Handler")]
			set;
		}
#pragma warning restore CS8764

#if ANDROID_12
		[Obsolete ("Please use the Adapter property setter")]
		[Register ("setAdapter", "(Landroid/widget/ListAdapter;)V", "GetSetAdapter_Landroid_widget_ListAdapter_Handler")]
		public virtual void SetAdapter (Android.Widget.IListAdapter adapter)
		{
			unsafe {
				// SAFETY: the adapter member signature matches the receiver and argument buffer; adapter references survive until the existing finally cleanup.
				const string id = "setAdapter.(Landroid/widget/ListAdapter;)V";
				try {
					JniArgumentValue* args = stackalloc JniArgumentValue [1];
					args [0] = new JniArgumentValue (adapter);
					_members.InstanceMethods.InvokeVirtualVoidMethod (id, this, args);
				} finally {
					GC.KeepAlive (adapter);
					GC.KeepAlive (this);
				}
			}
		}
#endif
	}

	internal partial class AbsListViewInvoker {

		public override IListAdapter? Adapter {
			get {
				unsafe {
					// SAFETY: the adapter member signature matches the receiver and argument buffer; adapter references survive until the existing finally cleanup.
					IntPtr value;
					const string __id = "getAdapter.()Landroid/widget/Adapter;";
					value = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null).Handle;
					return Java.Lang.Object.GetObject<IListAdapter> (value, JniHandleOwnership.TransferLocalRef);
				}
			}
			set {
				unsafe {
					// SAFETY: the adapter member signature matches the receiver and argument buffer; adapter references survive until the existing finally cleanup.
					const string __id = "setAdapter.(Landroid/widget/Adapter;)V";
					IntPtr native_value = JNIEnv.ToLocalJniHandle (value);
					try {
						JniArgumentValue* __args = stackalloc JniArgumentValue [1];
						__args [0] = new JniArgumentValue (native_value);
						_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
					} finally {
						JNIEnv.DeleteLocalRef (native_value);
					}
				}
			}
		}
	}

}
