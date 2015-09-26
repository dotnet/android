using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Java.Interop
{
	public abstract class JavaArray<T> : JavaObject, IList, IList<T>
	{
		// Value was created via CreateMarshalCollection, and thus can
		// be disposed of with impunity when no longer needed.
		protected bool forMarshalCollection;

		internal JavaArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public int Length {
			get {return JniEnvironment.Arrays.GetArrayLength (SafeHandle);}
		}

		public abstract T this [int index] {
			get;
			set;
		}

		public  abstract    void    Clear ();
		public  abstract    void    CopyTo (T[] array, int arrayIndex);
		public  abstract    int     IndexOf (T item);

		public virtual bool Contains (T item)
		{
			return IndexOf (item) >= 0;
		}

		public bool IsReadOnly {
			get {
				return false;
			}
		}

		public T[] ToArray ()
		{
			var a = new T [Length];
			CopyTo (a, 0);
			return a;
		}

		public virtual IEnumerator<T> GetEnumerator ()
		{
			int len = Length;
			for (int i = 0; i < len; ++i)
				yield return this [i];
		}

		internal static void CheckArrayCopy (int sourceIndex, int sourceLength, int destinationIndex, int destinationLength, int length)
		{
			if (sourceIndex < 0)
				throw new ArgumentOutOfRangeException ("sourceIndex", "source index must be >= 0; was " + sourceIndex + ".");
			if (sourceIndex != 0 && sourceIndex >= sourceLength)
				throw new ArgumentException ("source index is > source length.", "sourceIndex");
			if (checked(sourceIndex + length) > sourceLength)
				throw new ArgumentException ("source index + length >= source length", "length");
			if (destinationIndex < 0)
				throw new ArgumentOutOfRangeException ("destinationIndex", "destination index must be >= 0; was " + destinationIndex + ".");
			if (destinationIndex != 0 && destinationIndex >= destinationLength)
				throw new ArgumentException ("destination index is > destination length.", "destinationIndex");
			if (checked (destinationIndex + length) > destinationLength)
				throw new ArgumentException ("destination index + length >= destination length", "length");
		}

		internal static int CheckLength (int length)
		{
			if (length < 0)
				throw new ArgumentException ("'length' cannot be negative.", "length");
			return length;
		}

		internal static int CheckLength (IList<T> value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			return value.Count;
		}

		internal static IList<T> _ToList (IEnumerable<T> value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			IList<T> list = value as IList<T>;
			if (list != null)
				return list;
			return value.ToList ();
		}

		internal IList<T> ToTargetType (Type targetType, bool dispose)
		{
			if (TargetTypeIsCurrentType (targetType))
				return this;
			if (targetType == typeof (T[])) {
				try {
					return ToArray ();
				} finally {
					if (dispose)
						Dispose ();
				}
			}
			throw CreateMarshalNotSupportedException (GetType (), targetType);
		}

		internal virtual bool TargetTypeIsCurrentType (Type targetType)
		{
			return targetType == null || targetType == typeof (JavaArray<T>);
		}

		internal static Exception CreateMarshalNotSupportedException (Type sourceType, Type targetType)
		{
			throw new NotSupportedException (
					string.Format ("Do not know how to marshal a '{0}' into a '{1}'.",
						sourceType.FullName, targetType.FullName));
		}

		internal static JniLocalReference CreateLocalRef<TArray> (object value, Func<IList<T>, TArray> creator)
			where TArray : JavaArray<T>
		{
			if (value == null)
				return new JniLocalReference ();
			var array = value as TArray;
			if (array != null)
				return array.SafeHandle.NewLocalRef ();
			var items = value as IList<T>;
			if (items == null)
				throw CreateMarshalNotSupportedException (value.GetType (), typeof (TArray));
			using (array = creator (items))
				return array.SafeHandle.NewLocalRef ();
		}

		internal static IList<T> GetValueFromJni<TArray> (JniReferenceSafeHandle handle, JniHandleOwnership transfer, Type targetType, Func<JniReferenceSafeHandle, JniHandleOwnership, TArray> creator)
			where TArray : JavaArray<T>
		{
			var value = JniEnvironment.Current.JavaVM.PeekObject (handle);
			var array = value as TArray;
			if (array != null) {
				JniEnvironment.Handles.Dispose (handle, transfer);
				return array.ToTargetType (targetType, dispose: false);
			}
			return creator (handle, transfer)
				.ToTargetType (targetType, dispose: true);
		}

		internal static IJavaObject CreateMarshalCollection<TArray> (object value, Func<IList<T>, TArray> creator)
			where TArray : JavaArray<T>
		{
			if (value == null)
				return null;
			var v = value as TArray;
			if (v != null)
				return v;
			var list = value as IList<T>;
			if (list == null)
				throw CreateMarshalNotSupportedException (value.GetType (), typeof (TArray));
			return creator (list);
		}

		internal static void CleanupMarshalCollection<TArray> (IJavaObject marshalObject, object value)
			where TArray : JavaArray<T>
		{
			var source = (TArray) marshalObject;
			if (source == null)
				return;

			var arrayDest = value as T[];
			var listDest  = value as IList<T>;
			if (arrayDest != null)
				source.CopyTo (arrayDest, 0);
			else if (listDest != null)
				source.CopyToList (listDest, 0);

			if (source.forMarshalCollection) {
				source.Dispose ();
			}
		}

		internal virtual void CopyToList (IList<T> list, int index)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
				list [index + i] = this [i];
			}
		}

		bool ICollection.IsSynchronized {
			get {
				return false;
			}
		}

		object ICollection.SyncRoot {
			get {
				return this;
			}
		}

		int ICollection<T>.Count {
			get {return Length;}
		}

		int ICollection.Count {
			get {return Length;}
		}

		bool IList.IsFixedSize {
			get {
				return true;
			}
		}

		object IList.this [int index] {
			get {return this [index];}
			set {this [index] = (T) value;}
		}

		void ICollection.CopyTo (Array array, int index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");
			CheckArrayCopy (0, Length, index, array.Length, Length);
			int len = Length;
			for (int i = 0; i < len; i++)
				array.SetValue (this [i], index + i);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		void ICollection<T>.Add (T item)
		{
			throw new NotSupportedException ();
		}

		bool ICollection<T>.Remove (T item)
		{
			throw new NotSupportedException ();
		}

		bool IList.Contains (object value)
		{
			if (value is T)
				return Contains ((T) value);
			return false;
		}

		int IList.IndexOf (object value)
		{
			if (value is T)
				return IndexOf ((T) value);
			return -1;
		}

		int IList.Add (object value)
		{
			throw new NotSupportedException ();
		}

		void IList.Insert (int index, object value)
		{
			throw new NotSupportedException ();
		}

		void IList.Remove (object value)
		{
			throw new NotSupportedException ();
		}

		void IList.RemoveAt (int index)
		{
			throw new NotSupportedException ();
		}

		void IList<T>.Insert (int index, T item)
		{
			throw new NotSupportedException ();
		}

		void IList<T>.RemoveAt (int index)
		{
			throw new NotSupportedException ();
		}
	}

	public enum JniArrayElementsReleaseMode {
		CopyBack        = 0,
		DoNotCopyBack   = 2
	}

	public abstract class JniArrayElements : IDisposable {

		internal const int JNI_COMMIT = 1;

		IntPtr elements;

		internal JniArrayElements (IntPtr elements)
		{
			if (elements == IntPtr.Zero)
				throw new ArgumentException ("'elements' must not be IntPtr.Zero.", "elements");
			this.elements = elements;
		}

		bool IsDisposed {
			get {return elements == IntPtr.Zero;}
		}

		public  IntPtr  Elements {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				return elements;
			}
		}

		protected   abstract    void    Synchronize (JniArrayElementsReleaseMode releaseMode);

		public void CopyToJava ()
		{
			Synchronize ((JniArrayElementsReleaseMode) JNI_COMMIT);
		}

		public void Release (JniArrayElementsReleaseMode releaseMode)
		{
			if (IsDisposed)
				throw new ObjectDisposedException (GetType ().FullName);;
			Synchronize (releaseMode);
			elements = IntPtr.Zero;
		}

		public void Dispose ()
		{
			if (IsDisposed)
				return;
			Release (JniArrayElementsReleaseMode.CopyBack);
		}
	}
	
	public abstract class JavaPrimitiveArray<T> : JavaArray<T> {

		internal JavaPrimitiveArray (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public      abstract    void    CopyTo (int sourceIndex, T[] destinationArray, int destinationIndex, int length);
		public      abstract    void    CopyFrom (T[] sourceArray, int sourceIndex, int destinationIndex, int length);

		protected   abstract    JniArrayElements   CreateElements ();

		public override T this [int index] {
			get {
				var buf = new T [1];
				CopyTo (index, buf, 0, buf.Length);
				return buf [0];
			}
			set {
				if (index >= Length)
					throw new ArgumentOutOfRangeException ("index", "index >= Length");
				var buf = new T []{ value };
				CopyFrom (buf, 0, index, buf.Length);
			}
		}

		public JniArrayElements GetElements ()
		{
			return CreateElements ();
		}

		public override void CopyTo (T[] array, int arrayIndex)
		{
			CopyTo (0, array, arrayIndex, Length);
		}

		internal static T[] _ToArray (IEnumerable<T> value)
		{
			var array = value as T[];
			if (array != null)
				return array;
			return value.ToArray ();
		}
	}
}

