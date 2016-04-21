using System;

using Android.Runtime;

using Java.Interop;

namespace Android.Widget {

	[Register ("android/widget/BaseAdapter", DoNotGenerateAcw=true)]
	public abstract partial class BaseAdapter<T> : BaseAdapter {

		static IntPtr java_class_handle;
		static IntPtr class_ref {
			get {
				return JNIEnv.FindClass ("android/widget/BaseAdapter", ref java_class_handle);
			}
		}

		public BaseAdapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static IntPtr id_ctor;
		[Register (".ctor", "()V", "")]
		public BaseAdapter ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () != typeof (BaseAdapter)) {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "()V");
				return;
			}

			if (id_ctor == IntPtr.Zero)
				id_ctor = JNIEnv.GetMethodID (class_ref, "<init>", "()V");
			SetHandle (
					JNIEnv.StartCreateInstance (class_ref, id_ctor),
					JniHandleOwnership.TransferLocalRef);
			JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor);
		}

		public override Java.Lang.Object GetItem (int position)
		{
			return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (this [position]));
		}

		public abstract T this [int position] { [Register ("getItem", "(I)Ljava/lang/Object;", "GetGetItem_IHandler")] get; }

	}
}

