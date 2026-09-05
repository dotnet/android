using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Android.Runtime;

using Java.Interop;

namespace Android.Util
{
	[Register ("android/util/SparseArray", DoNotGenerateAcw=true)]
	public partial class SparseArray<
			[DynamicallyAccessedMembers (Constructors)]
			E
	> : SparseArray
	{
		[UnsafeAccessor (UnsafeAccessorKind.StaticField, Name = "_members")]
		static extern ref readonly JniPeerMembers GetPeerMembers (SparseArray? _);

		public SparseArray ()
		{
		}
		
		public SparseArray (int capacity)
			: base (capacity)
		{
		}
		
		static IntPtr id_append_ILjava_lang_Object_;
		[Register ("append", "(ILjava/lang/Object;)V", "")]
		public virtual void Append (int key, E value)
		{
			if (id_append_ILjava_lang_Object_ == IntPtr.Zero)
				id_append_ILjava_lang_Object_ = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "put", "(ILjava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (value, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_append_ILjava_lang_Object_, new JValue (key), new JValue (lref));
					return IntPtr.Zero;
			});
		}
		
		static IntPtr id_get_I;
		[Register ("get", "(I)Ljava/lang/Object;", "")]
		[return: MaybeNull]
		public new virtual E Get (int key)
		{
			if (id_get_I == IntPtr.Zero)
				id_get_I = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "get", "(I)Ljava/lang/Object;");
			return JavaConvert.FromJniHandle<E>(JNIEnv.CallNonvirtualObjectMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_get_I, new JValue (key)), JniHandleOwnership.TransferLocalRef);
		}

		static IntPtr id_get_ILjava_lang_Object_;
		[Register ("get", "(ILjava/lang/Object;)Ljava/lang/Object;", "")]
		[return: MaybeNull]
		public virtual E Get (int key, E valueIfKeyNotFound)
		{
			if (id_get_ILjava_lang_Object_ == IntPtr.Zero)
				id_get_ILjava_lang_Object_ = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "get", "(ILjava/lang/Object;)Ljava/lang/Object;");
			IntPtr value = JavaConvert.WithLocalJniHandle (valueIfKeyNotFound,
					lref => JNIEnv.CallNonvirtualObjectMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_get_ILjava_lang_Object_, new JValue (key), new JValue (lref)));
			return JavaConvert.FromJniHandle<E> (value, JniHandleOwnership.TransferLocalRef);
		}

		static IntPtr id_indexOfValue_Ljava_lang_Object_;
		[Register ("indexOfValue", "(Ljava/lang/Object;)I", "")]
		public virtual int IndexOfValue (E value)
		{
			if (id_indexOfValue_Ljava_lang_Object_ == IntPtr.Zero)
				id_indexOfValue_Ljava_lang_Object_ = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "indexOfValue", "(Ljava/lang/Object;)I");
			return JavaConvert.WithLocalJniHandle (value,
					lref => JNIEnv.CallNonvirtualIntMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_indexOfValue_Ljava_lang_Object_, new JValue (lref)));
		}

		static IntPtr id_put_ILjava_lang_Object_;
		[Register ("put", "(ILjava/lang/Object;)V", "")]
		public virtual void Put (int key, E value)
		{
			if (id_put_ILjava_lang_Object_ == IntPtr.Zero)
				id_put_ILjava_lang_Object_ = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "put", "(ILjava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (value, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_put_ILjava_lang_Object_, new JValue (key), new JValue (lref));
					return IntPtr.Zero;
			});
		}

		static IntPtr id_setValueAt_ILjava_lang_Object_;
		[Register ("setValueAt", "(ILjava/lang/Object;)V", "")]
		public virtual void SetValueAt (int index, E value)
		{
			if (id_setValueAt_ILjava_lang_Object_ == IntPtr.Zero)
				id_setValueAt_ILjava_lang_Object_ = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "setValueAt", "(ILjava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (value, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_setValueAt_ILjava_lang_Object_, new JValue (index), new JValue (lref));
					return IntPtr.Zero;
			});
		}
		
		static IntPtr id_valueAt_I;
		[Register ("valueAt", "(I)Ljava/lang/Object;", "")]
		[return: MaybeNull]
		public new virtual E ValueAt (int index)
		{
			if (id_valueAt_I == IntPtr.Zero)
				id_valueAt_I = JNIEnv.GetMethodID (GetPeerMembers (null).JniPeerType.PeerReference.Handle, "valueAt", "(I)Ljava/lang/Object;");
			return JavaConvert.FromJniHandle<E> (
					JNIEnv.CallNonvirtualObjectMethod (Handle, GetPeerMembers (null).JniPeerType.PeerReference.Handle, id_valueAt_I, new JValue (index)),
					JniHandleOwnership.TransferLocalRef);
		}
	}
}
