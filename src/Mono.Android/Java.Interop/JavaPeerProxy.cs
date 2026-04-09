#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Java.Interop
{
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
		string? jniName;
		Type? targetType;
		Type? invokerType;

		protected JavaPeerProxy ()
		{
		}

		protected JavaPeerProxy (
			string jniName,
			Type targetType,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type? invokerType)
		{
			this.jniName = jniName ?? throw new ArgumentNullException (nameof (jniName));
			this.targetType = targetType ?? throw new ArgumentNullException (nameof (targetType));
			this.invokerType = invokerType;
		}

		protected JavaPeerProxy (
			Type targetType,
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			Type? invokerType)
		{
			this.targetType = targetType ?? throw new ArgumentNullException (nameof (targetType));
			this.invokerType = invokerType;
		}

		/// <summary>
		/// Gets the final JNI type name of the Java class this proxy represents.
		/// </summary>
		public virtual string JniName => jniName ?? GetJniName (TargetType);

		static string GetJniName (Type targetType)
		{
			if (targetType.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false) is [IJniNameProviderAttribute provider, ..]
				&& !string.IsNullOrEmpty (provider.Name)) {
				return provider.Name.Replace ('.', '/');
			}

			throw new InvalidOperationException (
				$"No JNI name is available for proxy target type '{targetType.FullName}'. " +
				$"Use the JavaPeerProxy(string jniName, Type targetType, Type? invokerType) constructor " +
				$"or apply an {nameof (IJniNameProviderAttribute)}.");
		}

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
		public virtual Type TargetType => targetType ?? throw new InvalidOperationException (
			$"{GetType ().FullName} did not provide a target type.");

		/// <summary>
		/// Gets the invoker type for interfaces and abstract classes.
		/// Returns null for concrete types that can be directly instantiated.
		/// </summary>
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		public virtual Type? InvokerType => invokerType;

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
		protected JavaPeerProxy ()
			: base (typeof (T), invokerType: null)
		{
		}

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
