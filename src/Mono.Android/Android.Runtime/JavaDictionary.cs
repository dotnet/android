using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/HashMap", DoNotGenerateAcw=true)]
	// java.util.HashMap allows null keys and values
	public class JavaDictionary : Java.Lang.Object, System.Collections.IDictionary {

		internal const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		class DictionaryEnumerator : IDictionaryEnumerator {

			IEnumerator simple_enumerator;

			public DictionaryEnumerator (JavaDictionary owner)
			{
				simple_enumerator = (owner as IEnumerable).GetEnumerator ();
			}

			public object? Current {
				get { return simple_enumerator.Current; }
			}
				
			public DictionaryEntry Entry {
				get { return (DictionaryEntry) Current!; }
			}

			public object Key {
				get { return Entry.Key; }
			}

			public object? Value {
				get { return Entry.Value; }
			}

			public bool MoveNext ()
			{
				return simple_enumerator.MoveNext ();
			}

			public void Reset ()
			{
				simple_enumerator.Reset ();
			}
		}


		internal static IntPtr map_class = JNIEnv.FindClass ("java/util/Map");
		static IntPtr id_clear;
		internal static IntPtr id_containsKey;
		internal static IntPtr id_get;
		static IntPtr id_keySet;
		internal static IntPtr id_put;
		internal static IntPtr id_remove;
		static IntPtr id_size;
		static IntPtr id_values;

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.get(Object, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map#get(java.lang.Object)
		//
		internal object? Get (object key)
		{
			if (id_get == IntPtr.Zero)
				id_get = JNIEnv.GetMethodID (map_class, "get", "(Ljava/lang/Object;)Ljava/lang/Object;");
			return JavaConvert.FromJniHandle (
				JavaConvert.WithLocalJniHandle (key, lref => {
					try {
						return JNIEnv.CallObjectMethod (Handle, id_get, new JValue (lref));
					} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new InvalidCastException (ex.Message, ex);
					} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
						throw new NullReferenceException (ex.Message, ex);
					}
				}), JniHandleOwnership.TransferLocalRef);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Map.keySet()` is not documented to throw any exceptions.
		//
		internal IntPtr GetKeys ()
		{
			if (id_keySet == IntPtr.Zero)
				id_keySet = JNIEnv.GetMethodID (map_class, "keySet", "()Ljava/util/Set;");
			return JNIEnv.CallObjectMethod (Handle, id_keySet);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Map.values()` is not documented to throw any exceptions.
		//
		internal IntPtr GetValues ()
		{
			if (id_values == IntPtr.Zero)
				id_values = JNIEnv.GetMethodID (map_class, "values", "()Ljava/util/Collection;");
			return JNIEnv.CallObjectMethod (Handle, id_values);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Map.put(K, V)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map#put(K,%20V)
		//
		internal void Put (object key, object? value)
		{
			if (id_put == IntPtr.Zero)
				id_put = JNIEnv.GetMethodID (map_class, "put", "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");
			IntPtr r = JavaConvert.WithLocalJniHandle (key,
					lrefKey => JavaConvert.WithLocalJniHandle (value,
						lrefValue => {
							try {
								return JNIEnv.CallObjectMethod (Handle, id_put, new JValue (lrefKey), new JValue (lrefValue));
							} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new NotSupportedException (ex.Message, ex);
							} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new InvalidCastException (ex.Message, ex);
							} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new NullReferenceException (ex.Message, ex);
							} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new ArgumentException (ex.Message, ex);
							}
						}
					)
			);
			JNIEnv.DeleteLocalRef (r);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.HasMap.ctor()` is not documented to throw any exceptions. The `else` clause below
		//    instantiates a type we don't know at this time, so we have no information about the exceptions
		//    it may throw.
		//
		[Register (".ctor", "()V", "")]
		public JavaDictionary ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (JavaDictionary)) {
				SetHandle (
						JNIEnv.StartCreateInstance ("java/util/HashMap", "()V"),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "()V");
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
				JNIEnv.FinishCreateInstance (Handle, "()V");
			}
		}

		public JavaDictionary (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaDictionary (IDictionary items) : this ()
		{
			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

#pragma warning disable CS8605 // Unboxing a possibly null value.
			foreach (DictionaryEntry item in items)
#pragma warning restore CS8605 // Unboxing a possibly null value.
				Add (item.Key, item.Value);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.Map.size()` is not documented to throw any exceptions.
		//
		public int Count {
			get {
				if (id_size == IntPtr.Zero)
					id_size = JNIEnv.GetMethodID (map_class, "size", "()I");
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

		public ICollection Keys {
			get { return new JavaSet (GetKeys (), JniHandleOwnership.TransferLocalRef); }
		}

		public object? SyncRoot {
			get { return null; }
		}

		public ICollection Values {
			get { return new JavaCollection (GetValues (), JniHandleOwnership.TransferLocalRef); }
		}

		public object? this [object key] {
			get { return Get (key); }
			set { Put (key, value); }
		}

		public void Add (object key, object? value)
		{
			if (Contains (key))
				throw new ArgumentException ("The key '" + key + "' already exists.", "key");
			Put (key, value);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Map.clear()` throws an exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map.html?hl=en#clear()
		//
		public void Clear ()
		{
			if (id_clear == IntPtr.Zero)
				id_clear = JNIEnv.GetMethodID (map_class, "clear", "()V");
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
		//    `java.util.Map.containsKey(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map.html?hl=en#containsKey(java.lang.Object)
		//
		public bool Contains (object key)
		{
			if (id_containsKey == IntPtr.Zero)
				id_containsKey = JNIEnv.GetMethodID (map_class, "containsKey", "(Ljava/lang/Object;)Z");
			return JavaConvert.WithLocalJniHandle (key, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_containsKey, new JValue (lref));
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
			else if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			else if (array.Length < array_index + Count)
				throw new ArgumentException ("array");
			int i = 0;
			foreach (var o in this)
				array.SetValue (o, array_index + i++);
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			foreach (var key in Keys)
				yield return new DictionaryEntry (key!, this [key!]);
		}

		public IDictionaryEnumerator GetEnumerator ()
		{
			return new DictionaryEnumerator (this);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Map.remove(Object, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map.html?hl=en#remove(java.lang.Object,%20java.lang.Object)
		//
		public void Remove (object key)
		{
			if (id_remove == IntPtr.Zero)
				id_remove = JNIEnv.GetMethodID (map_class, "remove", "(Ljava/lang/Object;)Ljava/lang/Object;");
			IntPtr r = JavaConvert.WithLocalJniHandle (key, lref => {
				try {
					return JNIEnv.CallObjectMethod (Handle, id_remove, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});
			JNIEnv.DeleteLocalRef (r);
		}
		
		public static IDictionary? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaDictionary (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (IDictionary) inst;
		}

		public static IntPtr ToLocalJniHandle (IDictionary? dictionary)
		{
			if (dictionary == null)
				return IntPtr.Zero;

			var d = dictionary as JavaDictionary;
			if (d != null)
				return JNIEnv.ToLocalJniHandle (d);

			using (d = new JavaDictionary (dictionary))
				return JNIEnv.ToLocalJniHandle (d);
		}
	}

	//
	// Exception audit:
	//
	//  Verdict
	//    No need to wrap thrown exceptions in a BCL class
	//
	//  Rationale
	//    `java.util.HasMap.ctor()` is not documented to throw any exceptions. The `else` clause below
	//    instantiates a type we don't know at this time, so we have no information about the exceptions
	//    it may throw.
	//
	// Preserve FromJniHandle
	[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
	[Register ("java/util/HashMap", DoNotGenerateAcw=true)]
	public class JavaDictionary<
			[DynamicallyAccessedMembers (Constructors)]
			K,
			[DynamicallyAccessedMembers (Constructors)]
			V
	> : JavaDictionary, IDictionary<K, V> {

		[Register (".ctor", "()V", "")]
		public JavaDictionary ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			if (GetType () == typeof (JavaDictionary<K, V>)) {
				SetHandle (
						JNIEnv.StartCreateInstance ("java/util/HashMap", "()V"),
						JniHandleOwnership.TransferLocalRef);
			} else {
				SetHandle (
						JNIEnv.StartCreateInstance (GetType (), "()V"),
						JniHandleOwnership.TransferLocalRef);
			}
			JNIEnv.FinishCreateInstance (Handle, "()V");
		}

		public JavaDictionary (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaDictionary (IDictionary<K, V> items) : this ()
		{
			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

			foreach (K key in items.Keys)
				Add (key, items [key]);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Collection.get(Object, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map#get(java.lang.Object)
		//
		internal V? Get (K key)
		{
			if (id_get == IntPtr.Zero)
				id_get = JNIEnv.GetMethodID (map_class, "get", "(Ljava/lang/Object;)Ljava/lang/Object;");
			var v = JavaConvert.WithLocalJniHandle (key, lref => {
				try {
					return JNIEnv.CallObjectMethod (Handle, id_get, new JValue (lref));
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});

			return JavaConvert.FromJniHandle<V>(v, JniHandleOwnership.TransferLocalRef);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Map.put(K, V)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map#put(K,%20V)
		//
		internal void Put (K key, V value)
		{
			if (id_put == IntPtr.Zero)
				id_put = JNIEnv.GetMethodID (map_class, "put", "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");
			IntPtr r = JavaConvert.WithLocalJniHandle (key,
					lrefKey => JavaConvert.WithLocalJniHandle (value,
						lrefValue => {
							try {
								return JNIEnv.CallObjectMethod (Handle, id_put, new JValue (lrefKey), new JValue (lrefValue));
							} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new NotSupportedException (ex.Message, ex);
							} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new InvalidCastException (ex.Message, ex);
							} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new NullReferenceException (ex.Message, ex);
							} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
								throw new ArgumentException (ex.Message, ex);
							}
						}
					)
			);
			JNIEnv.DeleteLocalRef (r);
		}

		// C#'s IDictionary is documented as allowing implementations to determine if null is supported or not,
		// but is not annotated as MaybeNull.  Our implementation allows null.
		[MaybeNull]
		public V this [K key] {
			[return: MaybeNull]
#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member because of nullability attributes.
			get {
#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member because of nullability attributes.
				if (!Contains (key!))
					throw new KeyNotFoundException ();
				return Get (key);
			}
			set {
				Put (key, value);
			}
		}

		public ICollection<K> Keys {
			get { return new JavaSet<K> (GetKeys (), JniHandleOwnership.TransferLocalRef); }
		}

		public ICollection<V> Values {
			get { return new JavaCollection<V> (GetValues (), JniHandleOwnership.TransferLocalRef); }
		}

		public void Add (KeyValuePair<K, V> item)
		{
			Add (item.Key, item.Value);
		}

		public void Add (K key, V value)
		{
			if (ContainsKey (key))
				throw new ArgumentException ("The key '" + key + "' already exists.", "key");
			Put (key, value);
		}

		public bool Contains (KeyValuePair<K, V> item)
		{
			return ContainsKey (item.Key);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Map.containsKey(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map.html?hl=en#containsKey(java.lang.Object)
		//
		public bool ContainsKey (K key)
		{
			if (id_containsKey == IntPtr.Zero)
				id_containsKey = JNIEnv.GetMethodID (map_class, "containsKey", "(Ljava/lang/Object;)Z");

			return JavaConvert.WithLocalJniHandle (key, lref => {
				try {
					return JNIEnv.CallBooleanMethod (Handle, id_containsKey, new JValue (lref));
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});
		}

		public void CopyTo (KeyValuePair<K, V>[] array, int array_index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			else if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			else if (array.Length < array_index + Count)
				throw new ArgumentException ("array");
			int i = 0;
			foreach (KeyValuePair<K, V> pair in this)
				array [array_index + i++] = pair;
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		public IEnumerator<KeyValuePair<K, V>> GetEnumerator ()
		{
			foreach (K key in Keys)
#pragma warning disable CS8604 // Possible null reference argument.
				yield return new KeyValuePair<K, V> (key, this [key]);
#pragma warning restore CS8604 // Possible null reference argument.
		}

		public bool Remove (KeyValuePair<K, V> pair)
		{
			return Remove (pair.Key);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.Map.remove(Object, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/Map.html?hl=en#remove(java.lang.Object,%20java.lang.Object)
		//
		public bool Remove (K key)
		{
			bool contains = ContainsKey (key);
			if (id_remove == IntPtr.Zero)
				id_remove = JNIEnv.GetMethodID (map_class, "remove", "(Ljava/lang/Object;)Ljava/lang/Object;");
			IntPtr r = JavaConvert.WithLocalJniHandle (key, lref => {
				try {
					return JNIEnv.CallObjectMethod (Handle, id_remove, new JValue (lref));
				} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NotSupportedException (ex.Message, ex);
				} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new InvalidCastException (ex.Message, ex);
				} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
					throw new NullReferenceException (ex.Message, ex);
				}
			});
			JNIEnv.DeleteLocalRef (r);
			return contains;
		}

		public bool TryGetValue (K key, out V value)
		{
#pragma warning disable CS8601 // Possible null reference assignment.
			value = Get (key);
#pragma warning restore CS8601 // Possible null reference assignment.
			return ContainsKey (key);
		}
		
		public static IDictionary<K, V>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaDictionary<K, V> (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (IDictionary<K, V>) inst;
		}

		public static IntPtr ToLocalJniHandle (IDictionary<K, V>? dictionary)
		{
			if (dictionary == null)
				return IntPtr.Zero;

			var d = dictionary as JavaDictionary<K, V>;
			if (d != null)
				return JNIEnv.ToLocalJniHandle (d);

			using (d = new JavaDictionary<K, V>(dictionary))
				return JNIEnv.ToLocalJniHandle (d);
		}
	}
}
