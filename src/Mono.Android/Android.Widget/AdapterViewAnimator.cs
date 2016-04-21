#if ANDROID_12
using System;
using System.Collections.Generic;
using Android.Runtime;
using Android.Views;
using JLO = Java.Lang.Object;

using Java.Interop;

namespace Android.Widget {

	//[Register ("android/widget/AdapterViewAnimator", DoNotGenerateAcw=true)]
	//public abstract partial class AdapterViewAnimator<T> : AdapterViewAnimator
	public abstract partial class AdapterViewAnimator
	{
		/*
		public AdapterViewAnimator (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static IntPtr id_ctor_Landroid_content_Context_;
		[Register (".ctor", "(Landroid/content/Context;)V", "")]
		public AdapterViewAnimator (Android.Content.Context context)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (GetType () == typeof (AdapterViewAnimator<T>)) {
				if (id_ctor_Landroid_content_Context_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;)V");
				SetHandle (
						JNIEnv.NewObject (class_ref, id_ctor_Landroid_content_Context_, new JValue (context)),
						JniHandleOwnership.TransferLocalRef);
			} else {
				SetHandle (
						JNIEnv.CreateInstance (GetType (), "(Landroid/content/Context;)V", new JValue (context)),
						JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_;
		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;)V", "")]
		public AdapterViewAnimator (Android.Content.Context context, Android.Util.IAttributeSet attrs)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (GetType () == typeof (AdapterViewAnimator<T>)) {
				if (id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;Landroid/util/AttributeSet;)V");
				SetHandle (
						JNIEnv.NewObject (class_ref, id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_, new JValue (context), new JValue (attrs)),
						JniHandleOwnership.TransferLocalRef);
			} else {
				SetHandle (
						JNIEnv.CreateInstance (GetType (), "(Landroid/content/Context;Landroid/util/AttributeSet;)V", new JValue (context), new JValue (attrs)),
						JniHandleOwnership.TransferLocalRef);
			}
		}

		static IntPtr id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I;
		[Register (".ctor", "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", "")]
		public AdapterViewAnimator (Android.Content.Context context, Android.Util.IAttributeSet attrs, int defStyle)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (GetType () == typeof (AdapterViewAnimator<T>)) {
				if (id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I == IntPtr.Zero)
					id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;Landroid/util/AttributeSet;I)V");
				SetHandle (
						JNIEnv.NewObject (class_ref, id_ctor_Landroid_content_Context_Landroid_util_AttributeSet_I, new JValue (context), new JValue (attrs), new JValue (defStyle)),
						JniHandleOwnership.TransferLocalRef);
			} else {
				SetHandle (
						JNIEnv.CreateInstance (GetType (), "(Landroid/content/Context;Landroid/util/AttributeSet;I)V", new JValue (context), new JValue (attrs), new JValue (defStyle)),
						JniHandleOwnership.TransferLocalRef);
			}
		}
		*/

		protected override Java.Lang.Object RawAdapter {
			get { return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (Adapter)); }
			set { Adapter = JavaConvert.FromJavaObject<Android.Widget.IAdapter>(value); }
		}

                static IntPtr id_getAdapter;
                static IntPtr id_setAdapter_Landroid_widget_Adapter_;
                public Android.Widget.IAdapter Adapter {
                        [Register ("getAdapter", "()Landroid/widget/Adapter;", "GetGetAdapterHandler")]
                        get {
                                if (id_getAdapter == IntPtr.Zero)
                                        id_getAdapter = JNIEnv.GetMethodID (class_ref, "getAdapter", "()Landroid/widget/Adapter;");
                                if (GetType () == ThresholdType)
                                        return Java.Lang.Object.GetObject<Android.Widget.IAdapter> (JNIEnv.CallObjectMethod  (Handle, id_getAdapter), JniHandleOwnership.TransferLocalRef);
                                else
                                        return Java.Lang.Object.GetObject<Android.Widget.IAdapter> (
                                                JNIEnv.CallNonvirtualObjectMethod  (
                                                    Handle,
                                                    ThresholdClass,
                                                    JNIEnv.GetMethodID (ThresholdClass, "getAdapter", "()Landroid/widget/Adapter;")),
                                                JniHandleOwnership.TransferLocalRef);
                        }
                        set {
                                if (id_setAdapter_Landroid_widget_Adapter_ == IntPtr.Zero)
                                        id_setAdapter_Landroid_widget_Adapter_ = JNIEnv.GetMethodID (class_ref, "setAdapter", "(Landroid/widget/Adapter;)V");

                                if (GetType () == ThresholdType)
                                        JNIEnv.CallVoidMethod  (Handle, id_setAdapter_Landroid_widget_Adapter_, new JValue (JNIEnv.ToJniHandle (value)));
                                else
                                        JNIEnv.CallNonvirtualVoidMethod  (
                                                Handle,
                                                ThresholdClass,
                                                JNIEnv.GetMethodID (ThresholdClass, "setAdapter", "(Landroid/widget/Adapter;)V"),
                                                new JValue (JNIEnv.ToJniHandle ((IJavaObject) value)));
                        }

                }
	}
}

#endif
