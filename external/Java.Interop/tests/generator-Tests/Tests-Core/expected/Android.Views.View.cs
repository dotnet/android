using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Views {

	// Metadata.xml XPath class reference: path="/api/package[@name='android.view']/class[@name='View']"
	[global::Android.Runtime.Register ("android/view/View", DoNotGenerateAcw=true)]
	public partial class View : Java.Lang.Object {

		// Metadata.xml XPath interface reference: path="/api/package[@name='android.view']/interface[@name='View.OnClickListener']"
		[Register ("android/view/View$OnClickListener", "", "Android.Views.View/IOnClickListenerInvoker")]
		public partial interface IOnClickListener : IJavaObject {

			// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/interface[@name='View.OnClickListener']/method[@name='onClick' and count(parameter)=1 and parameter[1][@type='android.view.View']]"
			[Register ("onClick", "(Landroid/view/View;)V", "GetOnClick_Landroid_view_View_Handler:Android.Views.View/IOnClickListenerInvoker, ")]
			void OnClick (Android.Views.View v);

		}

		[global::Android.Runtime.Register ("android/view/View$OnClickListener", DoNotGenerateAcw=true)]
		internal partial class IOnClickListenerInvoker : global::Java.Lang.Object, IOnClickListener {

			static IntPtr java_class_ref = JNIEnv.FindClass ("android/view/View$OnClickListener");

			protected override IntPtr ThresholdClass {
				get { return class_ref; }
			}

			protected override global::System.Type ThresholdType {
				get { return typeof (IOnClickListenerInvoker); }
			}

			new IntPtr class_ref;

			public static IOnClickListener GetObject (IntPtr handle, JniHandleOwnership transfer)
			{
				return global::Java.Lang.Object.GetObject<IOnClickListener> (handle, transfer);
			}

			static IntPtr Validate (IntPtr handle)
			{
				if (!JNIEnv.IsInstanceOf (handle, java_class_ref))
					throw new InvalidCastException (string.Format ("Unable to convert instance of type '{0}' to type '{1}'.",
								JNIEnv.GetClassNameFromInstance (handle), "android.view.View.OnClickListener"));
				return handle;
			}

			protected override void Dispose (bool disposing)
			{
				if (this.class_ref != IntPtr.Zero)
					JNIEnv.DeleteGlobalRef (this.class_ref);
				this.class_ref = IntPtr.Zero;
				base.Dispose (disposing);
			}

			public IOnClickListenerInvoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)
			{
				IntPtr local_ref = JNIEnv.GetObjectClass (((global::Java.Lang.Object) this).Handle);
				this.class_ref = JNIEnv.NewGlobalRef (local_ref);
				JNIEnv.DeleteLocalRef (local_ref);
			}

			static Delegate cb_onClick_Landroid_view_View_;
#pragma warning disable 0169
			static Delegate GetOnClick_Landroid_view_View_Handler ()
			{
				if (cb_onClick_Landroid_view_View_ == null)
					cb_onClick_Landroid_view_View_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_OnClick_Landroid_view_View_);
				return cb_onClick_Landroid_view_View_;
			}

			static void n_OnClick_Landroid_view_View_ (IntPtr jnienv, IntPtr native__this, IntPtr native_v)
			{
				Android.Views.View.IOnClickListener __this = global::Java.Lang.Object.GetObject<Android.Views.View.IOnClickListener> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
				Android.Views.View v = global::Java.Lang.Object.GetObject<Android.Views.View> (native_v, JniHandleOwnership.DoNotTransfer);
				__this.OnClick (v);
			}
#pragma warning restore 0169

			IntPtr id_onClick_Landroid_view_View_;
			public unsafe void OnClick (Android.Views.View v)
			{
				if (id_onClick_Landroid_view_View_ == IntPtr.Zero)
					id_onClick_Landroid_view_View_ = JNIEnv.GetMethodID (class_ref, "onClick", "(Landroid/view/View;)V");
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (v);
				JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_onClick_Landroid_view_View_, __args);
			}

		}

		[global::Android.Runtime.Register ("mono/android/view/View_OnClickListenerImplementor")]
		internal sealed partial class IOnClickListenerImplementor : global::Java.Lang.Object, IOnClickListener {

			public IOnClickListenerImplementor ()
				: base (
					global::Android.Runtime.JNIEnv.StartCreateInstance ("mono/android/view/View_OnClickListenerImplementor", "()V"),
					JniHandleOwnership.TransferLocalRef)
			{
				global::Android.Runtime.JNIEnv.FinishCreateInstance (((global::Java.Lang.Object) this).Handle, "()V");
			}

#pragma warning disable 0649
			public EventHandler Handler;
#pragma warning restore 0649

			public void OnClick (Android.Views.View v)
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


		internal static new IntPtr java_class_handle;
		internal static new IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("android/view/View", ref java_class_handle);
			}
		}

		protected override IntPtr ThresholdClass {
			get { return class_ref; }
		}

		protected override global::System.Type ThresholdType {
			get { return typeof (View); }
		}

		protected View (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) {}

		static Delegate cb_setOnClickListener_Landroid_view_View_OnClickListener_;
#pragma warning disable 0169
		static Delegate GetSetOnClickListener_Landroid_view_View_OnClickListener_Handler ()
		{
			if (cb_setOnClickListener_Landroid_view_View_OnClickListener_ == null)
				cb_setOnClickListener_Landroid_view_View_OnClickListener_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_SetOnClickListener_Landroid_view_View_OnClickListener_);
			return cb_setOnClickListener_Landroid_view_View_OnClickListener_;
		}

		static void n_SetOnClickListener_Landroid_view_View_OnClickListener_ (IntPtr jnienv, IntPtr native__this, IntPtr native_l)
		{
			Android.Views.View __this = global::Java.Lang.Object.GetObject<Android.Views.View> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			Android.Views.View.IOnClickListener l = (Android.Views.View.IOnClickListener)global::Java.Lang.Object.GetObject<Android.Views.View.IOnClickListener> (native_l, JniHandleOwnership.DoNotTransfer);
			__this.SetOnClickListener (l);
		}
#pragma warning restore 0169

		static IntPtr id_setOnClickListener_Landroid_view_View_OnClickListener_;
		// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/class[@name='View']/method[@name='setOnClickListener' and count(parameter)=1 and parameter[1][@type='android.view.View.OnClickListener']]"
		[Register ("setOnClickListener", "(Landroid/view/View$OnClickListener;)V", "GetSetOnClickListener_Landroid_view_View_OnClickListener_Handler")]
		public virtual unsafe void SetOnClickListener (Android.Views.View.IOnClickListener l)
		{
			if (id_setOnClickListener_Landroid_view_View_OnClickListener_ == IntPtr.Zero)
				id_setOnClickListener_Landroid_view_View_OnClickListener_ = JNIEnv.GetMethodID (class_ref, "setOnClickListener", "(Landroid/view/View$OnClickListener;)V");
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (l);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_setOnClickListener_Landroid_view_View_OnClickListener_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "setOnClickListener", "(Landroid/view/View$OnClickListener;)V"), __args);
			} finally {
			}
		}

		static Delegate cb_addTouchables_Ljava_util_ArrayList_;
#pragma warning disable 0169
		static Delegate GetAddTouchables_Ljava_util_ArrayList_Handler ()
		{
			if (cb_addTouchables_Ljava_util_ArrayList_ == null)
				cb_addTouchables_Ljava_util_ArrayList_ = JNINativeWrapper.CreateDelegate ((Action<IntPtr, IntPtr, IntPtr>) n_AddTouchables_Ljava_util_ArrayList_);
			return cb_addTouchables_Ljava_util_ArrayList_;
		}

		static void n_AddTouchables_Ljava_util_ArrayList_ (IntPtr jnienv, IntPtr native__this, IntPtr native_views)
		{
			Android.Views.View __this = global::Java.Lang.Object.GetObject<Android.Views.View> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
			var views = Android.Runtime.JavaList<Android.Views.View>.FromJniHandle (native_views, JniHandleOwnership.DoNotTransfer);
			__this.AddTouchables (views);
		}
#pragma warning restore 0169

		static IntPtr id_addTouchables_Ljava_util_ArrayList_;
		// Metadata.xml XPath method reference: path="/api/package[@name='android.view']/class[@name='View']/method[@name='addTouchables' and count(parameter)=1 and parameter[1][@type='java.util.ArrayList&lt;android.view.View&gt;']]"
		[Register ("addTouchables", "(Ljava/util/ArrayList;)V", "GetAddTouchables_Ljava_util_ArrayList_Handler")]
		public virtual unsafe void AddTouchables (System.Collections.Generic.IList<Android.Views.View> views)
		{
			if (id_addTouchables_Ljava_util_ArrayList_ == IntPtr.Zero)
				id_addTouchables_Ljava_util_ArrayList_ = JNIEnv.GetMethodID (class_ref, "addTouchables", "(Ljava/util/ArrayList;)V");
			IntPtr native_views = Android.Runtime.JavaList<Android.Views.View>.ToLocalJniHandle (views);
			try {
				JValue* __args = stackalloc JValue [1];
				__args [0] = new JValue (native_views);

				if (((object) this).GetType () == ThresholdType)
					JNIEnv.CallVoidMethod (((global::Java.Lang.Object) this).Handle, id_addTouchables_Ljava_util_ArrayList_, __args);
				else
					JNIEnv.CallNonvirtualVoidMethod (((global::Java.Lang.Object) this).Handle, ThresholdClass, JNIEnv.GetMethodID (ThresholdClass, "addTouchables", "(Ljava/util/ArrayList;)V"), __args);
			} finally {
				JNIEnv.DeleteLocalRef (native_views);
			}
		}

#region "Event implementation for Android.Views.View.IOnClickListener"
		public event EventHandler Click {
			add {
				global::Java.Interop.EventHelper.AddEventHandler<Android.Views.View.IOnClickListener, Android.Views.View.IOnClickListenerImplementor>(
						ref weak_implementor_SetOnClickListener,
						__CreateIOnClickListenerImplementor,
						SetOnClickListener,
						__h => __h.Handler += value);
			}
			remove {
				global::Java.Interop.EventHelper.RemoveEventHandler<Android.Views.View.IOnClickListener, Android.Views.View.IOnClickListenerImplementor>(
						ref weak_implementor_SetOnClickListener,
						Android.Views.View.IOnClickListenerImplementor.__IsEmpty,
						__v => SetOnClickListener (null),
						__h => __h.Handler -= value);
			}
		}

		WeakReference weak_implementor_SetOnClickListener;

		Android.Views.View.IOnClickListenerImplementor __CreateIOnClickListenerImplementor ()
		{
			return new Android.Views.View.IOnClickListenerImplementor ();
		}
#endregion
	}
}
