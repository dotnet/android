using System;
using System.Collections;
using System.Collections.Generic;

using Android.Runtime;

using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/HashMap", DoNotGenerateAcw=true)]
	public class JavaDictionary : Java.Lang.Object, System.Collections.IDictionary {

		class DictionaryEnumerator : IDictionaryEnumerator {

			public DictionaryEnumerator (JavaDictionary owner)
			{
				throw new NotImplementedException ();
			}

			public object Current {
				get { throw new NotImplementedException (); }
			}

			public DictionaryEntry Entry {
				get { throw new NotImplementedException (); }
			}

			public object Key {
				get { throw new NotImplementedException (); }
			}

			public object Value {
				get { throw new NotImplementedException (); }
			}

			public bool MoveNext ()
			{
				throw new NotImplementedException ();
			}

			public void Reset ()
			{
				throw new NotImplementedException ();
			}
		}


		internal object Get (object key)
		{
			throw new NotImplementedException ();
		}

		internal IntPtr GetKeys ()
		{
			throw new NotImplementedException ();
		}

		internal IntPtr GetValues ()
		{
			throw new NotImplementedException ();
		}

		internal void Put (object key, object value)
		{
			throw new NotImplementedException ();
		}

		public JavaDictionary ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			throw new NotImplementedException ();
		}

		public JavaDictionary (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaDictionary (IDictionary items) : this ()
		{
			throw new NotImplementedException ();
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

		public ICollection Keys {
			get { throw new NotImplementedException (); }
		}

		public object SyncRoot {
			get { throw new NotImplementedException (); }
		}

		public ICollection Values {
			get { throw new NotImplementedException (); }
		}

		public object this [object key] {
			get { throw new NotImplementedException (); }
			set { throw new NotImplementedException (); }
		}

		public void Add (object key, object value)
		{
			throw new NotImplementedException ();
		}

		public void Clear ()
		{
			throw new NotImplementedException ();
		}

		public bool Contains (object key)
		{
			throw new NotImplementedException ();
		}

		public void CopyTo (Array array, int array_index)
		{
			throw new NotImplementedException ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public IDictionaryEnumerator GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public void Remove (object key)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IDictionary FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (IDictionary dictionary)
		{
			throw new NotImplementedException ();
		}
	}

	[Register ("java/util/HashMap", DoNotGenerateAcw=true)]
	public class JavaDictionary<K, V> : JavaDictionary, IDictionary<K, V> {

		[Register (".ctor", "()V", "")]
		public JavaDictionary ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			throw new NotImplementedException ();
		}

		public JavaDictionary (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaDictionary (IDictionary<K, V> items) : this ()
		{
			throw new NotImplementedException ();
		}

		internal V Get (K key)
		{
			throw new NotImplementedException ();
		}

		internal void Put (K key, V value)
		{
			throw new NotImplementedException ();
		}

		public V this [K key] {
			get {
				throw new NotImplementedException ();
			}
			set {
				throw new NotImplementedException ();
			}
		}

		public new ICollection<K> Keys {
			get { throw new NotImplementedException (); }
		}

		public new ICollection<V> Values {
			get { throw new NotImplementedException (); }
		}

		public void Add (KeyValuePair<K, V> item)
		{
			throw new NotImplementedException ();
		}

		public void Add (K key, V value)
		{
			throw new NotImplementedException ();
		}

		public bool Contains (KeyValuePair<K, V> item)
		{
			throw new NotImplementedException ();
		}

		public bool ContainsKey (K key)
		{
			throw new NotImplementedException ();
		}

		public void CopyTo (KeyValuePair<K, V>[] array, int array_index)
		{
			throw new NotImplementedException ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public new IEnumerator<KeyValuePair<K, V>> GetEnumerator ()
		{
			throw new NotImplementedException ();
		}

		public bool Remove (KeyValuePair<K, V> pair)
		{
			throw new NotImplementedException ();
		}

		public bool Remove (K key)
		{
			throw new NotImplementedException ();
		}

		public bool TryGetValue (K key, out V value)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static new IDictionary<K, V> FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		[Preserve (Conditional=true)]
		public static IntPtr ToLocalJniHandle (IDictionary<K, V> dictionary)
		{
			throw new NotImplementedException ();
		}
	}
}

