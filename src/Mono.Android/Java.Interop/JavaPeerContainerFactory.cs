using System;
using System.Collections;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// Abstract base class for creating containers (arrays, collections) of Java peer types in an AOT-safe manner.
	/// Each <see cref="JavaPeerProxy"/> returns a <see cref="JavaPeerContainerFactory"/> for its target type,
	/// enabling creation of arrays, lists, dictionaries, and other generic containers without reflection.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this class exists:</b>
	/// </para>
	/// <para>
	/// Generic type instantiation like <c>new T[length]</c> or <c>new JavaList&lt;T&gt;()</c> requires knowing T
	/// at compile time. The traditional reflection-based approaches (<c>Array.CreateInstance()</c> or
	/// <c>Activator.CreateInstance()</c>) are not compatible with Native AOT and aggressive trimming.
	/// </para>
	/// <para>
	/// By having each proxy return a factory that is already typed to its specific T, we can perform
	/// these operations using direct <c>new</c> expressions which are fully AOT-safe and trimmer-safe.
	/// </para>
	/// <para>
	/// <b>Example usage in TypeMap:</b>
	/// </para>
	/// <code>
	/// // To create IList&lt;View&gt; from a Java ArrayList:
	/// var proxy = typeMap.GetProxyForType(typeof(Android.Views.View));
	/// var factory = proxy.GetContainerFactory();
	/// IList list = factory.CreateListFromHandle(javaArrayListHandle, ownership);
	/// </code>
	/// </remarks>
	public abstract class JavaPeerContainerFactory
	{
		/// <summary>
		/// Creates an array of the target type.
		/// </summary>
		/// <param name="length">The length of the array.</param>
		/// <param name="rank">The array rank: 1 for T[], 2 for T[][], 3 for T[][][].</param>
		/// <returns>A new array of the target type.</returns>
		internal abstract Array CreateArray (int length, int rank);

		/// <summary>
		/// Creates an empty JavaList for the target type.
		/// </summary>
		/// <returns>A new empty JavaList wrapping the target type.</returns>
		internal abstract IList CreateList ();

		/// <summary>
		/// Creates a JavaList wrapping an existing Java ArrayList handle.
		/// </summary>
		/// <param name="handle">The JNI handle to the Java ArrayList.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaList wrapping the Java object.</returns>
		internal abstract IList CreateListFromHandle (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates a JavaCollection wrapping an existing Java Collection handle.
		/// </summary>
		/// <param name="handle">The JNI handle to the Java Collection.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaCollection wrapping the Java object.</returns>
		internal abstract ICollection CreateCollectionFromHandle (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates an empty JavaSet for the target type.
		/// </summary>
		/// <returns>A new empty JavaSet wrapping the target type.</returns>
		internal abstract ICollection CreateSet ();

		/// <summary>
		/// Creates a JavaSet wrapping an existing Java Set handle.
		/// </summary>
		/// <param name="handle">The JNI handle to the Java Set.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaSet wrapping the Java object.</returns>
		internal abstract ICollection CreateSetFromHandle (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates an empty JavaDictionary with the specified key factory.
		/// </summary>
		/// <param name="keyFactory">The factory for the key type (provides type information).</param>
		/// <returns>A new empty JavaDictionary, or null if not supported.</returns>
		/// <remarks>
		/// Dictionary creation uses a visitor pattern because two type parameters are needed.
		/// This factory provides the value type; the key factory provides the key type.
		/// </remarks>
		internal virtual IDictionary? CreateDictionary (JavaPeerContainerFactory keyFactory) => null;

		/// <summary>
		/// Creates a JavaDictionary wrapping an existing Java Map handle.
		/// </summary>
		/// <param name="keyFactory">The factory for the key type.</param>
		/// <param name="handle">The JNI handle to the Java Map.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaDictionary wrapping the Java object, or null if not supported.</returns>
		internal virtual IDictionary? CreateDictionaryFromHandle (JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer) => null;

		/// <summary>
		/// Internal visitor method for dictionary creation. Called by value factory's CreateDictionary.
		/// Override in <see cref="JavaPeerContainerFactory{T}"/> to provide T as the key type.
		/// </summary>
		internal virtual IDictionary CreateDictionaryWithValueFactory<TValue> (JavaPeerContainerFactory<TValue> valueFactory) where TValue : class, IJavaPeerable
			=> throw new NotSupportedException ("Dictionary creation requires a typed JavaPeerContainerFactory<T>");

		/// <summary>
		/// Internal visitor method for dictionary creation from handle. Called by value factory's CreateDictionaryFromHandle.
		/// Override in <see cref="JavaPeerContainerFactory{T}"/> to provide T as the key type.
		/// </summary>
		internal virtual IDictionary CreateDictionaryFromHandleWithValueFactory<TValue> (JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer) where TValue : class, IJavaPeerable
			=> throw new NotSupportedException ("Dictionary creation requires a typed JavaPeerContainerFactory<T>");

		/// <summary>
		/// Creates a <see cref="JavaPeerContainerFactory"/> for the specified element type T.
		/// </summary>
		/// <typeparam name="T">The element type for arrays and collections.</typeparam>
		/// <returns>A singleton factory instance for the specified type.</returns>
		public static JavaPeerContainerFactory Create<T> () where T : class, IJavaPeerable
			=> JavaPeerContainerFactory<T>.Instance;
	}

	/// <summary>
	/// Generic implementation of <see cref="JavaPeerContainerFactory"/> for a specific element type T.
	/// This class is internal - use <see cref="JavaPeerContainerFactory.Create{T}"/> to obtain instances.
	/// </summary>
	/// <typeparam name="T">The element type for arrays and collections.</typeparam>
	/// <remarks>
	/// <para>
	/// This implementation uses direct <c>new</c> expressions which are AOT-safe and trimmer-safe.
	/// No reflection is used.
	/// </para>
	/// <para>
	/// <b>Example generated proxy:</b>
	/// </para>
	/// <code>
	/// sealed class ViewProxy : JavaPeerProxy {
	///     public override JavaPeerContainerFactory GetContainerFactory()
	///         => JavaPeerContainerFactory.Create&lt;View&gt;();
	/// }
	/// </code>
	/// </remarks>
	internal sealed class JavaPeerContainerFactory<T> : JavaPeerContainerFactory where T : class, IJavaPeerable
	{
		/// <summary>
		/// Singleton instance - no state, so safe to share across all usages.
		/// </summary>
		internal static readonly JavaPeerContainerFactory<T> Instance = new ();

		private JavaPeerContainerFactory () { }

		/// <inheritdoc/>
		internal override Array CreateArray (int length, int rank)
		{
			return rank switch {
				1 => new T[length],
				2 => new T[length][],
				3 => new T[length][][],
				_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1, 2, or 3"),
			};
		}

		/// <inheritdoc/>
		internal override IList CreateList () => new JavaList<T> ();

		/// <inheritdoc/>
		internal override IList CreateListFromHandle (IntPtr handle, JniHandleOwnership transfer)
			=> new JavaList<T> (handle, transfer);

		/// <inheritdoc/>
		internal override ICollection CreateCollectionFromHandle (IntPtr handle, JniHandleOwnership transfer)
			=> new JavaCollection<T> (handle, transfer);

		/// <inheritdoc/>
		internal override ICollection CreateSet () => new JavaSet<T> ();

		/// <inheritdoc/>
		internal override ICollection CreateSetFromHandle (IntPtr handle, JniHandleOwnership transfer)
			=> new JavaSet<T> (handle, transfer);

		/// <inheritdoc/>
		internal override IDictionary? CreateDictionary (JavaPeerContainerFactory keyFactory)
		{
			// T is the value type - ask key factory to create dictionary with us as value
			return keyFactory.CreateDictionaryWithValueFactory (this);
		}

		/// <inheritdoc/>
		internal override IDictionary? CreateDictionaryFromHandle (JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
		{
			return keyFactory.CreateDictionaryFromHandleWithValueFactory (this, handle, transfer);
		}

		/// <summary>
		/// Creates a JavaDictionary with T as the key type and TValue as the value type.
		/// Called by value factory's CreateDictionary method (visitor pattern).
		/// </summary>
		internal override IDictionary CreateDictionaryWithValueFactory<TValue> (JavaPeerContainerFactory<TValue> valueFactory)
			=> new JavaDictionary<T, TValue> ();

		/// <summary>
		/// Creates a JavaDictionary from handle with T as key type and TValue as value type.
		/// </summary>
		internal override IDictionary CreateDictionaryFromHandleWithValueFactory<TValue> (JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer)
			=> new JavaDictionary<T, TValue> (handle, transfer);
	}
}
