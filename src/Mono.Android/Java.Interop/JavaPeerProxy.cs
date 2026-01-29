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
	/// Example generated proxy:
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
	/// }
	/// </code>
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public abstract class JavaPeerProxy : Attribute
	{
		/// <summary>
		/// Creates an instance of the target type using the JNI handle and ownership semantics.
		/// This is used for AOT-safe instance creation without reflection.
		/// </summary>
		/// <param name="handle">The JNI object reference handle.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A new instance of the target type wrapping the JNI handle.</returns>
		public abstract IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer);
	}

	/// <summary>
	/// Attribute applied to abstract classes and interfaces to provide AOT-safe access to their invoker types.
	/// The invoker type is a concrete implementation that can wrap a JNI handle for an abstract/interface type.
	/// </summary>
	/// <typeparam name="TInvoker">The invoker type (e.g., IComparableInvoker for IComparable).</typeparam>
	/// <remarks>
	/// This attribute is generated at build time and applied to abstract classes and interfaces.
	/// It provides a trim-safe and AOT-safe way to get the invoker type without runtime reflection.
	/// 
	/// Example:
	/// <code>
	/// [JavaPeerProxyWithInvoker&lt;IComparableInvoker&gt;]
	/// public interface IComparable { ... }
	/// </code>
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public sealed class JavaPeerProxyWithInvokerAttribute<
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		TInvoker
	> : Attribute
		where TInvoker : IJavaPeerable
	{
		/// <summary>
		/// Gets the invoker type for the abstract class or interface.
		/// </summary>
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		public Type InvokerType => typeof (TInvoker);
	}
}
