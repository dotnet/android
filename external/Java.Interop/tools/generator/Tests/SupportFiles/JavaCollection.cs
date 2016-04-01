using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Android.Runtime;

using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/Collection", DoNotGenerateAcw=true)]
	public class JavaCollection : Java.Lang.Object, System.Collections.ICollection {

		public JavaCollection (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public int Count {
			get {
				throw new NotImplementedException ();
			}
		}

		public bool IsSynchronized {
			get { throw new NotImplementedException (); }
		}

		public object SyncRoot {
			get { throw new NotImplementedException (); }
		}

		public void CopyTo (Array array, int array_index)
		{
			throw new NotImplementedException ();
		}

		public IEnumerator GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static ICollection FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (ICollection items)
		{
			throw new NotImplementedException ();
		}
	}

	[Register ("java/util/Collection", DoNotGenerateAcw=true)]
	public sealed class JavaCollection<T> : JavaCollection, ICollection<T> {

		public JavaCollection (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public bool IsReadOnly {
			get { throw new NotImplementedException (); }
		}

		public void Add (T item)
		{
			throw new NotImplementedException ();
		}

		public void Clear ()
		{
			throw new NotImplementedException ();
		}

		public bool Contains (T item)
		{
			throw new NotImplementedException ();
		}

		public void CopyTo (T[] array, int array_index)
		{
			throw new NotImplementedException ();
		}

		public bool Remove (T item)
		{
			throw new NotImplementedException ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public new IEnumerator<T> GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static new ICollection<T> FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (ICollection<T> items)
		{
			throw new NotImplementedException ();
		}
	}
}

