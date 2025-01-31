using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Android.Runtime;

using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/Collection", DoNotGenerateAcw=true)]
	// java.util.Collection allows null values
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

			foreach (var item in items)
				Add (item!);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.add(E)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Collection?hl=en#add(E)
		//
		internal void Add (object item)
		{
			if (id_add == IntPtr.Zero)
				id_add = JNIEnv.GetMethodID (collection_class, "add", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {

					try {
						JNIEnv.CallBooleanMethod (Handle, id_add, new JValue (lref));
					} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new NotSupportedException (ex.Message, ex);
					} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new InvalidCastException (ex.Message, ex);
					} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new NullReferenceException (ex.Message, ex);
					} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new ArgumentException (ex.Message, ex);
					} catch (Java.Lang.IllegalStateException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new InvalidOperationException (ex.Message, ex);
					}
					return IntPtr.Zero;
			});
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Collection.iterator()` is not documented to throw any exceptions.
		//
		internal Java.Util.IIterator Iterator ()
		{
			if (id_iterator == IntPtr.Zero)
				id_iterator = JNIEnv.GetMethodID (collection_class, "iterator", "()Ljava/util/Iterator;");
			return Java.Lang.Object.GetObject<Java.Util.IIterator> (
					JNIEnv.CallObjectMethod (Handle, id_iterator),
					JniHandleOwnership.TransferLocalRef)!;
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Collection.size()` is not documented to throw any exceptions.
		//
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

		public object? SyncRoot {
			get {return null;}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Collection.toArray()` is not documented to throw any exceptions.
		//
		internal Java.Lang.Object[] ToArray ()
		{
			if (id_toArray == IntPtr.Zero)
				id_toArray = JNIEnv.GetMethodID (collection_class, "toArray", "()[Ljava/lang/Object;");
			using (var o = new Java.Lang.Object (JNIEnv.CallObjectMethod (Handle, id_toArray),
					JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister))
				return ((Java.Lang.Object[]) o)!;
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Collection.toArray()` is not documented to throw any exceptions.
		//
		public void CopyTo (Array array, int array_index)
		{
			[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = "JavaCollection<T> constructors are preserved by the MarkJavaObjects trimmer step.")]
			[return: DynamicallyAccessedMembers (Constructors)]
			static Type GetElementType (Array array) =>
				array.GetType ().GetElementType ();

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
							GetElementType (array)),
						array_index + i);
			JNIEnv.DeleteLocalRef (lrefArray);
		}

		public IEnumerator GetEnumerator ()
		{
			return System.Linq.Extensions.ToEnumerator_Dispose (Iterator ());
		}

		public static ICollection? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaCollection (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (ICollection) inst;
		}

		public static IntPtr ToLocalJniHandle (ICollection? items)
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
	public sealed class JavaCollection<
			[DynamicallyAccessedMembers (Constructors)]
			T
	> : JavaCollection, ICollection<T> {

		public JavaCollection (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.ArrayList.ctor()` is not documented to throw any exceptions.
		//
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

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.add(E)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Collection?hl=en#add(E)
		//
		public void Add (T item)
		{
			if (id_add == IntPtr.Zero)
				id_add = JNIEnv.GetMethodID (collection_class, "add", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					JNIEnv.CallBooleanMethod (Handle, id_add, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new ArgumentException (ex.Message, ex);
				} catch (Java.Lang.IllegalStateException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidOperationException (ex.Message, ex);
				}
				return IntPtr.Zero;
			});
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.clear()` throws an  exception, see:
		//
		//     https://developer.android.com/reference/java/util/Collection?hl=en#clear()
		//
		public void Clear ()
		{
			if (id_clear == IntPtr.Zero)
				id_clear = JNIEnv.GetMethodID (collection_class, "clear", "()V");
			try {
				JNIEnv.CallVoidMethod (Handle, id_clear);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.contains(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Collection?hl=en#contains(java.lang.Object)
		//
		public bool Contains (T item)
		{
			if (id_contains == IntPtr.Zero)
				id_contains = JNIEnv.GetMethodID (collection_class, "contains", "(Ljava/lang/Object;)Z");
			return JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_contains, new JValue (lref));
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Collection.toArray()` is not documented to throw any exceptions.
		//
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
						JniHandleOwnership.TransferLocalRef)!;
			JNIEnv.DeleteLocalRef (lrefArray);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.remove(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Collection?hl=en#remove(java.lang.Object)
		//
		public bool Remove (T item)
		{
			if (id_remove == IntPtr.Zero)
				id_remove = JNIEnv.GetMethodID (collection_class, "remove", "(I)Ljava/lang/Object;");
			return JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_remove, new JValue (lref));
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				}
			});
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ()!;
		}

		public IEnumerator<T?> GetEnumerator ()
		{
			return System.Linq.Extensions.ToEnumerator_Dispose<T> (Iterator());
		}
		
		public static ICollection<T>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaCollection<T> (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (ICollection<T>) inst;
		}

		public static IntPtr ToLocalJniHandle (ICollection<T>? items)
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
