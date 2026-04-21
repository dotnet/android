#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// Attribute applied to generated alias holder types. When multiple .NET types
	/// map to the same JNI name (e.g., <c>JavaCollection</c> and <c>JavaCollection&lt;T&gt;</c>
	/// both map to <c>"java/util/Collection"</c>), the base JNI name entry points to
	/// a plain holder class annotated with this attribute, which lists the indexed
	/// TypeMap keys for each alias type.
	/// </summary>
	/// <remarks>
	/// The alias holder is NOT a <see cref="JavaPeerProxy"/> subclass — this ensures
	/// <c>GetCustomAttribute&lt;JavaPeerProxy&gt;()</c> returns null for alias entries,
	/// keeping the fast path (non-alias types) free of alias checks.
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class JavaPeerAliasesAttribute : Attribute
	{
		/// <summary>
		/// Gets the indexed TypeMap keys for this alias group (e.g., <c>"java/util/Collection[0]"</c>,
		/// <c>"java/util/Collection[1]"</c>).
		/// </summary>
		public string[] Aliases { get; }

		public JavaPeerAliasesAttribute (params string[] aliases) => Aliases = aliases;
	}

	/// <summary>
	/// Base attribute class for generated proxy types that enable AOT-safe type mapping
	/// between Java and .NET types.
	/// </summary>
	/// <remarks>
	/// Proxy attributes are generated at build time and applied to the proxy type itself
	/// (self-application pattern). The .NET runtime's <c>GetCustomAttribute&lt;JavaPeerProxy&gt;()</c>
	/// instantiates the proxy in an AOT-safe manner — no <c>Activator.CreateInstance()</c> needed.
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public abstract class JavaPeerProxy : Attribute
	{
		protected JavaPeerProxy (
			string jniName,
			Type targetType,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type? invokerType)
		{
			JniName = jniName ?? throw new ArgumentNullException (nameof (jniName));
			TargetType = targetType ?? throw new ArgumentNullException (nameof (targetType));
			InvokerType = invokerType;
		}

		/// <summary>
		/// Gets the final JNI type name of the Java class this proxy represents.
		/// </summary>
		public string JniName { get; }

		/// <summary>
		/// Creates an instance of the target type using the JNI handle and ownership semantics.
		/// This replaces the reflection-based constructor invocation used in the legacy path.
		/// </summary>
		/// <param name="handle">The JNI object reference handle.</param>
		/// <param name="transfer">How to handle JNI reference ownership.</param>
		/// <returns>A new instance of the target type wrapping the JNI handle, or null if activation is not supported.</returns>
		public abstract IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer);

		/// <summary>
		/// Gets the target .NET type that this proxy represents.
		/// </summary>
		public Type TargetType { get; }

		/// <summary>
		/// Gets the invoker type for interfaces and abstract classes.
		/// Returns null for concrete types that can be directly instantiated.
		/// </summary>
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		public Type? InvokerType { get; }

		/// <summary>
		/// Gets a factory for creating containers (arrays, collections) of the target type.
		/// Enables AOT-safe creation of generic collections without <c>MakeGenericType()</c>.
		/// </summary>
		/// <returns>A factory for creating containers of the target type, or null if not supported.</returns>
		public virtual JavaPeerContainerFactory? GetContainerFactory () => null;
	}

	/// <summary>
	/// Generic base for generated proxy types. Provides <see cref="JavaPeerProxy.TargetType"/>
	/// and <see cref="JavaPeerProxy.GetContainerFactory"/> automatically from the type parameter.
	/// </summary>
	/// <typeparam name="T">The target .NET peer type this proxy represents.</typeparam>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public abstract class JavaPeerProxy<
		// TODO (https://github.com/dotnet/android/issues/10794): Remove this DAM annotation
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		T
	> : JavaPeerProxy where T : class, IJavaPeerable
	{
		protected JavaPeerProxy (
			string jniName,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type? invokerType) : base (jniName, typeof (T), invokerType)
		{
		}

		public override JavaPeerContainerFactory GetContainerFactory ()
			=> JavaPeerContainerFactory<T>.Instance;
	}
}
