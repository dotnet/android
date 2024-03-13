using System;
using System.Diagnostics.CodeAnalysis;

using Android.Runtime;

using Java.Interop;

namespace Android.Util
{
	[Register ("android/util/SparseArray", DoNotGenerateAcw=true)]
	public partial class SparseArray<
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			E
	> : SparseArray
	{
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
				id_append_ILjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "put", "(ILjava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (value, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_append_ILjava_lang_Object_, new JValue (key), new JValue (lref));
					return IntPtr.Zero;
			});
		}
		
		static IntPtr id_get_I;
		[Register ("get", "(I)Ljava/lang/Object;", "")]
		public virtual E Get (int key)
		{
			if (id_get_I == IntPtr.Zero)
				id_get_I = JNIEnv.GetMethodID (class_ref, "get", "(I)Ljava/lang/Object;");
			return JavaConvert.FromJniHandle<E>(JNIEnv.CallNonvirtualObjectMethod (Handle, class_ref, id_get_I, new JValue (key)), JniHandleOwnership.TransferLocalRef)!;
		}

		static IntPtr id_get_ILjava_lang_Object_;
		[Register ("get", "(ILjava/lang/Object;)Ljava/lang/Object;", "")]
		public virtual E Get (int key, E valueIfKeyNotFound)
		{
			if (id_get_ILjava_lang_Object_ == IntPtr.Zero)
				id_get_ILjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "get", "(ILjava/lang/Object;)Ljava/lang/Object;");
			IntPtr value = JavaConvert.WithLocalJniHandle (valueIfKeyNotFound,
					lref => JNIEnv.CallNonvirtualObjectMethod (Handle, class_ref, id_get_ILjava_lang_Object_, new JValue (key), new JValue (lref)));
			return JavaConvert.FromJniHandle<E> (value, JniHandleOwnership.TransferLocalRef)!;
		}

		static IntPtr id_indexOfValue_Ljava_lang_Object_;
		[Register ("indexOfValue", "(Ljava/lang/Object;)I", "")]
		public virtual int IndexOfValue (E value)
		{
			if (id_indexOfValue_Ljava_lang_Object_ == IntPtr.Zero)
				id_indexOfValue_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "indexOfValue", "(Ljava/lang/Object;)I");
			return JavaConvert.WithLocalJniHandle (value,
					lref => JNIEnv.CallNonvirtualIntMethod (Handle, class_ref, id_indexOfValue_Ljava_lang_Object_, new JValue (lref)));
		}

		static IntPtr id_put_ILjava_lang_Object_;
		[Register ("put", "(ILjava/lang/Object;)V", "")]
		public virtual void Put (int key, E value)
		{
			if (id_put_ILjava_lang_Object_ == IntPtr.Zero)
				id_put_ILjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "put", "(ILjava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (value, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_put_ILjava_lang_Object_, new JValue (key), new JValue (lref));
					return IntPtr.Zero;
			});
		}

		static IntPtr id_setValueAt_ILjava_lang_Object_;
		[Register ("setValueAt", "(ILjava/lang/Object;)V", "")]
		public virtual void SetValueAt (int index, E value)
		{
			if (id_setValueAt_ILjava_lang_Object_ == IntPtr.Zero)
				id_setValueAt_ILjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "setValueAt", "(ILjava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (value, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_setValueAt_ILjava_lang_Object_, new JValue (index), new JValue (lref));
					return IntPtr.Zero;
			});
		}
		
		static IntPtr id_valueAt_I;
		[Register ("valueAt", "(I)Ljava/lang/Object;", "")]
		public virtual E ValueAt (int index)
		{
			if (id_valueAt_I == IntPtr.Zero)
				id_valueAt_I = JNIEnv.GetMethodID (class_ref, "valueAt", "(I)Ljava/lang/Object;");
			return JavaConvert.FromJniHandle<E> (
					JNIEnv.CallNonvirtualObjectMethod (Handle, class_ref, id_valueAt_I, new JValue (index)),
					JniHandleOwnership.TransferLocalRef)!;
		}
	}
}
