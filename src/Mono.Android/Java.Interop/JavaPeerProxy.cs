using System;
using System.Diagnostics.CodeAnalysis;

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
	/// [TypeMapProxy("android/app/Activity")]
	/// [AttributeUsage(AttributeTargets.Class, Inherited = false)]
	/// sealed class ActivityProxy : JavaPeerProxy
	/// {
	///     public override Type TargetType => typeof(Activity);
	/// }
	/// </code>
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	abstract class JavaPeerProxy : Attribute
	{
		/// <summary>
		/// Gets the .NET type that this proxy maps to.
		/// The return value has DynamicallyAccessedMembers annotation to preserve constructor metadata for AOT scenarios.
		/// </summary>
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		public abstract Type TargetType { get; }

		/// <summary>
		/// Gets a function pointer for a marshal method at the specified index.
		/// This is used to resolve [UnmanagedCallersOnly] method pointers for JNI callbacks.
		/// </summary>
		/// <param name="methodIndex">The index of the marshal method within this type's method table.</param>
		/// <returns>A function pointer to the UCO method, or <see cref="IntPtr.Zero"/> if the index is invalid.</returns>
		public abstract IntPtr GetFunctionPointer (int methodIndex);
	}
}
