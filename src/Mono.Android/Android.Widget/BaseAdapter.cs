using System;

using Android.Runtime;

using Java.Interop;

namespace Android.Widget {

	[Register ("android/widget/BaseAdapter", DoNotGenerateAcw=true)]
	public abstract partial class BaseAdapter<T> : BaseAdapter {

		/// <safety>
		/// A nonzero <paramref name="handle"/> must be a live BaseAdapter-compatible reference from this VM
		/// usable on this thread until construction returns or throws, with elements compatible with T's conversion.
		/// Own the requested transfer and exclude release/use of consumed aliases; zero preserves empty-peer initialization.
		/// Caller-unsafe enforcement on raw peer constructors is deferred; existing initialization APIs are unchanged.
		/// </safety>
		public BaseAdapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register (".ctor", "()V", "")]
		public BaseAdapter ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			unsafe {
				// SAFETY: StartCreateInstance supplies this peer's owned handle; FinishCreateInstance invokes the ()V constructor with the matching arguments before this constructor returns.
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
