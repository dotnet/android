using System;

using Android.Runtime;

using Java.Interop;

namespace Android.Widget {

	[Register ("android/widget/BaseAdapter", DoNotGenerateAcw=true)]
	public abstract partial class BaseAdapter<T> : BaseAdapter {

		public BaseAdapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register (".ctor", "()V", "")]
		public BaseAdapter ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			unsafe {
				// SAFETY: the constructor uses the literal JNI signature with the matching argument order; this peer owns its newly allocated handle through initialization and existing cleanup.
				if (Handle != IntPtr.Zero)
					return;

				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "()V");
			}
		}

		public override Java.Lang.Object? GetItem (int position)
		{
			return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (this [position]));
		}

		public abstract T this [int position] { [Register ("getItem", "(I)Ljava/lang/Object;", "GetGetItem_IHandler")] get; }

	}
}
