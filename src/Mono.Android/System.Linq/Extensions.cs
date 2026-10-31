using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

using Java.Interop;

namespace System.Linq {

	/// <summary>
	/// Provides LINQ-friendly extension methods for converting Java collection types
	/// into managed <see cref="System.Collections.IEnumerable" /> sequences.
	/// </summary>
	public static class Extensions {

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		static IntPtr id_next;

		static Extensions ()
		{
			IntPtr grefIterable = JNIEnv.FindClass ("java/util/Iterator");
			unsafe {
				// SAFETY: the class reference is owned here; next is the parameterless Iterator method returning Object.
				id_next = JNIEnv.GetMethodID (grefIterable, "next", "()Ljava/lang/Object;");
				JNIEnv.DeleteGlobalRef (grefIterable);
			}
		}

		/// <summary>
		/// Returns an <see cref="System.Collections.IEnumerable" /> that iterates over a Java
		/// <see cref="Java.Lang.IIterable" />, allowing <c>foreach</c> and LINQ to be used with Java
		/// collection types. Each element is marshaled from its Java instance to the corresponding
		/// managed type.
		/// </summary>
		/// <param name="source">The Java <see cref="Java.Lang.IIterable" /> to enumerate.</param>
		/// <returns>An <see cref="System.Collections.IEnumerable" /> over the elements of <paramref name="source" />.</returns>
		/// <exception cref="System.ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
		public static IEnumerable ToEnumerable (this Java.Lang.IIterable source)
		{
			if (source == null)
				throw new ArgumentNullException ("source");
			using (var iterator = source.Iterator ())
				while (iterator.HasNext) {
					// SAFETY: iterator is retained by using and id_next is the matching zero-argument Object-returning member.
					// The returned local transfers to conversion before iterator suspension, including its existing failure path.
					yield return unsafe(JavaConvert.FromJniHandle (
							JNIEnv.CallObjectMethod (iterator.Handle, id_next),
							JniHandleOwnership.TransferLocalRef));
				}
		}

		internal static IEnumerator ToEnumerator_Dispose (this Java.Util.IIterator source)
		{
			using (source)
				while (source.HasNext) {
					// SAFETY: source is retained by using and id_next is the matching zero-argument Object-returning member.
					// The returned local transfers to conversion before iterator suspension, including its existing failure path.
					yield return unsafe(JavaConvert.FromJniHandle (
							JNIEnv.CallObjectMethod (source.Handle, id_next),
							JniHandleOwnership.TransferLocalRef));
				}
		}

		/// <summary>
		/// Returns an <see cref="System.Collections.Generic.IEnumerable{T}" /> that iterates over a Java
		/// <see cref="Java.Lang.IIterable" />, marshaling each element to <typeparamref name="T" />. This
		/// allows <c>foreach</c> and LINQ to be used with Java collection types.
		/// </summary>
		/// <typeparam name="T">The managed type to marshal each Java element to.</typeparam>
		/// <param name="source">The Java <see cref="Java.Lang.IIterable" /> to enumerate.</param>
		/// <returns>An <see cref="System.Collections.Generic.IEnumerable{T}" /> over the elements of <paramref name="source" />.</returns>
		/// <exception cref="System.ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
		public static IEnumerable<T?> ToEnumerable<
				[DynamicallyAccessedMembers (Constructors)] 
				T
		> (this Java.Lang.IIterable source)
		{
			if (source == null)
				throw new ArgumentNullException ("source");
			using (var iterator = source.Iterator ())
				while (iterator.HasNext) {
					// SAFETY: iterator is retained by using and id_next is the matching zero-argument Object-returning member.
					// Conversion receives the owned local before yield; no JNI reference or unsafe context spans suspension.
					yield return unsafe(JavaConvert.FromJniHandle<T> (
							JNIEnv.CallObjectMethod (iterator.Handle, id_next),
							JniHandleOwnership.TransferLocalRef));
				}
		}

		internal static IEnumerator<T> ToEnumerator_Dispose<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (this Java.Util.IIterator source)
		{
			using (source)
				while (source.HasNext) {
#pragma warning disable CS8603 // Java iterators can return null even when T is non-nullable.
					// SAFETY: source is retained by using and id_next is the matching zero-argument Object-returning member.
					// Conversion receives the owned local before yield; no JNI reference or unsafe context spans suspension.
					yield return unsafe(JavaConvert.FromJniHandle<T> (
							JNIEnv.CallObjectMethod (source.Handle, id_next),
							JniHandleOwnership.TransferLocalRef));
#pragma warning restore CS8603
				}
		}
	}
}
