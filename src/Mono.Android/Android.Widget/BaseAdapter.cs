using System;
using System.Runtime.CompilerServices;

using Android.Runtime;

using Java.Interop;
namespace Android.Widget {

	[Register ("android/widget/BaseAdapter", DoNotGenerateAcw=true)]
	public abstract partial class BaseAdapter<T> : BaseAdapter {

		[UnsafeAccessor (UnsafeAccessorKind.StaticField, Name = "_members")]
		static extern ref readonly JniPeerMembers GetPeerMembers (BaseAdapter? _);

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
				id_ctor = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "<init>", "()V");
			SetHandle (
					JNIEnv.StartCreateInstance (GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_ctor),
					JniHandleOwnership.TransferLocalRef);
			JNIEnv.FinishCreateInstance (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_ctor);
		}

		public override Java.Lang.Object? GetItem (int position)
		{
			return JavaObjectExtensions.JavaCast<Java.Lang.Object>(JavaConvert.ToJavaObject (this [position]));
		}

		public abstract T this [int position] { [Register ("getItem", "(I)Ljava/lang/Object;", "GetGetItem_IHandler")] get; }

	}
}
