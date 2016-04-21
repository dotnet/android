using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Android.Runtime;

using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/Collection", DoNotGenerateAcw=true)]
	public class JavaCollection : Java.Lang.Object, System.Collections.ICollection {

		internal static IntPtr collection_class = JNIEnv.FindClass ("java/util/Collection");

		internal static IntPtr id_add;
		static IntPtr id_iterator;
		static IntPtr id_size;
		internal static IntPtr id_toArray;

		public JavaCollection (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		internal JavaCollection (IEnumerable items)
			: base (
					JNIEnv.StartCreateInstance ("java/util/ArrayList", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");

			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

			foreach (object item in items)
				Add (item);
		}

		internal void Add (object item)
		{
			if (id_add == IntPtr.Zero)
				id_add = JNIEnv.GetMethodID (collection_class, "add", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {
					JNIEnv.CallBooleanMethod (Handle, id_add, new JValue (lref));
					return IntPtr.Zero;
			});
		}

		internal Java.Util.IIterator Iterator ()
		{
			if (id_iterator == IntPtr.Zero)
				id_iterator = JNIEnv.GetMethodID (collection_class, "iterator", "()Ljava/util/Iterator;");
			return Java.Lang.Object.GetObject<Java.Util.IIterator> (
					JNIEnv.CallObjectMethod (Handle, id_iterator),
					JniHandleOwnership.TransferLocalRef);
		}

		public int Count {
			get {
				if (id_size == IntPtr.Zero)
					id_size = JNIEnv.GetMethodID (collection_class, "size", "()I");
				return JNIEnv.CallIntMethod (Handle, id_size);
			}
		}

		public bool IsSynchronized {
			get {return false;}
		}

		public object SyncRoot {
			get {return null;}
		}

		internal Java.Lang.Object[] ToArray ()
		{
			if (id_toArray == IntPtr.Zero)
				id_toArray = JNIEnv.GetMethodID (collection_class, "toArray", "()[Ljava/lang/Object;");
			using (var o = new Java.Lang.Object (JNIEnv.CallObjectMethod (Handle, id_toArray),
					JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
				return (Java.Lang.Object[]) o;
		}

		public void CopyTo (Array array, int array_index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			if (array.Length < array_index + Count)
				throw new ArgumentException ("array");

			if (id_toArray == IntPtr.Zero)
				id_toArray = JNIEnv.GetMethodID (collection_class, "toArray", "()[Ljava/lang/Object;");

			IntPtr lrefArray = JNIEnv.CallObjectMethod (Handle, id_toArray);
			for (int i = 0; i < Count; i++)
				array.SetValue (
						JavaConvert.FromJniHandle (
							JNIEnv.GetObjectArrayElement (lrefArray, i),
							JniHandleOwnership.TransferLocalRef,
							array.GetType ().GetElementType ()),
						array_index + i);
			JNIEnv.DeleteLocalRef (lrefArray);
		}

		public IEnumerator GetEnumerator ()
		{
			return System.Linq.Extensions.ToEnumerator_Dispose (Iterator ());
		}

		[Preserve (Conditional=true)]
		public static ICollection FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			IJavaObject inst = Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaCollection (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (ICollection) inst;
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (ICollection items)
		{
			if (items == null)
				return IntPtr.Zero;

			var c = items as JavaCollection;
			if (c != null)
				return JNIEnv.ToLocalJniHandle (c);

			using (c = new JavaCollection (items))
				return JNIEnv.ToLocalJniHandle (c);
		}
	}

	[Register ("java/util/Collection", DoNotGenerateAcw=true)]
	public sealed class JavaCollection<T> : JavaCollection, ICollection<T> {

		public JavaCollection (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		internal JavaCollection (IEnumerable<T> items)
			: base (
					JNIEnv.StartCreateInstance ("java/util/ArrayList", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");

			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

			foreach (T item in items)
				Add (item);
		}

		static IntPtr id_clear;
		static IntPtr id_contains;
		static IntPtr id_remove;

		public bool IsReadOnly {
			get {return false;}
		}

		public void Add (T item)
		{
			if (id_add == IntPtr.Zero)
				id_add = JNIEnv.GetMethodID (collection_class, "add", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {
					JNIEnv.CallBooleanMethod (Handle, id_add, new JValue (lref));
					return IntPtr.Zero;
			});
		}

		public void Clear ()
		{
			if (id_clear == IntPtr.Zero)
				id_clear = JNIEnv.GetMethodID (collection_class, "clear", "()V");
			JNIEnv.CallVoidMethod (Handle, id_clear);
		}

		public bool Contains (T item)
		{
			if (id_contains == IntPtr.Zero)
				id_contains = JNIEnv.GetMethodID (collection_class, "contains", "(Ljava/lang/Object;)Z");
			return JavaConvert.WithLocalJniHandle (item,
					lref => JNIEnv.CallBooleanMethod (Handle, id_contains, new JValue (lref)));
		}

		public void CopyTo (T[] array, int array_index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			if (array.Length < array_index + Count)
				throw new ArgumentException ("array");

			if (id_toArray == IntPtr.Zero)
				id_toArray = JNIEnv.GetMethodID (collection_class, "toArray", "()[Ljava/lang/Object;");

			IntPtr lrefArray = JNIEnv.CallObjectMethod (Handle, id_toArray);
			for (int i = 0; i < Count; i++)
				array [array_index + i] = JavaConvert.FromJniHandle<T>(
						JNIEnv.GetObjectArrayElement (lrefArray, i),
						JniHandleOwnership.TransferLocalRef);
			JNIEnv.DeleteLocalRef (lrefArray);
		}

		public bool Remove (T item)
		{
			if (id_remove == IntPtr.Zero)
				id_remove = JNIEnv.GetMethodID (collection_class, "remove", "(I)Ljava/lang/Object;");
			return JavaConvert.WithLocalJniHandle (item,
					lref => JNIEnv.CallBooleanMethod (Handle, id_remove, new JValue (lref)));
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			return System.Linq.Extensions.ToEnumerator_Dispose<T> (Iterator());
		}
		
		[Preserve (Conditional=true)]
		public static ICollection<T> FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			IJavaObject inst = Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaCollection<T> (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (ICollection<T>) inst;
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (ICollection<T> items)
		{
			if (items == null)
				return IntPtr.Zero;

			var c = items as JavaCollection<T>;
			if (c != null)
				return JNIEnv.ToLocalJniHandle (c);

			using (c = new JavaCollection<T>(items))
				return JNIEnv.ToLocalJniHandle (c);
		}
	}
}
