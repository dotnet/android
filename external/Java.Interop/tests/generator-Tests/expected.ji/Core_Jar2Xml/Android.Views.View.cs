using System;
using System.Collections.Generic;
using Java.Interop;

namespace Android.Views {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.view']/class[@name='View']"
	[global::Java.Interop.JniTypeSignature ("android/view/View", GenerateJavaPeer=false)]
	public partial class View : global::Java.Lang.Object {
		// Metadata.xml XPath interface reference: path="/api/package[@name='android.view']/interface[@name='View.OnClickListener']"
		[global::Java.Interop.JniTypeSignature ("android/view/View$OnClickListener", GenerateJavaPeer=false)]
		public partial interface IOnClickListener : IJavaPeerable {
			// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/interface[@name='View.OnClickListener']/method[@name='onClick' and count(parameter)=1 and parameter[1][@type='android.view.View']]"
			void OnClick (global::Android.Views.View v);

		}

		[global::Android.Runtime.Register ("mono/android/view/View_OnClickListenerImplementor")]
		internal sealed partial class IOnClickListenerImplementor : global::Java.Lang.Object, IOnClickListener {
			public IOnClickListenerImplementor () : base (global::Android.Runtime.JNIEnv.StartCreateInstance ("mono/android/view/View_OnClickListenerImplementor", "()V"), JniHandleOwnership.TransferLocalRef)
			{
				global::Android.Runtime.JNIEnv.FinishCreateInstance (this.PeerReference, "()V");
			}

			#pragma warning disable 0649
			public EventHandler Handler;
			#pragma warning restore 0649

			public void OnClick (global::Android.Views.View v)
			{
				var __h = Handler;
				if (__h != null)
					__h (v, new EventArgs ());
			}

			internal static bool __IsEmpty (IOnClickListenerImplementor value)
			{
				return value.Handler == null;
			}

		}

		static readonly JniPeerMembers _members = new JniPeerMembers ("android/view/View", typeof (View));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected View (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/class[@name='View']/method[@name='setOnClickListener' and count(parameter)=1 and parameter[1][@type='android.view.View.OnClickListener']]"
		public virtual unsafe void SetOnClickListener (global::Android.Views.View.IOnClickListener l)
		{
			const string __id = "setOnClickListener.(Landroid/view/View$OnClickListener;)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (l);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (l);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/class[@name='View']/method[@name='setOn123Listener' and count(parameter)=1 and parameter[1][@type='android.view.View.OnClickListener']]"
		public virtual unsafe void SetOn123Listener (global::Android.Views.View.IOnClickListener l)
		{
			const string __id = "setOn123Listener.(Landroid/view/View$OnClickListener;)V";
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (l);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				global::System.GC.KeepAlive (l);
			}
		}

		// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/class[@name='View']/method[@name='addTouchables' and count(parameter)=1 and parameter[1][@type='java.util.ArrayList&lt;android.view.View&gt;']]"
		public virtual unsafe void AddTouchables (global::System.Collections.Generic.IList<global::Android.Views.View> views)
		{
			const string __id = "addTouchables.(Ljava/util/ArrayList;)V";
			IntPtr native_views = global::Android.Runtime.JavaList<global::Android.Views.View>.ToLocalJniHandle (views);
			try {
				JniArgumentValue* __args = stackalloc JniArgumentValue [1];
				__args [0] = new JniArgumentValue (native_views);
				_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_views);
				global::System.GC.KeepAlive (views);
			}
		}

		#region "Event implementation for Android.Views.View.IOnClickListener"

		public event EventHandler Click {
			add {
				global::Java.Interop.EventHelper.AddEventHandler<global::Android.Views.View.IOnClickListener, global::Android.Views.View.IOnClickListenerImplementor>(
				ref weak_implementor_SetOnClickListener,
				__CreateIOnClickListenerImplementor,
				SetOnClickListener,
				__h => __h.Handler += value);
			}
			remove {
				global::Java.Interop.EventHelper.RemoveEventHandler<global::Android.Views.View.IOnClickListener, global::Android.Views.View.IOnClickListenerImplementor>(
				ref weak_implementor_SetOnClickListener,
				global::Android.Views.View.IOnClickListenerImplementor.__IsEmpty,
				__v => SetOnClickListener (null),
				__h => __h.Handler -= value);
			}
		}

		WeakReference weak_implementor_SetOnClickListener;

		global::Android.Views.View.IOnClickListenerImplementor __CreateIOnClickListenerImplementor ()
		{
			return new global::Android.Views.View.IOnClickListenerImplementor ();
		}

		#endregion

	}
}
