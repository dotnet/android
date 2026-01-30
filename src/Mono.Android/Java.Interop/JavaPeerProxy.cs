using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// Base attribute class for generated proxy types that enable AOT-safe type mapping between Java and .NET types.
	/// Each proxy attribute is applied to the target .NET type and provides trim-safe access to the target type's constructors.
	/// </summary>
	/// <remarks>
	/// This attribute is not intended for direct use by application developers.
	/// Proxy attributes are generated at build time by the ILLink step and applied to target types.
	/// 
	/// The key insight is that .NET's custom attribute mechanism creates attribute instances in an AOT-safe manner,
	/// so by making the proxy an attribute applied to the target type, we can use <c>GetCustomAttribute&lt;JavaPeerProxy&gt;()</c>
	/// to get the proxy instance without using <c>Activator.CreateInstance()</c>.
	/// 
	/// Example generated proxy for a concrete type:
	/// <code>
	/// // Generated attribute applied to the target type
	/// [ActivityProxy]
	/// class Activity : Java.Lang.Object { ... }
	/// 
	/// // Generated proxy attribute type
	/// [AttributeUsage(AttributeTargets.Class, Inherited = false)]
	/// sealed class ActivityProxy : JavaPeerProxy
	/// {
	///     public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
	///         => new Activity(handle, transfer);
	///     public override Array CreateArray(int length) => new Activity[length];
	///     public override Array CreateArray2(int length) => new Activity[length][];
	/// }
	/// </code>
	/// 
	/// Example generated proxy for an interface/abstract type:
	/// <code>
	/// // Generated proxy for interface - returns InvokerType
	/// [IComparableProxy]
	/// interface IComparable { ... }
	/// 
	/// [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
	/// sealed class IComparableProxy : JavaPeerProxy
	/// {
	///     public override Type? InvokerType => typeof(IComparableInvoker);
	///     public override IJavaPeerable CreateInstance(IntPtr handle, JniHandleOwnership transfer)
	///         => new IComparableInvoker(handle, transfer);
	///     public override Array CreateArray(int length) => new IComparable[length];
	///     public override Array CreateArray2(int length) => new IComparable[length][];
	/// }
	/// </code>
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public abstract class JavaPeerProxy : Attribute
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		/// <summary>
		/// Creates an instance of the target type using the JNI handle and ownership semantics.
		/// This is used for AOT-safe instance creation without reflection.
		/// </summary>
		/// <param name="handle">The JNI object reference handle.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A new instance of the target type wrapping the JNI handle.</returns>
		public abstract IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Creates an array of the target type.
		/// This is used for AOT-safe array creation without using Array.CreateInstance().
		/// </summary>
		/// <param name="length">The length of the array to create.</param>
		/// <param name="rank">The array rank: 1 for T[], 2 for T[][].</param>
		/// <returns>A new array of the target type.</returns>
		public abstract Array CreateArray (int length, int rank);

		/// <summary>
		/// Static helper for AOT-safe array creation. Generated proxies call this method.
		/// </summary>
		/// <typeparam name="T">The element type of the array.</typeparam>
		/// <param name="length">The length of the array to create.</param>
		/// <param name="rank">The array rank: 1 for T[], 2 for T[][].</param>
		/// <returns>A new array of the specified type and rank.</returns>
		protected static Array CreateArrayOf<T> (int length, int rank)
		{
			return rank switch {
				1 => new T[length],
				2 => new T[length][],
				_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1 or 2"),
			};
		}

		/// <summary>
		/// Gets the invoker type for abstract classes and interfaces.
		/// Returns null for concrete types that can be directly instantiated.
		/// </summary>
		/// <remarks>
		/// This property is set in generated proxies for interfaces and abstract classes
		/// to return their corresponding invoker type (e.g., IComparableInvoker for IComparable).
		/// The invoker type is a concrete implementation that can wrap a JNI handle.
		/// </remarks>
		[return: DynamicallyAccessedMembers (Constructors)]
		public Type? InvokerType { get; protected set; }
	}
}
