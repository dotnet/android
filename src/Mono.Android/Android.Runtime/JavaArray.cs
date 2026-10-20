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

		/// <safety>
		/// A nonzero <paramref name="handle"/> must be a live reference from this VM compatible with an array compatible with T.
		/// Local references must belong to this thread; retain the input until construction returns or throws.
		/// <paramref name="transfer"/> must match ownership the caller holds. Exclude concurrent use/release
		/// through aliases to a transferred handle; construction can consume it before a later failure.
		/// Zero is the existing empty-peer initialization path, not a usable Java instance.
		/// Caller-unsafe enforcement on raw peer constructors is deferred; existing initialization APIs are unchanged.
		/// </safety>
		public JavaArray (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public int Count {
			get {
				unsafe {
					// SAFETY: this managed array peer retains its JNI array, element conversion uses the matching T and checked indices, and marshaling completes synchronously.
					return JNIEnv.GetArrayLength (Handle);
				}
			}
		}

		public bool IsReadOnly {
			get { return false; }
		}

		public T this [int index] {
			get {
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");
				unsafe {
					// SAFETY: this peer retains the T-compatible array and index was checked above.
					return JNIEnv.GetArrayItem<T> (Handle, index);
				}
			}
			set {
				unsafe {
					// SAFETY: this managed array peer retains its JNI array, element conversion uses the matching T and checked indices, and marshaling completes synchronously.
					JNIEnv.SetArrayItem<T> (Handle, index, value);
				}
			}
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
			unsafe {
				// SAFETY: this peer retains the JNI array through conversion and the immediate bounded managed copy.
				var items = JNIEnv.GetArray<T> (Handle)!;
				for (int i = 0; i < Count; i++)
					array [array_index + i] = items [i];
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			T[]? items;
			unsafe {
				// SAFETY: this array peer owns the live JNI array; marshaling completes before iterator suspension.
				items = JNIEnv.GetArray<T> (Handle);
			}
                        for (int i = 0; i < items!.Length; i++)
                                yield return items [i];
		}

		public int IndexOf (T item)
		{
			unsafe {
				// SAFETY: this peer retains the JNI array during the synchronous T conversion.
				var items = JNIEnv.GetArray<T> (Handle)!;
				return Array.IndexOf (items, item);
			}
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

		/// <safety>
		/// <paramref name="handle"/> must be null or a live reference to the matching Java collection/array
		/// from this VM, usable on this thread until conversion returns or throws. Generic element types
		/// must match the array/collection conversion; a cached peer does not validate an arbitrary handle.
		/// <paramref name="transfer"/> must describe ownership the caller holds. Do not race reference
		/// release or reuse aliases after ownership is consumed, including when conversion later fails.
		/// </safety>
		public static unsafe JavaArray<T>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
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
