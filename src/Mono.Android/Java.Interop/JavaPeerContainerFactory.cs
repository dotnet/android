#nullable enable

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// AOT-safe factory for creating typed containers (lists, collections, dictionaries).
	/// Array creation lives in <c>JNIEnv.ArrayCreateInstance</c>.
	/// </summary>
	public abstract class JavaPeerContainerFactory
	{
		private protected const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

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
		internal virtual IDictionary? CreateDictionaryWithValueFactory<[DynamicallyAccessedMembers (Constructors)] TValue> (
			JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer)
			=> null;
	}

	/// <summary>
	/// Typed container factory. All creation uses direct <c>new</c> expressions — fully AOT-safe.
	/// </summary>
	/// <typeparam name="T">The container element type.</typeparam>
	public sealed class JavaPeerContainerFactory<[DynamicallyAccessedMembers (Constructors)] T> : JavaPeerContainerFactory
	{
		internal override IList CreateList (IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaList<T> (handle, transfer);

		internal override ICollection CreateCollection (IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaCollection<T> (handle, transfer);

		internal override IDictionary? CreateDictionary (JavaPeerContainerFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
			=> keyFactory.CreateDictionaryWithValueFactory (this, handle, transfer);

		internal override IDictionary? CreateDictionaryWithValueFactory<[DynamicallyAccessedMembers (Constructors)] TValue> (
			JavaPeerContainerFactory<TValue> valueFactory, IntPtr handle, JniHandleOwnership transfer)
			=> new Android.Runtime.JavaDictionary<T, TValue> (handle, transfer);
	}
}
