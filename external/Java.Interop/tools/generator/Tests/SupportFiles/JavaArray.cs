using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


namespace Android.Runtime {

	[Register ("mono/android/runtime/JavaArray", DoNotGenerateAcw=true)]
	public sealed class JavaArray<T> : Java.Lang.Object, IList<T> {

		public JavaArray (IntPtr handle, JniHandleOwnership transfer)
		{
		}

		public int Count {
			get { throw new NotImplementedException (); }
			set { throw new NotImplementedException (); }
		}


		public bool IsReadOnly {
			get { throw new NotImplementedException (); }
			set { throw new NotImplementedException (); }
		}

		public T this [int index] {
			get { throw new NotImplementedException (); }
			set { throw new NotImplementedException (); }
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

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public int IndexOf (T item)
		{
			throw new NotImplementedException ();
		}

		public void Insert (int index, T item)
		{
			throw new NotImplementedException ();
		}

		public bool Remove (T item)
		{
			throw new NotImplementedException ();
		}

		public void RemoveAt (int index)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static JavaArray<T> FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (IList<T> value)
		{
			throw new NotImplementedException ();
		}
	}

	public class JavaList : Java.Lang.Object, IList {

		public JavaList ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
		}

		public JavaList (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		public JavaList (IEnumerable items) : this ()
		{
		}

		public int Count {
			get {
				throw new NotImplementedException ();
			}
		}

		public bool IsFixedSize {
			get { throw new NotImplementedException (); }
		}

		public bool IsReadOnly {
			get { throw new NotImplementedException (); }
		}

		public bool IsSynchronized {
			get { throw new NotImplementedException (); }
		}

		public object SyncRoot {
			get { throw new NotImplementedException (); }
		}

		public object this [int index] {
			get {
				throw new NotImplementedException ();
			}
			set { throw new NotImplementedException (); }
		}

		public int Add (object item)
		{
			throw new NotImplementedException ();
		}

		public void Clear ()
		{
			throw new NotImplementedException ();
		}

		public bool Contains (object item)
		{
			throw new NotImplementedException ();
		}

		public void CopyTo (Array array, int array_index)
		{
			throw new NotImplementedException ();
		}

		public IEnumerator GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public virtual int IndexOf (object item)
		{
			throw new NotImplementedException ();
		}

		public virtual int LastIndexOf (object item)
		{
			throw new NotImplementedException ();
		}

		public void Insert (int index, object item)
		{
			throw new NotImplementedException ();
		}

		public void Remove (object item)
		{
			throw new NotImplementedException ();
		}

		public void RemoveAt (int index)
		{
			throw new NotImplementedException ();
		}

		public virtual Java.Lang.Object Set (int location, Java.Lang.Object item)
		{
			throw new NotImplementedException ();
		}

		public virtual JavaList SubList (int start, int end)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IList FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (IList items)
		{
			throw new NotImplementedException ();
		}

		#region ArrayList "implementation"
		//
		// They provide "implementation" of java.util.ArrayList methods.
		// They could be "overriden" in derived classes if there is
		// some class that derives from ArrayList (since some methods
		// are abstract, it is mandatory to implement them.
		//
		// Here's the scenario that happens to any class that derives
		// from ArrayList and overrides add() method:
		//
		// - jar2xml excludes the add() override from the result api xml.
		//   In general, jar2xml excludes (AOSP excluded) any overrides
		//   that are defined enough in the base class. ArrayList has
		//   add() method, so the derived class does not need it.
		// - but in reality, it is not java.util.ArrayList but our
		//   JavaList which actually takes part of the base class.
		// - Since there is no API definition for add() because of above,
		//   they don't appear in the generated class.
		// - but after that, the generator tries to *implement* missing
		//   methods from its implementing interfaces, which includes
		//   java.util.List. And that interface contains add() method.
		//
		// Java.Util.IList does not exist, so we cannot implement explicitly.
		//	
		public virtual bool Add (Java.Lang.Object item)
		{
			throw new NotImplementedException ();
		}

		public virtual bool Add (int index, Java.Lang.Object item)
		{
			throw new NotImplementedException ();
		}

		public virtual bool Add (JavaList collection)
		{
			throw new NotImplementedException ();
		}

		public virtual bool AddAll (int location, JavaList collection)
		{
			throw new NotImplementedException ();
		}

		// Clear() exists.

		public virtual bool Contains (Java.Lang.Object item)
		{
			throw new NotImplementedException ();
		}

		public virtual bool ContainsAll (JavaList collection)
		{
			throw new NotImplementedException ();
		}

		public virtual bool Equals (Java.Lang.Object obj)
		{
			throw new NotImplementedException ();
		}

		public virtual Java.Lang.Object Get (int location)
		{
			throw new NotImplementedException ();
		}

		public virtual int IndexOf (Java.Lang.Object item)
		{
			throw new NotImplementedException ();
		}

		public virtual bool IsEmpty {
			get { throw new NotImplementedException (); }
		}

		public virtual Java.Lang.Object Remove (int location)
		{
			throw new NotImplementedException ();
		}

		public virtual bool Remove (Java.Lang.Object item)
		{
			throw new NotImplementedException ();
		}

		public virtual bool RemoveAll (JavaList collection)
		{
			throw new NotImplementedException ();
		}

		public virtual bool RetainAll (JavaList collection)
		{
			throw new NotImplementedException ();
		}

		// Set() exists (added above, for code style consistency).

		public virtual int Size ()
		{
			throw new NotImplementedException ();
		}

		// SubList() exists (added above, for code style consistency).

		public virtual Java.Lang.Object [] ToArray (Java.Lang.Object [] array)
		{
			throw new NotImplementedException ();
		}

		public virtual Java.Lang.Object [] ToArray ()
		{
			throw new NotImplementedException ();
		}
		#endregion
	}

	public sealed class JavaList<T> : Java.Lang.Object, IList<T> {

		public JavaList (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public int Count {
			get { throw new NotImplementedException (); }
		}
			
		public bool IsReadOnly {
			get { throw new NotImplementedException (); }
		}

		public T this [int index] {
			get {
				throw new NotImplementedException ();
			}
			set { throw new NotImplementedException (); }
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

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public IEnumerator<T> GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public int IndexOf (T item)
		{
			throw new NotImplementedException ();
		}

		public void Insert (int index, T item)
		{
			throw new NotImplementedException ();
		}

		public bool Remove (T item)
		{
			throw new NotImplementedException ();
		}

		public void RemoveAt (int index)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static JavaArray<T> FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (IList<T> value)
		{
			throw new NotImplementedException ();
		}
	}
}
