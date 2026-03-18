#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace Java.Interop
{
	/// <summary>
	/// AOT-safe factory for creating typed containers (arrays, lists, collections, dictionaries)
	/// without using <c>MakeGenericType()</c> or <c>Array.CreateInstance()</c>.
	/// </summary>
	public abstract class JavaPeerContainerFactory
	{
		/// <summary>
		/// Creates a typed array. Rank 1 = T[], rank 2 = T[][], rank 3 = T[][][].
		/// </summary>
		internal abstract Array CreateArray (int length, int rank);

		/// <summary>
		/// Creates a typed <c>JavaList&lt;T&gt;</c> from a JNI handle.
		/// </summary>
		internal abstract IList CreateList (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates a typed <c>JavaCollection&lt;T&gt;</c> from a JNI handle.
		/// </summary>
		internal abstract ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates a typed <c>JavaDictionary&lt;TKey, TValue&gt;</c> using the visitor pattern.
		/// This factory provides the value type; <paramref name="keyFactory"/> provides the key type.
		/// </summary>
		internal virtual IDictionary? CreateDictionary (JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
			=> null;

		/// <summary>
		/// Visitor callback invoked by the value factory's <see cref="CreateDictionary"/>.
		/// Override in <see cref="JavaPeerContainerFactory{T}"/> to provide both type parameters.
		/// </summary>
		internal virtual IDictionary? CreateDictionaryWithValueFactory<TValue> (
			JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer)
			where TValue : class, IJavaPeerable
			=> null;

		/// <summary>
		/// Creates a <see cref="JavaPeerContainerFactory{T}"/> singleton for the specified type.
		/// </summary>
		public static JavaPeerContainerFactory Create<T> () where T : class, IJavaPeerable
			=> JavaPeerContainerFactory<T>.Instance;
	}

	/// <summary>
	/// Typed container factory. All creation uses direct <c>new</c> expressions — fully AOT-safe.
	/// </summary>
	/// <typeparam name="T">The Java peer element type.</typeparam>
	public sealed class JavaPeerContainerFactory<T> : JavaPeerContainerFactory
		where T : class, IJavaPeerable
	{
		internal static readonly JavaPeerContainerFactory<T> Instance = new ();

		JavaPeerContainerFactory () { }

		internal override Array CreateArray (int length, int rank) => rank switch {
			1 => new T [length],
			2 => new T [length][],
			3 => new T [length][][],
			_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Array rank must be 1, 2, or 3."),
		};

		internal override IList CreateList (IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaList<T> (handle, transfer);

		internal override ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaCollection<T> (handle, transfer);

		internal override IDictionary? CreateDictionary (JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
			=> keyFactory.CreateDictionaryWithValueFactory (this, handle, transfer);

		internal override IDictionary? CreateDictionaryWithValueFactory<TValue> (
			JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaDictionary<T, TValue> (handle, transfer);
	}
}
