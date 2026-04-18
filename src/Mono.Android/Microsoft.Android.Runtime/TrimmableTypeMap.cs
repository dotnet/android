#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Central type map for the trimmable typemap path. Owns the TypeMapping dictionary
/// and provides peer creation, invoker resolution, container factories, and native
/// method registration. All proxy attribute access is encapsulated here.
/// </summary>
class TrimmableTypeMap
{
	static readonly Lock s_initLock = new ();
	static readonly JavaPeerProxy s_noPeerSentinel = new MissingJavaPeerProxy ();
	static TrimmableTypeMap? s_instance;

	internal static TrimmableTypeMap Instance =>
		s_instance ?? throw new InvalidOperationException (
			"TrimmableTypeMap has not been initialized. Ensure RuntimeFeature.TrimmableTypeMap is enabled and the JNI runtime is initialized.");

	readonly IReadOnlyDictionary<string, Type> _typeMap;
	readonly IReadOnlyDictionary<Type, Type> _proxyTypeMap;
	readonly ConcurrentDictionary<Type, JavaPeerProxy> _proxyCache = new ();
	readonly ConcurrentDictionary<string, JavaPeerProxy> _peerProxyCache = new (StringComparer.Ordinal);

	TrimmableTypeMap ()
	{
		_typeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
		_proxyTypeMap = TypeMapping.GetOrCreateProxyTypeMapping<Java.Lang.Object> ();
	}

	/// <summary>
	/// Initializes the singleton instance and registers the bootstrap JNI native method.
	/// Must be called after the JNI runtime is initialized and before any JCW class is loaded.
	/// </summary>
	internal static void Initialize ()
	{
		if (s_instance is not null)
			return;

		lock (s_initLock) {
			if (s_instance is not null)
				return;

			var instance = new TrimmableTypeMap ();
			instance.RegisterNatives ();
			s_instance = instance;
		}
	}

	unsafe void RegisterNatives ()
	{
		// Use the `string` overload of `JniType` deliberately. Its underlying
		// `JniEnvironment.Types.TryFindClass(string, bool)` tries raw JNI `FindClass`
		// first and, if that fails, falls back to `Class.forName(name, true, info.Runtime.ClassLoader)`,
		// which resolves via the runtime's app ClassLoader — the same one that loads
		// `mono.android.Runtime` from the APK.
		// The `ReadOnlySpan<byte>` overload (see external/Java.Interop/src/Java.Interop/Java.Interop/JniEnvironment.Types.cs)
		// only calls raw JNI `FindClass`, which resolves via the system ClassLoader on
		// Android and returns a different `Class` instance from the one JCWs reference.
		// Registering natives on that other instance is silently wrong.
		using var runtimeClass = new JniType ("mono/android/Runtime");
		fixed (byte* name = "registerNatives"u8, sig = "(Ljava/lang/Class;)V"u8) {
			var onRegisterNatives = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnRegisterNatives;
			var method = new JniNativeMethod (name, sig, onRegisterNatives);
			JniEnvironment.Types.RegisterNatives (runtimeClass.PeerReference, [method]);
		}
	}

	internal bool TryGetTargetType (string jniSimpleReference, [NotNullWhen (true)] out Type? type)
	{
		type = GetProxyForJavaType (jniSimpleReference)?.TargetType;
		return type is not null;
	}

	/// <summary>
	/// Resolves the <see cref="JavaPeerProxy"/> for a managed type via the CLR
	/// <c>TypeMapping</c> proxy dictionary.
	/// </summary>
	/// <remarks>
	/// The generator emits exactly one <c>TypeMapAssociation</c> per generic peer,
	/// keyed by the open generic definition (Java erases generics, so one proxy
	/// fits every closed instantiation). Closed instantiations are normalised to
	/// their generic type definition before the lookup because the CLR lazy
	/// dictionary does identity-based key matching
	/// (see <c>dotnet/runtime</c> <c>TypeMapLazyDictionary.cs</c>).
	/// <see cref="Type.GetGenericTypeDefinition"/> is safe under full AOT + trim
	/// (it is not <c>RequiresDynamicCode</c>). Java→managed construction of a
	/// closed generic peer still requires a closed <see cref="Type"/> at the call
	/// site and is tracked separately.
	/// </remarks>
	JavaPeerProxy? GetProxyForManagedType (Type managedType)
	{
		if (managedType.IsGenericType && !managedType.IsGenericTypeDefinition) {
			managedType = managedType.GetGenericTypeDefinition ();
		}

		var proxy = _proxyCache.GetOrAdd (managedType, static (type, self) => {
			if (self._proxyTypeMap.TryGetValue (type, out var proxyType)) {
				return proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false) ?? s_noPeerSentinel;
			}

			return s_noPeerSentinel;
		}, this);
		return ReferenceEquals (proxy, s_noPeerSentinel) ? null : proxy;
	}

	JavaPeerProxy? GetProxyForJavaType (string className)
	{
		var proxy = _peerProxyCache.GetOrAdd (className, static (name, self) => {
			if (!self._typeMap.TryGetValue (name, out var mappedType)) {
				return s_noPeerSentinel;
			}

			var proxy = mappedType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			if (proxy is null) {
				// Alias typemap entries (for example "jni/name[1]") are not implemented yet.
				// Support for them will be added in a follow-up for https://github.com/dotnet/android/issues/10788.
				throw new NotImplementedException (
					$"Trimmable typemap alias handling is not implemented yet for '{name}'.");
			}

			return proxy;
		}, this);
		return ReferenceEquals (proxy, s_noPeerSentinel) ? null : proxy;
	}

	internal bool TryGetJniNameForManagedType (Type managedType, [NotNullWhen (true)] out string? jniName)
	{
		jniName = GetProxyForManagedType (managedType)?.JniName;
		return jniName is not null;
	}

	internal JavaPeerProxy? GetProxyForJavaObject (IntPtr handle, Type? targetType = null)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		return TryGetProxyFromHierarchy (this, handle, targetType) ??
			TryGetProxyFromTargetType (this, handle, targetType);

		static JavaPeerProxy? TryGetProxyFromHierarchy (TrimmableTypeMap self, IntPtr handle, Type? targetType)
		{
			var selfRef = new JniObjectReference (handle);
			var jniClass = JniEnvironment.Types.GetObjectClass (selfRef);

			try {
				while (jniClass.IsValid) {
					var className = JniEnvironment.Types.GetJniTypeNameFromClass (jniClass);
					if (className != null) {
						var proxy = self.GetProxyForJavaType (className);
						if (proxy != null && (targetType is null || TargetTypeMatches (targetType, proxy.TargetType))) {
							return proxy;
						}
					}

					var super = JniEnvironment.Types.GetSuperclass (jniClass);
					JniObjectReference.Dispose (ref jniClass);
					jniClass = super;
				}
			} finally {
				JniObjectReference.Dispose (ref jniClass);
			}

			return null;
		}

		static JavaPeerProxy? TryGetProxyFromTargetType (TrimmableTypeMap self, IntPtr handle, Type? targetType)
		{
			if (targetType is null) {
				return null;
			}

			var proxy = self.GetProxyForManagedType (targetType);
			// Verify the Java object is actually assignable to the target Java type
			// before returning the fallback proxy. Without this, we'd create invalid peers
			// (e.g., IAppendableInvoker wrapping a java.lang.Integer).
			if (proxy is null || !self.TryGetJniNameForManagedType (targetType, out var targetJniName)) {
				return null;
			}

			var selfRef = new JniObjectReference (handle);
			var objClass = default (JniObjectReference);
			var targetClass = default (JniObjectReference);
			try {
				objClass = JniEnvironment.Types.GetObjectClass (selfRef);
				targetClass = JniEnvironment.Types.FindClass (targetJniName);
				return JniEnvironment.Types.IsAssignableFrom (objClass, targetClass) ? proxy : null;
			} finally {
				JniObjectReference.Dispose (ref objClass);
				JniObjectReference.Dispose (ref targetClass);
			}
		}
	}

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	/// <summary>
	/// Match the proxy's stored target type against a hint from the caller.
	/// The proxy's target type is the open generic definition for generic peers
	/// (Java erases generics, so one proxy fits every closed instantiation),
	/// so a plain <see cref="Type.IsAssignableFrom"/> check misses when the hint
	/// is a closed instantiation. Walk the hint's base chain to find a generic
	/// type whose definition equals the proxy's open target type. This covers
	/// closed subclasses of an open generic class peer.
	/// </summary>
	/// <remarks>
	/// Implementers of an open generic <em>interface</em> peer are intentionally
	/// not matched here: <see cref="TryGetProxyFromHierarchy"/> walks only the
	/// JNI class chain (<c>getSuperclass</c>), never JNI interfaces, so the
	/// proxy returned from that walk is always a class peer. Matching on
	/// <c>Type.GetInterfaces()</c> would also force a trimmer
	/// <c>DynamicallyAccessedMembers(Interfaces)</c> annotation up the chain
	/// (ultimately into Java.Interop's <c>CreatePeer</c> API). If we ever need
	/// to discover interface peers, the generator should emit an explicit
	/// implementer→interface map so runtime can avoid reflection over
	/// interface lists.
	/// </remarks>
	internal static bool TargetTypeMatches (Type targetType, Type proxyTargetType)
	{
		if (targetType.IsAssignableFrom (proxyTargetType)) {
			return true;
		}

		if (!proxyTargetType.IsGenericTypeDefinition) {
			return false;
		}

		for (Type? t = targetType; t is not null; t = t.BaseType) {
			if (t.IsGenericType && !t.IsGenericTypeDefinition &&
					t.GetGenericTypeDefinition () == proxyTargetType) {
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets the invoker type for an interface or abstract class from the proxy attribute.
	/// </summary>
	[return: DynamicallyAccessedMembers (Constructors)]
	internal Type? GetInvokerType (Type type)
	{
		return GetProxyForManagedType (type)?.InvokerType;
	}

	/// <summary>
	/// Gets the container factory for a type from its proxy attribute.
	/// Used for AOT-safe array/collection/dictionary creation.
	/// </summary>
	internal JavaPeerContainerFactory? GetContainerFactory (Type type)
	{
		return GetProxyForManagedType (type)?.GetContainerFactory ();
	}

	[UnmanagedCallersOnly]
	internal static void OnRegisterNatives (IntPtr jnienv, IntPtr klass, IntPtr nativeClassHandle)
	{
		string? className = null;
		try {
			if (s_instance is null) {
				return;
			}

			var classRef = new JniObjectReference (nativeClassHandle);
			className = JniEnvironment.Types.GetJniTypeNameFromClass (classRef);
			if (className is null) {
				return;
			}

			if (!s_instance._typeMap.TryGetValue (className, out var type)) {
				return;
			}

			var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			if (proxy is IAndroidCallableWrapper acw) {
				// Use the class reference passed from Java (via C++) — not JniType(className)
				// which resolves via FindClass and may get a different class from a different ClassLoader.
				using var jniType = new JniType (ref classRef, JniObjectReferenceOptions.Copy);
				acw.RegisterNatives (jniType);
			}
		} catch (Exception ex) {
			Environment.FailFast ($"TrimmableTypeMap: Failed to register natives for class '{className}'.", ex);
		}
	}

	sealed class MissingJavaPeerProxy : JavaPeerProxy
	{
		public MissingJavaPeerProxy () : base ("<missing>", typeof (Java.Lang.Object), null)
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

}
