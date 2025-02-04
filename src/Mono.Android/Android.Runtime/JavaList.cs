using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

namespace Android.Runtime {

	[Register ("java/util/ArrayList", DoNotGenerateAcw=true)]
	// java.util.ArrayList allows null values
	public partial class JavaList : Java.Lang.Object, System.Collections.IList {

		internal static readonly JniPeerMembers list_members = new XAPeerMembers ("java/util/List", typeof (JavaList), isInterface: true);

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.get(int)` throws an exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List.html?hl=en#get(int)
		//
		internal unsafe object? InternalGet (
				int location,
				[DynamicallyAccessedMembers (Constructors)]
				Type? targetType = null)
		{
			const string id = "get.(I)Ljava/lang/Object;";
			JniObjectReference obj;
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (location),
				};
				obj = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			}

			return JavaConvert.FromJniHandle (
					obj.Handle,
					JniHandleOwnership.TransferLocalRef,
					targetType);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.List.iterator()` is not documented to throw any exceptions.
		//
		public virtual unsafe Java.Util.IIterator Iterator ()
		{
			const string id = "iterator.()Ljava/util/Iterator;";
			JniObjectReference obj = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, null);
			return Java.Lang.Object.GetObject<Java.Util.IIterator> (
					obj.Handle,
					JniHandleOwnership.TransferLocalRef)!;
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.set(int, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List.html?hl=en#set(int,%20E)
		//
		internal unsafe void InternalSet (int location, object? value)
		{
			const string id = "set.(ILjava/lang/Object;)Ljava/lang/Object;";
			IntPtr lref = JavaConvert.ToLocalJniHandle (value);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [2] {
					new JniArgumentValue (location),
					new JniArgumentValue (lref),
				};
				var r = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
				JniObjectReference.Dispose (ref r);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (value);
			}
		}


		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.ArrayList.ctor()` is not documented to throw any exceptions. The `else` clause below
		//    instantiates a type we don't know at this time, so we have no information about the exceptions
		//    it may throw.
		//
		[Register (".ctor", "()V", "")]
		public unsafe JavaList ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			const string id = "()V";
			var methods = JniPeerMembers.InstanceMethods;
			var obj = methods.StartCreateInstance (id, GetType (), null);
			SetHandle (obj.Handle, JniHandleOwnership.TransferLocalRef);
			methods.FinishCreateInstance (id, this, null);
		}

		public JavaList (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

		public JavaList (IEnumerable items) : this ()
		{
			if (items == null) {
				Dispose ();
				throw new ArgumentNullException ("items");
			}

			foreach (var item in items)
				Add (item);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.List.size()` is not documented to throw any exceptions.
		//
		public unsafe int Count {
			get {
				const string id = "size.()I";
				return list_members.InstanceMethods.InvokeAbstractInt32Method (id, this, null);
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

		public object? this [int index] {
			get {
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");
				return InternalGet (index);
			}
			set { InternalSet (index, value); }
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.add(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#add(E)
		//
		public unsafe int Add (object? item)
		{
			const string id = "add.(Ljava/lang/Object;)Z";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				list_members.InstanceMethods.InvokeAbstractBooleanMethod (id, this, parameters);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
			return Count - 1;
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.clear()` throws an exception, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#clear()
		//
		public unsafe void Clear ()
		{
			const string id = "clear.()V";
			try {
				list_members.InstanceMethods.InvokeAbstractVoidMethod (id, this, null);
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
		//    `java.util.List.contains(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#contains(java.lang.Object)
		//
		public unsafe bool Contains (object? item)
		{
			const string id = "contains.(Ljava/lang/Object;)Z";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				return list_members.InstanceMethods.InvokeAbstractBooleanMethod (id, this, parameters);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		public void CopyTo (Array array, int array_index)
		{
			[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = "JavaList<T> constructors are preserved by the MarkJavaObjects trimmer step.")]
			[return: DynamicallyAccessedMembers (Constructors)]
			static Type GetElementType (Array array) =>
				array.GetType ().GetElementType ();

			if (array == null)
				throw new ArgumentNullException ("array");
			if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			if (array.Length < array_index + Count)
				throw new ArgumentException ("array");

			var targetType = GetElementType (array);
			int c = Count;
			for (int i = 0; i < c; i++)
				array.SetValue (InternalGet (i, targetType), array_index + i);
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
		//    `java.util.List.indexOf(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#indexOf(java.lang.Object)
		//
		public virtual unsafe int IndexOf (object? item)
		{
			const string id = "indexOf.(Ljava/lang/Object;)I";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				return list_members.InstanceMethods.InvokeAbstractInt32Method (id, this, parameters);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.lastIndexOf(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#lastIndexOf(java.lang.Object)
		//
		public virtual unsafe int LastIndexOf (object item)
		{
			const string id = "lastIndexOf.(Ljava/lang/Object;)I";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				return list_members.InstanceMethods.InvokeAbstractInt32Method (id, this, parameters);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.add(int, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#add(int,%20E)
		//
		public unsafe void Insert (int index, object? item)
		{
			const string id = "add.(ILjava/lang/Object;)V";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [2] {
					new JniArgumentValue (index),
					new JniArgumentValue (lref),
				};
				list_members.InstanceMethods.InvokeAbstractVoidMethod (id, this, parameters);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		public void Remove (object? item)
		{
			int i = IndexOf (item);
			if (i < 0 && i >= Count)
				return;
			RemoveAt (i);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.remove(int)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#remove(int)
		//
		public unsafe void RemoveAt (int index)
		{
			const string id = "remove.(I)Ljava/lang/Object;";
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (index),
				};
				JniObjectReference r = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
				JniObjectReference.Dispose (ref r);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			}
		}
		
		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.set(int, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#set(int,%20E)
		//
		public virtual unsafe Java.Lang.Object? Set (int location, Java.Lang.Object item)
		{
			const string id = "set.(ILjava/lang/Object;)Ljava/lang/Object;";
			JniObjectReference obj;
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [2] {
					new JniArgumentValue (location),
					new JniArgumentValue (item),
				};
				obj = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			}
			return Java.Lang.Object.GetObject<Java.Lang.Object> (
					obj.Handle,
					JniHandleOwnership.TransferLocalRef);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.subList(int, int)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#subList(int,%20int)
		//     https://developer.android.com/reference/java/util/ArrayList?hl=en#subList(int,%20int)
		//
		public virtual unsafe JavaList SubList (int start, int end)
		{
			const string id = "subList.(II)Ljava/util/List;";
			JniObjectReference obj;
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [2] {
					new JniArgumentValue (start),
					new JniArgumentValue (end),
				};
				obj = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			}
			return new JavaList (
					obj.Handle,
					JniHandleOwnership.TransferLocalRef);
		}
		
		public static IList? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle);
			if (inst == null)
				inst = new JavaList (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (IList) inst;
		}

		public static IntPtr ToLocalJniHandle (IList? items)
		{
			if (items == null)
				return IntPtr.Zero;

			var c = items as JavaList;
			if (c != null)
				return JNIEnv.ToLocalJniHandle (c);

			using (c = new JavaList (items))
				return JNIEnv.ToLocalJniHandle (c);
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
		public virtual bool Add (Java.Lang.Object? item)
		{
			return Add (0, item);
		}

		public virtual bool Add (int index, Java.Lang.Object? item)
		{
			Add ((object?) item);
			return true;
		}

		public virtual bool Add (JavaList collection)
		{
			return AddAll (0, collection);
		}
		
		public virtual bool AddAll (int location, JavaList collection)
		{
			int pos = location;
			bool ret = false;
			foreach (Java.Lang.Object? item in collection)
				ret |= Add (pos++, item);
			return ret;
		}
		
		// Clear() exists.
		
		public virtual bool Contains (Java.Lang.Object? item)
		{
			return Contains ((object?) item);
		}
		
		public virtual bool ContainsAll (JavaList collection)
		{
			foreach (Java.Lang.Object? item in collection)
				if (!Contains (item))
					return false;
			return true;
		}
		
		public virtual bool Equals (Java.Lang.Object obj)
		{
			var collection = obj as JavaList;
			if (collection == null || Count != collection.Count)
				return false;
			// I'm not sure if this should be valid (i.e. Count doesn't change), hopefully it can be premised...
			for (int i = 0; i < Count; i++)
				if (!(this [i]?.Equals (collection [i]) == true))
					return false;
			return true;
		}
		
		public virtual Java.Lang.Object? Get (int location)
		{
			return (Java.Lang.Object?) InternalGet (location);
		}

		public virtual int IndexOf (Java.Lang.Object? item)
		{
			return IndexOf ((object?) item);
		}

		public virtual bool IsEmpty {
			get { return Count == 0; }
		}
		
		// Iterate() exists.

		// LastIndexOf() exists (added above, for code style consistency).
		
		// ListIterator does not exist in MfA, so listIterator() methods cannot be implemented.
		
		public virtual Java.Lang.Object? Remove (int location)
		{
			var ret = Get (location);
			RemoveAt (location);
			return ret;
		}
		
		public virtual bool Remove (Java.Lang.Object? item)
		{
			int i = IndexOf (item);
			if (i < 0 && i >= Count)
				return false;
			RemoveAt (i);
			return true;
		}
		
		public virtual bool RemoveAll (JavaList collection)
		{
			bool ret = false;
			foreach (Java.Lang.Object? item in collection)
				ret |= Remove (item);
			return ret;
		}
		
		public virtual bool RetainAll (JavaList collection)
		{
			bool ret = false;
			for (int i = 0; i < Count; i++) {
				var item = Get (i);
				if (!collection.Contains (item!)) {
					Remove (item!);
					ret = true;
					i--;
				}
			}
			return ret;
		}
		
		// Set() exists (added above, for code style consistency).
		
		public virtual int Size ()
		{
			return Count;
		}
		
		// SubList() exists (added above, for code style consistency).
		
		public virtual Java.Lang.Object [] ToArray (Java.Lang.Object [] array)
		{
			if (array.Length < Count)
				array = new Java.Lang.Object [Count];
			CopyTo (array, 0);
			return array;
		}
		
		public virtual Java.Lang.Object [] ToArray ()
		{
			return ToArray (new Java.Lang.Object [0]);
		}
		#endregion
	}

	[Register ("java/util/ArrayList", DoNotGenerateAcw=true)]
	public class JavaList<
			[DynamicallyAccessedMembers (Constructors)]
			T
	> : JavaList, IList<T> {

		//
		// Exception audit:
		//
		//  Verdict
		//    No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//    `java.util.ArrayList.ctor()` is not documented to throw any exceptions. The `else` clause below
		//    instantiates a type we don't know at this time, so we have no information about the exceptions
		//    it may throw.
		//
		[Register (".ctor", "()V", "")]
		public unsafe JavaList ()
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			if (Handle != IntPtr.Zero)
				return;

			const string id = "()V";
			var methods = JniPeerMembers.InstanceMethods;
			var obj = methods.StartCreateInstance (id, GetType (), null);
			SetHandle (obj.Handle, JniHandleOwnership.TransferLocalRef);
			methods.FinishCreateInstance (id, this, null);
		}

		public JavaList (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public JavaList (IEnumerable<T> items) : this ()
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
		//    `java.util.List.get(int)` throws an exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List.html?hl=en#get(int)
		//
		internal unsafe T? InternalGet (int location)
		{
			const string id = "get.(I)Ljava/lang/Object;";
			JniObjectReference obj;
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (location),
				};
				obj = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			}

			return JavaConvert.FromJniHandle<T> (
					obj.Handle,
					JniHandleOwnership.TransferLocalRef);
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.set(int, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List.html?hl=en#set(int,%20E)
		//
		internal unsafe void InternalSet (int location, T value)
		{
			const string id = "set.(ILjava/lang/Object;)Ljava/lang/Object;";
			IntPtr lref = JavaConvert.ToLocalJniHandle (value);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [2] {
					new JniArgumentValue (location),
					new JniArgumentValue (lref),
				};
				var r = list_members.InstanceMethods.InvokeAbstractObjectMethod (id, this, parameters);
				JniObjectReference.Dispose (ref r);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (value);
			}
		}

		// C#'s IList<T> allows nulls but is not annotated as MaybeNull.
		[MaybeNull]
		public T this [int index] {
			[return: MaybeNull]
#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member because of nullability attributes.
			get {
#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member because of nullability attributes.
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");
				return InternalGet (index);
			}
			set { InternalSet (index, value); }
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.add(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#add(E)
		//
		public unsafe void Add (T item)
		{
			const string id = "add.(Ljava/lang/Object;)Z";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				list_members.InstanceMethods.InvokeAbstractBooleanMethod (id, this, parameters);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.contains(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#contains(java.lang.Object)
		//
		public unsafe bool Contains (T item)
		{
			const string id = "contains.(Ljava/lang/Object;)Z";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				return list_members.InstanceMethods.InvokeAbstractBooleanMethod (id, this, parameters);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		public void CopyTo (T[] array, int array_index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			if (array_index < 0)
				throw new ArgumentOutOfRangeException ("array_index");
			if (array.Length < array_index + Count)
				throw new ArgumentException ("array");

			for (int i = 0; i < Count; i++)
				array [array_index + i] = this [i]!;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ()!;
		}

		public IEnumerator<T?> GetEnumerator ()
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
		//    `java.util.List.indexOf(Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#indexOf(java.lang.Object)
		//
		public unsafe int IndexOf (T item)
		{
			const string id = "indexOf.(Ljava/lang/Object;)I";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [1] {
					new JniArgumentValue (lref),
				};
				return list_members.InstanceMethods.InvokeAbstractInt32Method (id, this, parameters);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		//
		// Exception audit:
		//
		//  Verdict
		//    Exception wrapping is required.
		//
		//  Rationale
		//    `java.util.List.add(int, Object)` throws a number of exceptions, see:
		//
		//     https://developer.android.com/reference/java/util/List?hl=en#add(int,%20E)
		//
		public unsafe void Insert (int index, T item)
		{
			const string id = "add.(ILjava/lang/Object;)V";
			IntPtr lref = JavaConvert.ToLocalJniHandle (item);
			try {
				JniArgumentValue* parameters = stackalloc JniArgumentValue [2] {
					new JniArgumentValue (index),
					new JniArgumentValue (lref),
				};
				list_members.InstanceMethods.InvokeAbstractVoidMethod (id, this, parameters);
			} catch (Java.Lang.UnsupportedOperationException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NotSupportedException (ex.Message, ex);
			} catch (Java.Lang.ClassCastException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new InvalidCastException (ex.Message, ex);
			} catch (Java.Lang.NullPointerException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new NullReferenceException (ex.Message, ex);
			} catch (Java.Lang.IllegalArgumentException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentException (ex.Message, ex);
			} catch (Java.Lang.IndexOutOfBoundsException ex) when (JNIEnv.ShouldWrapJavaException (ex)) {
				throw new ArgumentOutOfRangeException (ex.Message, ex);
			} finally {
				JNIEnv.DeleteLocalRef (lref);
				GC.KeepAlive (item);
			}
		}

		public bool Remove (T item)
		{
			int i = IndexOf (item);
			if (i < 0 && i >= Count)
				return false;
			RemoveAt (i);
			return true;
		}
		
		public static IList<T>? FromJniHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			if (handle == IntPtr.Zero)
				return null;

			var inst = (IJavaObject?) Java.Lang.Object.PeekObject (handle, typeof (IList<T>));
			if (inst == null)
				inst = new JavaList<T> (handle, transfer);
			else
				JNIEnv.DeleteRef (handle, transfer);

			return (IList<T>) inst;
		}

		public static IntPtr ToLocalJniHandle (IList<T>? items)
		{
			if (items == null)
				return IntPtr.Zero;

			var c = items as JavaList<T>;
			if (c != null)
				return JNIEnv.ToLocalJniHandle (c);

			using (c = new JavaList<T>(items))
				return JNIEnv.ToLocalJniHandle (c);
		}
	}
}
