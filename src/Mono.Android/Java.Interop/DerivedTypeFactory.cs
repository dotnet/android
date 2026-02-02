using System;
using System.Collections;
using System.Collections.Generic;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// Abstract base class for creating derived types (arrays, collections) in an AOT-safe manner.
	/// Each JavaPeerProxy returns a DerivedTypeFactory for its target type, enabling creation
	/// of arrays, lists, dictionaries, and other generic containers without reflection.
	/// </summary>
	/// <remarks>
	/// The key insight is that generic type instantiation like <c>new T[length]</c> or <c>new JavaList&lt;T&gt;()</c>
	/// requires knowing T at compile time. By having the proxy return a factory typed to its specific T,
	/// we can perform these operations without <c>Array.CreateInstance()</c> or <c>Activator.CreateInstance()</c>.
	/// 
	/// Example usage in TypeMap:
	/// <code>
	/// // To create IList&lt;View&gt; from a Java ArrayList:
	/// var proxy = typeMap.GetProxyForType(typeof(Android.Views.View));
	/// var factory = proxy.GetDerivedTypeFactory();
	/// IList list = factory.CreateListFromHandle(javaArrayListHandle, ownership);
	/// </code>
	/// </remarks>
	public abstract class DerivedTypeFactory
	{
		/// <summary>
		/// Creates an array of the target type.
		/// </summary>
		/// <param name="length">The length of the array.</param>
		/// <param name="rank">The array rank: 1 for T[], 2 for T[][], 3 for T[][][].</param>
		/// <returns>A new array of the target type.</returns>
		public abstract Array CreateArray (int length, int rank);

		/// <summary>
		/// Creates an empty JavaList for the target type.
		/// </summary>
		/// <returns>A new empty JavaList wrapping the target type.</returns>
		public abstract IList CreateList ();

		/// <summary>
		/// Creates a JavaList wrapping an existing Java ArrayList handle.
		/// </summary>
		/// <param name="handle">The JNI handle to the Java ArrayList.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaList wrapping the Java object.</returns>
		public abstract IList CreateListFromHandle (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates a JavaCollection wrapping an existing Java Collection handle.
		/// </summary>
		/// <param name="handle">The JNI handle to the Java Collection.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaCollection wrapping the Java object.</returns>
		public abstract ICollection CreateCollectionFromHandle (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates an empty JavaSet for the target type.
		/// </summary>
		/// <returns>A new empty JavaSet wrapping the target type.</returns>
		public abstract ICollection CreateSet ();

		/// <summary>
		/// Creates a JavaSet wrapping an existing Java Set handle.
		/// </summary>
		/// <param name="handle">The JNI handle to the Java Set.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaSet wrapping the Java object.</returns>
		public abstract ICollection CreateSetFromHandle (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates an empty JavaDictionary with the specified key factory.
		/// </summary>
		/// <param name="keyFactory">The factory for the key type (provides type information).</param>
		/// <returns>A new empty JavaDictionary, or null if not supported.</returns>
		public virtual IDictionary? CreateDictionary (DerivedTypeFactory keyFactory) => null;

		/// <summary>
		/// Creates a JavaDictionary wrapping an existing Java Map handle.
		/// </summary>
		/// <param name="keyFactory">The factory for the key type.</param>
		/// <param name="handle">The JNI handle to the Java Map.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A JavaDictionary wrapping the Java object, or null if not supported.</returns>
		public virtual IDictionary? CreateDictionaryFromHandle (DerivedTypeFactory keyFactory, IntPtr handle, JniHandleOwnership transfer) => null;
	}

	/// <summary>
	/// Generic implementation of DerivedTypeFactory for a specific element type T.
	/// This class is instantiated by generated proxy types to provide type-safe factory methods.
	/// </summary>
	/// <typeparam name="T">The element type for arrays and collections.</typeparam>
	/// <remarks>
	/// This implementation uses direct <c>new</c> expressions which are AOT-safe and trimmer-safe.
	/// No reflection is used.
	/// 
	/// Example generated proxy:
	/// <code>
	/// sealed class ViewProxy : JavaPeerProxy {
	///     public override DerivedTypeFactory GetDerivedTypeFactory() 
	///         => DerivedTypeFactory&lt;View&gt;.Instance;
	/// }
	/// </code>
	/// </remarks>
	public sealed class DerivedTypeFactory<T> : DerivedTypeFactory where T : class
	{
		/// <summary>
		/// Singleton instance - no state, so safe to share across all usages.
		/// </summary>
		public static readonly DerivedTypeFactory<T> Instance = new ();

		private DerivedTypeFactory () { }

		/// <inheritdoc/>
		public override Array CreateArray (int length, int rank)
		{
			return rank switch {
				1 => new T[length],
				2 => new T[length][],
				3 => new T[length][][],
				_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1, 2, or 3"),
			};
		}

		/// <inheritdoc/>
		public override IList CreateList () => new JavaList<T> ();

		/// <inheritdoc/>
		public override IList CreateListFromHandle (IntPtr handle, JniHandleOwnership transfer)
			=> new JavaList<T> (handle, transfer);

		/// <inheritdoc/>
		public override ICollection CreateCollectionFromHandle (IntPtr handle, JniHandleOwnership transfer)
			=> new JavaCollection<T> (handle, transfer);

		/// <inheritdoc/>
		public override ICollection CreateSet () => new JavaSet<T> ();

		/// <inheritdoc/>
		public override ICollection CreateSetFromHandle (IntPtr handle, JniHandleOwnership transfer)
			=> new JavaSet<T> (handle, transfer);

		/// <inheritdoc/>
		public override IDictionary? CreateDictionary (DerivedTypeFactory keyFactory)
		{
			// We need the key factory to be generic to create the dictionary
			// This uses a visitor pattern - we ask the key factory to create the dictionary with us as value
			return keyFactory.CreateDictionaryWithValueFactory (this);
		}

		/// <inheritdoc/>
		public override IDictionary? CreateDictionaryFromHandle (DerivedTypeFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
		{
			return keyFactory.CreateDictionaryFromHandleWithValueFactory (this, handle, transfer);
		}

		/// <summary>
		/// Creates a JavaDictionary with this type as the key and the provided factory's type as the value.
		/// Called by value factory's CreateDictionary method (visitor pattern).
		/// </summary>
		internal IDictionary CreateDictionaryWithValueFactory<TValue> (DerivedTypeFactory<TValue> valueFactory) where TValue : class
			=> new JavaDictionary<T, TValue> ();

		/// <summary>
		/// Creates a JavaDictionary from handle with this type as key and provided factory's type as value.
		/// </summary>
		internal IDictionary CreateDictionaryFromHandleWithValueFactory<TValue> (DerivedTypeFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer) where TValue : class
			=> new JavaDictionary<T, TValue> (handle, transfer);
	}

	// Extension methods on base class to support visitor pattern for dictionaries
	public static class DerivedTypeFactoryExtensions
	{
		/// <summary>
		/// Creates a dictionary using the visitor pattern - the key factory creates the dictionary
		/// with both type parameters known.
		/// </summary>
		internal static IDictionary? CreateDictionaryWithValueFactory<TValue> (this DerivedTypeFactory keyFactory, DerivedTypeFactory<TValue> valueFactory) where TValue : class
		{
			// Dynamic dispatch to the correct generic key factory
			return keyFactory switch {
				DerivedTypeFactory<Java.Lang.Object> f => f.CreateDictionaryWithValueFactory (valueFactory),
				DerivedTypeFactory<Java.Lang.String> f => f.CreateDictionaryWithValueFactory (valueFactory),
				// Add more common key types as needed, or use a registration mechanism
				_ => null // Unsupported key type
			};
		}

		internal static IDictionary? CreateDictionaryFromHandleWithValueFactory<TValue> (this DerivedTypeFactory keyFactory, DerivedTypeFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer) where TValue : class
		{
			return keyFactory switch {
				DerivedTypeFactory<Java.Lang.Object> f => f.CreateDictionaryFromHandleWithValueFactory (valueFactory, handle, transfer),
				DerivedTypeFactory<Java.Lang.String> f => f.CreateDictionaryFromHandleWithValueFactory (valueFactory, handle, transfer),
				_ => null
			};
		}
	}
}
