using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Android.Runtime {

	[Register ("mono/android/runtime/JavaArray", DoNotGenerateAcw=true)]
	public sealed class JavaArray<
			[DynamicallyAccessedMembers (Constructors)]
			T
	> : Java.Lang.Object, IList<T> {

		public JavaArray (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public int Count {
			get { return JNIEnv.GetArrayLength (Handle); }
		}

		public bool IsReadOnly {
			get { return false; }
		}

		public T this [int index] {
			get {
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");
				return JNIEnv.GetArrayItem<T> (Handle, index);
			}
			set { JNIEnv.SetArrayItem<T> (Handle, index, value); }
		}

		public void Add (T item)
		{
			throw new InvalidOperationException ();
		}

		public void Clear ()
		{
			throw new InvalidOperationException ();
		}

		public bool Contains (T item)
		{
			return IndexOf (item) >= 0;
		}

		public void CopyTo (T[] array, int array_index)
		{
			var items = JNIEnv.GetArray<T> (Handle)!;
			for (int i = 0; i < Count; i++)
				array [array_index + i] = items [i];
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			var items = JNIEnv.GetArray<T> (Handle);
                        for (int i = 0; i < items!.Length; i++)
                                yield return items [i];
		}

		public int IndexOf (T item)
		{
			var items = JNIEnv.GetArray<T> (Handle)!;
			return Array.IndexOf (items, item);
		}

		public void Insert (int index, T item)
		{
			throw new InvalidOperationException ();
		}

		public bool Remove (T item)
		{
			throw new InvalidOperationException ();
		}

		public void RemoveAt (int index)
		{
			throw new InvalidOperationException ();
		}

		public static JavaArray<T>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var existing = Java.Lang.Object.PeekObject (handle) as JavaArray<T>;
			if (existing != null) {
				JNIEnv.DeleteRef (handle, transfer);
				return existing;
			}
			return new JavaArray<T>(handle, transfer);
		}

		public static IntPtr ToLocalJniHandle (IList<T>? value)
		{
			if (value == null)
				return IntPtr.Zero;

			var c = value as JavaArray<T>;
			if (c != null)
				return JNIEnv.ToLocalJniHandle (c);

			var a = new T [value.Count];
			value.CopyTo (a, 0);

			return  JNIEnv.NewArray (a, typeof (T));
		}
	}
}
