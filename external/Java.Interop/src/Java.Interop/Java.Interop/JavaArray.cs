#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Java.Interop
{
	public abstract class JavaArray<T> : JavaObject, IList, IList<T>
	{
		internal delegate TArray ArrayCreator<TArray> (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			where TArray : JavaArray<T>;

		// Value was created via CreateMarshalCollection, and thus can
		// be disposed of with impunity when no longer needed.
		internal bool forMarshalCollection;

		internal JavaArray (ref JniObjectReference handle, JniObjectReferenceOptions transfer)
			: base (ref handle, transfer)
		{
		}

		public int Length {
			get {return JniEnvironment.Arrays.GetArrayLength (PeerReference);}
		}

		[MaybeNull]
		public abstract T this [int index] {
			// I think this will be fixable in .NET5+ with support for "T?"
#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
			get;
#pragma warning restore CS8766
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
#pragma warning disable CS8603 // Possible null reference return.
				yield return this [i];
#pragma warning restore CS8603 // Possible null reference return.
		}

		internal static void CheckArrayCopy (int sourceIndex, int sourceLength, int destinationIndex, int destinationLength, int length)
		{
			if (sourceIndex < 0)
				throw new ArgumentOutOfRangeException (nameof (sourceIndex), $"source index must be >= 0; was {sourceIndex}.");
			if (sourceIndex != 0 && sourceIndex >= sourceLength)
				throw new ArgumentException ("source index is > source length.", nameof (sourceIndex));
			if (checked(sourceIndex + length) > sourceLength)
				throw new ArgumentException ("source index + length >= source length", nameof (length));
			if (destinationIndex < 0)
				throw new ArgumentOutOfRangeException (nameof (destinationIndex), $"destination index must be >= 0; was {destinationIndex}.");
			if (destinationIndex != 0 && destinationIndex >= destinationLength)
				throw new ArgumentException ("destination index is > destination length.", nameof (destinationIndex));
			if (checked (destinationIndex + length) > destinationLength)
				throw new ArgumentException ("destination index + length >= destination length", nameof (length));
		}

		internal static int CheckLength (int length)
		{
			if (length < 0)
				throw new ArgumentException ("'length' cannot be negative.", nameof (length));
			return length;
		}

		internal static int CheckLength (IList<T> value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));
			return value.Count;
		}

		internal static IList<T> ToList (IEnumerable<T> value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));
			if (value is IList<T> list)
				return list;
			return value.ToList ();
		}

		internal IList<T> ToTargetType (Type? targetType, bool dispose)
		{
			if (TargetTypeIsCurrentType (targetType))
				return this;
			if (targetType == typeof (T[]) || targetType.IsAssignableFrom (typeof (IList<T>))) {
				try {
					return ToArray ();
				} finally {
					if (dispose)
						Dispose ();
				}
			}
			throw CreateMarshalNotSupportedException (GetType (), targetType);
		}

		internal virtual bool TargetTypeIsCurrentType ([NotNullWhen (false)]Type? targetType)
		{
			return targetType == null || targetType == typeof (JavaArray<T>);
		}

		internal static Exception CreateMarshalNotSupportedException (Type sourceType, Type? targetType)
		{
			return new NotSupportedException (
					string.Format ("Do not know how to marshal a `{0}`{1}.",
						sourceType.FullName,
						targetType != null ? $" into a `{targetType.FullName}`" : ""));
		}

		internal static IList<T> CreateValue<TArray> (ref JniObjectReference reference, JniObjectReferenceOptions transfer, Type? targetType, ArrayCreator<TArray> creator)
			where TArray : JavaArray<T>
		{
			return creator (ref reference, transfer)
				.ToTargetType (targetType, dispose: true);
		}

		internal    static  JniValueMarshalerState  CreateArgumentState<TArray> (IList<T>? value, ParameterAttributes synchronize, Func<IList<T>, bool, TArray> creator)
			where TArray : JavaArray<T>
		{
			if (value == null)
				return new JniValueMarshalerState ();
			if (value is TArray v) {
				return new JniValueMarshalerState (v);
			}
			var list = value as IList<T>;
			if (list == null)
				throw CreateMarshalNotSupportedException (value.GetType (), typeof (TArray));
			synchronize = GetCopyDirection (synchronize);
			var c   = (synchronize & ParameterAttributes.In) == ParameterAttributes.In;
			var a   = creator (list, c);
			return new JniValueMarshalerState (a);
		}

		internal static void DestroyArgumentState<TArray> (IList<T>? value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
			where TArray : JavaArray<T>
		{
			var source = (TArray?) state.PeerableValue;
			if (source == null)
				return;

			synchronize = GetCopyDirection (synchronize);
			if ((synchronize & ParameterAttributes.Out) == ParameterAttributes.Out) {
				var arrayDest = value as T[];
				var listDest  = value as IList<T>;
				if (arrayDest != null)
					source.CopyTo (arrayDest, 0);
				else if (listDest != null)
					source.CopyToList (listDest, 0);
			}

			if (source.forMarshalCollection) {
				source.Dispose ();
			}

			state   = new JniValueMarshalerState ();
		}

		internal static ParameterAttributes GetCopyDirection (ParameterAttributes value)
		{
			// If .In or .Out are specified, use as-is.
			// Otherwise, we should copy both directions.
			const   ParameterAttributes     inout   = ParameterAttributes.In | ParameterAttributes.Out;
			if ((value & inout) != 0)
				return (value & inout);
			return inout;
		}

		internal virtual void CopyToList (IList<T> list, int index)
		{
			int len = Length;
			for (int i = 0; i < len; i++) {
#pragma warning disable CS8601 // Possible null reference assignment.
				list [index + i] = this [i];
#pragma warning restore CS8601 // Possible null reference assignment.
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

		object? IList.this [int index] {
			get {return this [index];}
#pragma warning disable 8600,8601
			set {this [index] = (T) value;}
#pragma warning restore 8600,8601
		}

		void ICollection.CopyTo (Array array, int index)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));
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

		bool IList.Contains (object? value)
		{
			if (value is T)
				return Contains ((T) value);
			return false;
		}

		int IList.IndexOf (object? value)
		{
			if (value is T)
				return IndexOf ((T) value);
			return -1;
		}

		int IList.Add (object? value)
		{
			throw new NotSupportedException ();
		}

		void IList.Insert (int index, object? value)
		{
			throw new NotSupportedException ();
		}

		void IList.Remove (object? value)
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

	public abstract class JniArrayElements : IDisposable {

		IntPtr elements;
		int size;

		internal JniArrayElements (IntPtr elements, int size)
		{
			if (elements == IntPtr.Zero)
				throw new ArgumentException ("'elements' must not be IntPtr.Zero.", nameof (elements));
			this.elements = elements;
			this.size = size;
		}

		internal bool IsDisposed {
			get {return elements == IntPtr.Zero;}
		}

		public  IntPtr  Elements {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				return elements;
			}
		}

		public int Size {
			get {
				if (IsDisposed)
					throw new ObjectDisposedException (GetType ().FullName);
				return size;
			}
		}

		protected   abstract    void    Synchronize (JniReleaseArrayElementsMode releaseMode);

		public void CopyToJava ()
		{
			Synchronize (JniReleaseArrayElementsMode.Commit);
		}

		public void Release (JniReleaseArrayElementsMode releaseMode)
		{
			if (IsDisposed)
				throw new ObjectDisposedException (GetType ().FullName);
			Synchronize (releaseMode);
			elements = IntPtr.Zero;
		}

		public void Dispose ()
		{
			if (IsDisposed)
				return;
			Release (JniReleaseArrayElementsMode.Default);
		}
	}
	
	public abstract class JavaPrimitiveArray<T> : JavaArray<T> {

		internal JavaPrimitiveArray (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			: base (ref reference, transfer)
		{
		}

		public override void DisposeUnlessReferenced ()
		{
			if (forMarshalCollection) {
				Dispose ();
				return;
			}
			base.DisposeUnlessReferenced ();
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
					throw new ArgumentOutOfRangeException (nameof (index), "index >= Length");
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

		internal static T[] ToArray (IEnumerable<T> value)
		{
			if (value is T [] array)
				return array;
			return value.ToArray ();
		}
	}
}

