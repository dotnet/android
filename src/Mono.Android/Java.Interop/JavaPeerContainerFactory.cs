#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// AOT-safe factory for creating typed containers (arrays, lists, collections, dictionaries)
	/// without using <c>MakeGenericType()</c> or <c>Array.CreateInstance()</c>.
	/// </summary>
	public abstract class JavaPeerContainerFactory
	{
		/// <summary>
		/// Creates a typed jagged array. Rank 1 = T[], rank 2 = T[][], etc.
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
		public static JavaPeerContainerFactory Create<
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			T
		> () where T : class, IJavaPeerable
			=> JavaPeerContainerFactory<T>.Instance;
	}

	/// <summary>
	/// Typed container factory. All creation uses direct <c>new</c> expressions — fully AOT-safe.
	/// </summary>
	/// <typeparam name="T">The Java peer element type.</typeparam>
	public sealed class JavaPeerContainerFactory<
	 	// TODO (https://github.com/dotnet/android/issues/10794): Remove this DAM annotation — it preserves too much reflection metadata on all types in the typemap.
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		T
	> : JavaPeerContainerFactory
		where T : class, IJavaPeerable
	{
		internal static readonly JavaPeerContainerFactory<T> Instance = new ();

		JavaPeerContainerFactory () { }

		// TODO (https://github.com/dotnet/android/issues/10794): This might cause unnecessary code bloat for NativeAOT. Revisit
		// how we use this API and instead use a differnet approach that uses AOT-safe `Array.CreateInstanceFromArrayType`
		// with statically provided array types based on a statically known array type.
		internal override Array CreateArray (int length, int rank) => rank switch {
			1 => new T [length],
			2 => new T [length][],
			3 => new T [length][][],
			_ when rank >= 0 => CreateHigherRankArray (length, rank),
			_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be non-negative."),
		};

		static Array CreateHigherRankArray (int length, int rank)
		{
			if (!RuntimeFeature.IsDynamicCodeSupported) {
				throw new NotSupportedException ($"Cannot create array of rank {rank} because dynamic code is not supported.");
			}

			var arrayType = typeof (T);
			for (int i = 0; i < rank; i++) {
				arrayType = arrayType.MakeArrayType ();
			}

			return Array.CreateInstanceFromArrayType (arrayType, length);
		}

		internal override IList CreateList (IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaList<T> (handle, transfer);

		internal override ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaCollection<T> (handle, transfer);

		internal override IDictionary? CreateDictionary (JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
			=> keyFactory.CreateDictionaryWithValueFactory (this, handle, transfer);

		#pragma warning disable IL2091 // DynamicallyAccessedMembers on base method type parameter cannot be repeated on override in C#
		internal override IDictionary? CreateDictionaryWithValueFactory<TValue> (
			JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaDictionary<T, TValue> (handle, transfer);
		#pragma warning restore IL2091
	}
}
