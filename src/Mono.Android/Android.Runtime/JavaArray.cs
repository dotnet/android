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
				// SAFETY: GC.KeepAlive (this) retains the managed array peer across the synchronous JNI array length lookup.
				int count = unsafe(JNIEnv.GetArrayLength (Handle));
				GC.KeepAlive (this);
				return count;
			}
		}

		public bool IsReadOnly {
			get { return false; }
		}

		public T this [int index] {
			get {
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");
				// SAFETY: the constructor contract guarantees a T-compatible array and index was checked above;
				// GC.KeepAlive (this) retains this peer across the synchronous call.
				var item = unsafe(JNIEnv.GetArrayItem<T> (Handle, index));
				GC.KeepAlive (this);
				return item;
			}
			set {
				unsafe {
					// SAFETY: the constructor contract guarantees a T-compatible array and SetArrayItem<T> bounds-checks index;
					// GC.KeepAlive (this) retains this peer across the synchronous write.
					JNIEnv.SetArrayItem<T> (Handle, index, value);
					GC.KeepAlive (this);
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
			// SAFETY: the constructor contract guarantees a T-compatible array;
			// GC.KeepAlive (this) retains this peer across the synchronous JNI array conversion before iterator suspension.
			var items = unsafe(JNIEnv.GetArray<T> (Handle));
			GC.KeepAlive (this);
			for (int i = 0; i < items!.Length; i++)
				yield return items [i];
		}

		public int IndexOf (T item)
		{
			unsafe {
				// SAFETY: the constructor contract guarantees a T-compatible array;
				// GC.KeepAlive (this) retains this peer across the synchronous T array conversion.
				var items = JNIEnv.GetArray<T> (Handle)!;
				GC.KeepAlive (this);
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
		public static JavaArray<T>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var existing = Java.Lang.Object.PeekObject (handle) as JavaArray<T>;
			if (existing != null) {
				unsafe {
					// SAFETY: the caller's transfer contract covers this reference; the cached peer replaces it.
					JNIEnv.DeleteRef (handle, transfer);
				}
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
