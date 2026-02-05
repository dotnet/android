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
	///     public override JavaPeerContainerFactory GetJavaPeerContainerFactory() => JavaPeerContainerFactory.Create&lt;Activity&gt;();
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
	///     public override JavaPeerContainerFactory GetJavaPeerContainerFactory() => JavaPeerContainerFactory.Create&lt;IComparable&gt;();
	/// }
	/// </code>
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public abstract class JavaPeerProxy : Attribute
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		/// <summary>
		/// Gets the target type that this proxy creates instances of.
		/// </summary>
		/// <remarks>
		/// This property is set in generated proxies to return the .NET type that wraps the Java class.
		/// For example, a proxy for android/content/Context would return typeof(Android.Content.Context).
		/// </remarks>
		public virtual Type TargetType { get; set; } = null!;

		/// <summary>
		/// Creates an instance of the target type using the JNI handle and ownership semantics.
		/// This is used for AOT-safe instance creation without reflection.
		/// </summary>
		/// <param name="handle">The JNI object reference handle.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A new instance of the target type wrapping the JNI handle.</returns>
		public abstract IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer);

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

		/// <summary>
		/// Creates an instance of the invoker type using the JNI handle and ownership semantics.
		/// This is used for abstract classes and interfaces where the target type cannot be
		/// instantiated directly. Returns null if no invoker type is defined.
		/// </summary>
		/// <param name="handle">The JNI object reference handle.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A new instance of the invoker type wrapping the JNI handle, or null if no invoker.</returns>
		[Obsolete ("Use CreateInstance instead - it automatically creates the invoker for abstract/interface types")]
		public virtual IJavaPeerable? CreateInvokerInstance (IntPtr handle, JniHandleOwnership transfer) => null;

		/// <summary>
		/// Gets a factory for creating derived types (arrays, collections) of the target type.
		/// This enables AOT-safe creation of generic collections like <c>IList&lt;T&gt;</c> without reflection.
		/// </summary>
		/// <remarks>
		/// The factory is typed to the target type and can create:
		/// - Arrays: T[], T[][], T[][][]
		/// - Lists: JavaList&lt;T&gt;
		/// - Collections: JavaCollection&lt;T&gt;
		/// - Sets: JavaSet&lt;T&gt;
		/// - Dictionaries: JavaDictionary&lt;TKey, T&gt; (with a key factory)
		/// 
		/// Example usage in TypeMap:
		/// <code>
		/// var proxy = typeMap.GetProxyForType(typeof(View));
		/// var factory = proxy.GetJavaPeerContainerFactory();
		/// var array = factory.CreateArray(10, 1);           // T[]
		/// var list = factory.CreateListFromHandle(handle, transfer);  // IList
		/// </code>
		/// </remarks>
		/// <returns>A factory for creating derived types of the target type.</returns>
		public abstract JavaPeerContainerFactory GetJavaPeerContainerFactory ();
	}
}
