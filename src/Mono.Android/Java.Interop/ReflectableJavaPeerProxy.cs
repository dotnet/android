using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// This is an idea for a peer proxy which does not need generating proxies for IL and is still reflection and AOT-safe.
	/// TODO: explore if this would be a viable option for "size" optimized builds.
	/// </summary>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public sealed class ReflectableJavaPeerProxy<[DynamicallyAccessedMembers(Constructors | Methods)] TTarget> : Attribute
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;

		/// <summary>
		/// Gets the target type that this proxy creates instances of.
		/// </summary>
		/// <remarks>
		/// This property is set in generated proxies to return the .NET type that wraps the Java class.
		/// For example, a proxy for android/content/Context would return typeof(Android.Content.Context).
		/// </remarks>
		[DynamicallyAccessedMembers (Constructors | Methods)]
		public required Type TargetType => typeof(TTarget);

		/// <summary>
		/// Gets the invoker type for abstract classes and interfaces.
		/// Returns null for concrete types that can be directly instantiated.
		/// </summary>
		/// <remarks>
		/// This property is set in generated proxies for interfaces and abstract classes
		/// to return their corresponding invoker type (e.g., IComparableInvoker for IComparable).
		/// The invoker type is a concrete implementation that can wrap a JNI handle.
		/// </remarks>
		[DynamicallyAccessedMembers (Constructors)]
		public Type? InvokerType { get; init; }

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
		/// var factory = proxy.GetDerivedTypeFactory();
		/// var array = factory.CreateArray(10, 1);           // T[]
		/// var list = factory.CreateListFromHandle(handle, transfer);  // IList
		/// </code>
		/// </remarks>
		/// <returns>A factory for creating derived types of the target type.</returns>
		public DerivedTypeFactory GetDerivedTypeFactory () => new DerivedTypeFactory<TTarget> ();
	}
}
