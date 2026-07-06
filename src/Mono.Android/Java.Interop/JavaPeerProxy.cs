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
		protected JavaPeerProxy (string jniName, Type targetType)
		{
			ArgumentNullException.ThrowIfNull (jniName);
			ArgumentNullException.ThrowIfNull (targetType);

			JniName = jniName;
			TargetType = targetType;
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
		/// Gets a factory for creating containers (arrays, collections) of the target type.
		/// Enables AOT-safe creation of generic collections without <c>MakeGenericType()</c>.
		/// </summary>
		/// <returns>A factory for creating containers of the target type, or null if not supported.</returns>
		public virtual JavaPeerContainerFactory? GetContainerFactory () => null;

		/// <summary>
		/// Returns <see langword="true"/> when the UCO constructor callback should skip
		/// activation because a managed peer already exists for the given JNI handle
		/// (e.g., when called from <c>FinishCreateInstance</c> after <c>StartCreateInstance</c>
		/// already registered the peer).
		/// </summary>
		public static bool ShouldSkipActivation (IntPtr jniSelf)
		{
			var reference = new JniObjectReference (jniSelf, JniObjectReferenceType.Invalid);
			var peer = JniEnvironment.Runtime.ValueManager.PeekPeer (reference);
			if (peer != null && !IsActivationPeer (peer)) {
				return true;
			}
			return JniEnvironment.WithinNewObjectScope;
		}

		public static IJavaPeerable? GetActivationPeer (IntPtr jniSelf)
		{
			var reference = new JniObjectReference (jniSelf, JniObjectReferenceType.Invalid);
			var peer = JniEnvironment.Runtime.ValueManager.PeekPeer (reference);
			return peer != null && IsActivationPeer (peer) ? peer : null;
		}

		public static void SetActivationPeerReference (IJavaPeerable peer, IntPtr jniSelf)
		{
			var reference = new JniObjectReference (jniSelf, JniObjectReferenceType.Invalid);
			peer.SetPeerReference (reference);
			peer.SetJniIdentityHashCode (JniEnvironment.References.GetIdentityHashCode (reference));
		}

		public static void MarkActivationPeerReplaceable (IntPtr jniSelf)
		{
			var reference = new JniObjectReference (jniSelf, JniObjectReferenceType.Invalid);
			var peer = JniEnvironment.Runtime.ValueManager.PeekPeer (reference);
			if (peer == null) {
				return;
			}

			peer.SetJniManagedPeerState (peer.JniManagedPeerState | JniManagedPeerStates.Replaceable);
		}

		static bool IsActivationPeer (IJavaPeerable peer)
		{
			var state = peer.JniManagedPeerState;
			return (state & JniManagedPeerStates.Activatable) == JniManagedPeerStates.Activatable
				|| (state & JniManagedPeerStates.Replaceable) == JniManagedPeerStates.Replaceable;
		}
	}

	/// <summary>
	/// Generic base for generated proxy types. Provides <see cref="JavaPeerProxy.TargetType"/>
	/// and <see cref="JavaPeerProxy.GetContainerFactory"/> automatically from the type parameter.
	/// </summary>
	/// <typeparam name="T">The target .NET peer type this proxy represents.</typeparam>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public abstract class JavaPeerProxy<[DynamicallyAccessedMembers (Constructors)] T>
		: JavaPeerProxy where T : class, IJavaPeerable
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		protected JavaPeerProxy (string jniName)
			: base (jniName, typeof (T))
		{
		}

		public override JavaPeerContainerFactory? GetContainerFactory ()
			=> JavaPeerContainerFactory<T>.Instance;
	}

	/// <summary>
	/// Base attribute class for generated array-proxy types that enable AOT-safe construction
	/// of managed arrays for a specific element type and rank, without
	/// <see cref="Array.CreateInstance(Type, int)"/> or other reflection-based allocation.
	/// </summary>
	/// <remarks>
	/// Like <see cref="JavaPeerProxy"/>, each generated array proxy is applied to its own holder
	/// type (self-application pattern), so the runtime can retrieve it via
	/// <c>GetCustomAttribute&lt;JavaArrayProxy&gt;()</c> and invoke it without
	/// <c>Activator.CreateInstance()</c>. The per-rank TypeMap groups
	/// (<c>__ArrayMapRank{N}</c>) map a JNI name to the holder type carrying this attribute;
	/// <c>TrimmableTypeMap.TryGetArrayProxy</c> resolves the holder and returns the attribute.
	/// </remarks>
	[AttributeUsage (AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public abstract class JavaArrayProxy : Attribute
	{
		/// <summary>
		/// Gets the .NET array and wrapper types associated with this proxy (for example
		/// <c>T[]</c>, <c>JavaArray&lt;T&gt;</c>, and the matching <c>JavaObjectArray&lt;T&gt;</c> /
		/// <c>JavaPrimitiveArray&lt;T&gt;</c> wrappers). Emitting these <see cref="Type"/> tokens
		/// roots the types so the trimmer/ILC keeps them available for marshaling.
		/// </summary>
		/// <returns>The array and wrapper types handled by this proxy.</returns>
		public abstract Type[] GetArrayTypes ();

		/// <summary>
		/// Creates a new managed array of this proxy's element type and rank using a rooted
		/// <c>newarr</c>, which is AOT-safe unlike <see cref="Array.CreateInstance(Type, int)"/>.
		/// </summary>
		/// <param name="length">The length of the outermost array dimension.</param>
		/// <returns>A new array of the proxy's element type with the requested length.</returns>
		public abstract Array CreateManagedArray (int length);
	}
}
