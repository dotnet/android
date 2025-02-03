using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/HashSet", DoNotGenerateAcw=true)]
	// java.util.HashSet allows null values
	public class JavaSet : Java.Lang.Object, ICollection {

		internal static IntPtr set_class = JNIEnv.FindClass ("java/util/Set");

		internal static IntPtr id_add;
		internal static IntPtr id_contains;
		internal static IntPtr id_remove;

		static IntPtr id_iterator;
		internal Java.Util.IIterator Iterator ()
		{
			if (id_iterator == IntPtr.Zero)
				id_iterator = JNIEnv.GetMethodID (set_class, "iterator", "()Ljava/util/Iterator;");
			return Java.Lang.Object.GetObject<Java.Util.IIterator> (
					JNIEnv.CallObjectMethod (Handle, id_iterator),
					JniHandleOwnership.TransferLocalRef)!;
		}

		internal static IntPtr id_ctor;

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.HashSet.ctor()` is not documented to throw any exceptions. The `else` clause below
		//    instantiates a type we don't know at this time, so we have no information about the exceptions
		//    it may throw.
		//
		[Register (".ctor", "()V", "")]
		public JavaSet ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (JavaSet)) {
				SetHandle (
						JNIEnv.StartCreateInstance ("java/util/HashSet", "()V"),
						JniHandleOwnership.TransferLocalRef);
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
			}
			JNIEnv.FinishCreateInstance (Handle, "()V");
		}

		public JavaSet (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaSet (IEnumerable items) : this ()
		{
			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

			foreach (object? item in items)
				Add (item);
		}

		static IntPtr id_size;

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Set.size()` is not documented to throw any exceptions.
		//
		public int Count {
			get {
				if (id_size == IntPtr.Zero)
					id_size = JNIEnv.GetMethodID (set_class, "size", "()I");
				return JNIEnv.CallIntMethod (Handle, id_size);
			}
		}

		public bool IsFixedSize {
			get { return false; }
		}

		public bool IsReadOnly {
			get { return false; }
		}

		public bool IsSynchronized {
			get { return false; }
		}

		public object? SyncRoot {
			get { return null; }
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Set.add(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#add(E)
		//
		public void Add (object? item)
		{
			if (id_add == IntPtr.Zero)
				id_add = JNIEnv.GetMethodID (set_class, "add", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_add, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new ArgumentException (ex.Message, ex);
				}
			});
		}

		static IntPtr id_clear;

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Set.clear()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#clear()
		//
		public void Clear ()
		{
			if (id_clear == IntPtr.Zero)
				id_clear = JNIEnv.GetMethodID (set_class, "clear", "()V");
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
		//    `java.util.Set.contains(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#contains(java.lang.Object)
		//
		public bool Contains (object? item)
		{
			if (id_contains == IntPtr.Zero)
				id_contains = JNIEnv.GetMethodID (set_class, "contains", "(Ljava/lang/Object;)Z");
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

		public void CopyTo (Array array, int array_index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			if (array.Length < array_index + Count)
				throw new ArgumentException ("array");

			int i = 0;
			foreach (object? item in this)
				array.SetValue (item, array_index + i++);
		}

		public IEnumerator GetEnumerator ()
		{
			return System.Linq.Extensions.ToEnumerator_Dispose (Iterator ());
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Set.remove(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#remove(java.lang.Object)
		//
		public void Remove (object? item)
		{
			if (id_remove == IntPtr.Zero)
				id_remove = JNIEnv.GetMethodID (set_class, "remove", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_remove, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});
		}

		public static ICollection? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaSet (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (ICollection) inst;
		}
		
		public static IntPtr ToLocalJniHandle (ICollection? items)
		{
			if (items == null)
				return IntPtr.Zero;

			var s = items as JavaSet;
			if (s != null)
				return JNIEnv.ToLocalJniHandle (s);

			using (s = new JavaSet (items))
				return JNIEnv.ToLocalJniHandle (s);
		}
	}

	[Register ("java/util/HashSet", DoNotGenerateAcw=true)]
	// java.util.HashSet allows null
	public class JavaSet<
			[DynamicallyAccessedMembers (Constructors)]
			T
	> : JavaSet, ICollection<T> {

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.HashSet.ctor()` is not documented to throw any exceptions. The `else` clause below
		//    instantiates a type we don't know at this time, so we have no information about the exceptions
		//    it may throw.
		//
		[Register (".ctor", "()V", "")]
		public JavaSet () : base ()
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (JavaSet<T>)) {
				SetHandle (
						JNIEnv.StartCreateInstance ("java/util/HashSet", "()V"),
						JniHandleOwnership.TransferLocalRef);
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
			}
			JNIEnv.FinishCreateInstance (Handle, "()V");
		}

		public JavaSet (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaSet (IEnumerable<T> items) : this ()
		{
			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

			foreach (T item in items)
				Add (item);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Set.add(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#add(E)
		//
		public void Add (T item)
		{
			if (id_add == IntPtr.Zero)
				id_add = JNIEnv.GetMethodID (set_class, "add", "(Ljava/lang/Object;)Z");
			JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_add, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new ArgumentException (ex.Message, ex);
				}
			});
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Set.contains(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#contains(java.lang.Object)
		//
		public bool Contains (T item)
		{
			if (id_contains == IntPtr.Zero)
				id_contains = JNIEnv.GetMethodID (set_class, "contains", "(Ljava/lang/Object;)Z");
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

		public void CopyTo (T[] array, int array_index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			if (array.Length < array_index + Count)
				throw new ArgumentException ("array");

			int i = 0;
			foreach (T item in this)
				array [array_index + i++] = item;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ()!;
		}

		public IEnumerator<T> GetEnumerator ()
		{
			return System.Linq.Extensions.ToEnumerator_Dispose<T> (Iterator ());
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Set.remove(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Set?hl=en#remove(java.lang.Object)
		//
		public bool Remove (T item)
		{
			if (id_remove == IntPtr.Zero)
				id_remove = JNIEnv.GetMethodID (set_class, "remove", "(Ljava/lang/Object;)Z");
			return JavaConvert.WithLocalJniHandle (item, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_remove, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});
		}

		public static ICollection<T>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaSet<T> (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (ICollection<T>) inst;
		}

		public static IntPtr ToLocalJniHandle (ICollection<T>? items)
		{
			if (items == null)
				return IntPtr.Zero;

			var s = items as JavaSet<T>;
			if (s != null)
				return JNIEnv.ToLocalJniHandle (s);

			using (s = new JavaSet<T>(items))
				return JNIEnv.ToLocalJniHandle (s);
		}
	}
}
