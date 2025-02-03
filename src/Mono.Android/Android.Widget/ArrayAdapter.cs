using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

using Java.Interop;

namespace Android.Widget {

	[Register ("android/widget/ArrayAdapter", DoNotGenerateAcw=true)]
	public partial class ArrayAdapter<
			[DynamicallyAccessedMembers (Constructors)]
			T
	> : ArrayAdapter {

		public ArrayAdapter (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		static IntPtr id_ctor_Landroid_content_Context_I;
		[Register (".ctor", "(Landroid/content/Context;I)V", "")]
		public ArrayAdapter (Android.Content.Context context, int textViewResourceId)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (ArrayAdapter<T>)) {
				if (id_ctor_Landroid_content_Context_I == IntPtr.Zero)
					id_ctor_Landroid_content_Context_I = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;I)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_I, new JValue (context), new JValue (textViewResourceId)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_I, new JValue (context), new JValue (textViewResourceId));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;I)V", new JValue (context), new JValue (textViewResourceId)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;I)V", new JValue (context), new JValue (textViewResourceId));
			}
		}

		static IntPtr id_ctor_Landroid_content_Context_II;
		[Register (".ctor", "(Landroid/content/Context;II)V", "")]
		public ArrayAdapter (Android.Content.Context context, int resource, int textViewResourceId)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (ArrayAdapter<T>)) {
				if (id_ctor_Landroid_content_Context_II == IntPtr.Zero)
					id_ctor_Landroid_content_Context_II = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;II)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_II, new JValue (context), new JValue (resource), new JValue (textViewResourceId)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_II, new JValue (context), new JValue (resource), new JValue (textViewResourceId));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;II)V", new JValue (context), new JValue (resource), new JValue (textViewResourceId)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;II)V", new JValue (context), new JValue (resource), new JValue (textViewResourceId));
			}
		}

		static IntPtr id_ctor_Landroid_content_Context_IarrayLjava_lang_Object_;
		[Register (".ctor", "(Landroid/content/Context;I[Ljava/lang/Object;)V", "")]
		public ArrayAdapter (Android.Content.Context context, int textViewResourceId, T[] objects)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			IntPtr native_objects = JNIEnv.NewObjectArray (objects);
			if (GetType () == typeof (ArrayAdapter<T>)) {
				if (id_ctor_Landroid_content_Context_IarrayLjava_lang_Object_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_IarrayLjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;I[Ljava/lang/Object;)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_IarrayLjava_lang_Object_, new JValue (context), new JValue (textViewResourceId), new JValue (native_objects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_IarrayLjava_lang_Object_, new JValue (context), new JValue (textViewResourceId), new JValue (native_objects));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;I[Ljava/lang/Object;)V", new JValue (context), new JValue (textViewResourceId), new JValue (native_objects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;I[Ljava/lang/Object;)V", new JValue (context), new JValue (textViewResourceId), new JValue (native_objects));
			}
			JNIEnv.DeleteLocalRef (native_objects);
		}

		static IntPtr id_ctor_Landroid_content_Context_IIarrayLjava_lang_Object_;
		[Register (".ctor", "(Landroid/content/Context;II[Ljava/lang/Object;)V", "")]
		public ArrayAdapter (Android.Content.Context context, int resource, int textViewResourceId, T[] objects)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			IntPtr native_objects = JNIEnv.NewObjectArray<T> (objects);;
			if (GetType () == typeof (ArrayAdapter<T>)) {
				if (id_ctor_Landroid_content_Context_IIarrayLjava_lang_Object_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_IIarrayLjava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;II[Ljava/lang/Object;)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_IIarrayLjava_lang_Object_, new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (native_objects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_IIarrayLjava_lang_Object_, new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (native_objects));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;II[Ljava/lang/Object;)V", new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (native_objects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;II[Ljava/lang/Object;)V", new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (native_objects));
			}
			JNIEnv.DeleteLocalRef (native_objects);
		}

		static IntPtr id_ctor_Landroid_content_Context_ILjava_util_List_;
		[Register (".ctor", "(Landroid/content/Context;ILjava/util/List;)V", "")]
		public ArrayAdapter (Android.Content.Context context, int textViewResourceId, System.Collections.Generic.IList<T> objects)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			IntPtr lrefObjects = JavaList<T>.ToLocalJniHandle (objects);
			if (GetType () == typeof (ArrayAdapter<T>)) {
				if (id_ctor_Landroid_content_Context_ILjava_util_List_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_ILjava_util_List_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;ILjava/util/List;)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_ILjava_util_List_, new JValue (context), new JValue (textViewResourceId), new JValue (lrefObjects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_ILjava_util_List_, new JValue (context), new JValue (textViewResourceId), new JValue (lrefObjects));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;ILjava/util/List;)V", new JValue (context), new JValue (textViewResourceId), new JValue (lrefObjects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;ILjava/util/List;)V", new JValue (context), new JValue (textViewResourceId), new JValue (lrefObjects));
			}
			JNIEnv.DeleteLocalRef (lrefObjects);
		}

		static IntPtr id_ctor_Landroid_content_Context_IILjava_util_List_;
		[Register (".ctor", "(Landroid/content/Context;IILjava/util/List;)V", "")]
		public ArrayAdapter (Android.Content.Context context, int resource, int textViewResourceId, System.Collections.Generic.IList<T> objects)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			IntPtr lrefObjects = JavaList<T>.ToLocalJniHandle (objects);
			if (GetType () == typeof (ArrayAdapter<T>)) {
				if (id_ctor_Landroid_content_Context_IILjava_util_List_ == IntPtr.Zero)
					id_ctor_Landroid_content_Context_IILjava_util_List_ = JNIEnv.GetMethodID (class_ref, "<init>", "(Landroid/content/Context;IILjava/util/List;)V");
				SetHandle (
						JNIEnv.StartCreateInstance (class_ref, id_ctor_Landroid_content_Context_IILjava_util_List_, new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (lrefObjects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, class_ref, id_ctor_Landroid_content_Context_IILjava_util_List_, new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (lrefObjects));
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "(Landroid/content/Context;IILjava/util/List;)V", new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (lrefObjects)),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "(Landroid/content/Context;IILjava/util/List;)V", new JValue (context), new JValue (resource), new JValue (textViewResourceId), new JValue (lrefObjects));
			}
			JNIEnv.DeleteLocalRef (lrefObjects);
		}

		static IntPtr id_add_Ljava_lang_Object_;
		[Register ("add", "(Ljava/lang/Object;)V", "GetAdd_Ljava_lang_Object_Handler")]
		public void Add (T @object)
		{
			if (id_add_Ljava_lang_Object_ == IntPtr.Zero)
				id_add_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "add", "(Ljava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (@object, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_add_Ljava_lang_Object_, new JValue (lref));
					return IntPtr.Zero;
			});
		}

		static IntPtr id_createFromResource_Landroid_content_Context_II;
		[Register ("createFromResource", "(Landroid/content/Context;II)Landroid/widget/ArrayAdapter;", "")]
		public static Android.Widget.ArrayAdapter<Java.Lang.ICharSequence> CreateFromResource (Android.Content.Context context, int textArrayResId, int textViewResId)
		{
			if (id_createFromResource_Landroid_content_Context_II == IntPtr.Zero)
				id_createFromResource_Landroid_content_Context_II = JNIEnv.GetStaticMethodID (class_ref, "createFromResource", "(Landroid/content/Context;II)Landroid/widget/ArrayAdapter;");
			return JavaConvert.FromJniHandle<ArrayAdapter<Java.Lang.ICharSequence>> (
					JNIEnv.CallStaticObjectMethod (class_ref, id_createFromResource_Landroid_content_Context_II, new JValue (context), new JValue (textArrayResId), new JValue (textViewResId)),
						JniHandleOwnership.TransferLocalRef)!;
		}

		static IntPtr id_getItem_I;
		[Register ("getItem", "(I)Ljava/lang/Object;", "GetGetItem_IHandler")]
		public T? GetItem (int position)
		{
			if (id_getItem_I == IntPtr.Zero)
				id_getItem_I = JNIEnv.GetMethodID (class_ref, "getItem", "(I)Ljava/lang/Object;");
			return JavaConvert.FromJniHandle<T>(
					JNIEnv.CallNonvirtualObjectMethod (Handle, class_ref, id_getItem_I, new JValue (position)),
					JniHandleOwnership.TransferLocalRef);
		}

		static IntPtr id_getPosition_Ljava_lang_Object_;
		[Register ("getPosition", "(Ljava/lang/Object;)I", "GetGetPosition_Ljava_lang_Object_Handler")]
		public int GetPosition (T item)
		{
			if (id_getPosition_Ljava_lang_Object_ == IntPtr.Zero)
				id_getPosition_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "getPosition", "(Ljava/lang/Object;)I");
			return JavaConvert.WithLocalJniHandle (item,
					lref => JNIEnv.CallNonvirtualIntMethod (Handle, class_ref, id_getPosition_Ljava_lang_Object_, new JValue (lref)));
		}

		static IntPtr id_insert_Ljava_lang_Object_I;
		[Register ("insert", "(Ljava/lang/Object;I)V", "GetInsert_Ljava_lang_Object_IHandler")]
		public void Insert (T @object, int index)
		{
			if (id_insert_Ljava_lang_Object_I == IntPtr.Zero)
				id_insert_Ljava_lang_Object_I = JNIEnv.GetMethodID (class_ref, "insert", "(Ljava/lang/Object;I)V");
			JavaConvert.WithLocalJniHandle (@object, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_insert_Ljava_lang_Object_I, new JValue (lref), new JValue (index));
					return IntPtr.Zero;
			});
		}

		static IntPtr id_remove_Ljava_lang_Object_;
		[Register ("remove", "(Ljava/lang/Object;)V", "GetRemove_Ljava_lang_Object_Handler")]
		public void Remove (T @object)
		{
			if (id_remove_Ljava_lang_Object_ == IntPtr.Zero)
				id_remove_Ljava_lang_Object_ = JNIEnv.GetMethodID (class_ref, "remove", "(Ljava/lang/Object;)V");
			JavaConvert.WithLocalJniHandle (@object, lref => {
					JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_remove_Ljava_lang_Object_, new JValue (lref));
					return IntPtr.Zero;
			});
		}

		static IntPtr id_sort_Ljava_util_Comparator_;
		[Register ("sort", "(Ljava/util/Comparator;)V", "GetSort_Ljava_util_Comparator_Handler")]
		public void Sort (Java.Util.IComparator comparator)
		{
			if (id_sort_Ljava_util_Comparator_ == IntPtr.Zero)
				id_sort_Ljava_util_Comparator_ = JNIEnv.GetMethodID (class_ref, "sort", "(Ljava/util/Comparator;)V");
			JNIEnv.CallNonvirtualVoidMethod (Handle, class_ref, id_sort_Ljava_util_Comparator_, new JValue (comparator));
		}
	}
}
