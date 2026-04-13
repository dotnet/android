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
	static TrimmableTypeMap? s_instance;

	internal static TrimmableTypeMap Instance =>
		s_instance ?? throw new InvalidOperationException (
			"TrimmableTypeMap has not been initialized. Ensure RuntimeFeature.TrimmableTypeMap is enabled and the JNI runtime is initialized.");

	readonly IReadOnlyDictionary<string, Type> _typeMap;
	readonly ConcurrentDictionary<Type, JavaPeerProxy?> _proxyCache = new ();

	TrimmableTypeMap ()
	{
		_typeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
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

	/// <summary>
	/// Registers the <c>mono.android.Runtime.registerNatives</c> JNI native method.
	/// </summary>
	unsafe void RegisterNatives ()
	{
		using var runtimeClass = new JniType ("mono/android/Runtime"u8);
		fixed (byte* name = "registerNatives"u8, sig = "(Ljava/lang/Class;)V"u8) {
			var onRegisterNatives = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnRegisterNatives;
			var method = new JniNativeMethod (name, sig, onRegisterNatives);
			JniEnvironment.Types.RegisterNatives (runtimeClass.PeerReference, [method]);
		}
	}

	internal bool TryGetType (string jniSimpleReference, [NotNullWhen (true)] out Type? type)
		=> _typeMap.TryGetValue (jniSimpleReference, out type);

	/// <summary>
	/// Finds the proxy for a managed type by resolving its JNI name (from [Register] or
	/// [JniTypeSignature] attributes) and looking it up in the TypeMap dictionary.
	/// Results are cached per type.
	/// </summary>
	internal JavaPeerProxy? GetProxyForManagedType (Type managedType)
	{
		return _proxyCache.GetOrAdd (managedType, static (type, self) => {
			for (var currentType = type; currentType is not null; currentType = currentType.BaseType) {
				// First check if the type itself IS a proxy (has self-applied attribute)
				var direct = currentType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
				if (direct is not null) {
					return direct;
				}

				// Managed-only Java subclasses such as JavaProxyThrowable do not have their own
				// [Register] attribute, so fall back to the nearest Java base type with one.
				if (!TryGetJniNameForType (currentType, out var jniName)) {
					continue;
				}

				// Look up the proxy type in the TypeMap dictionary
				if (!self._typeMap.TryGetValue (jniName, out var proxyType)) {
					continue;
				}

				var proxy = proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
				if (proxy is not null) {
					return proxy;
				}
			}

			return null;
		}, this);
	}

	/// <summary>
	/// Resolves a managed type's JNI name from its <see cref="IJniNameProviderAttribute"/>
	/// (implemented by both <c>[Register]</c> and <c>[JniTypeSignature]</c>).
	/// </summary>
	internal static bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
	{
		if (type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false) is [IJniNameProviderAttribute provider, ..]
			&& !string.IsNullOrEmpty (provider.Name)) {
			jniName = provider.Name.Replace ('.', '/');
			return true;
		}

		jniName = null;
		return false;
	}

	/// <summary>
	/// Creates a peer instance using the proxy's CreateInstance method.
	/// Given a managed type, resolves the JNI name, finds the proxy, and calls CreateInstance.
	/// </summary>
	internal bool TryCreatePeer (Type type, IntPtr handle, JniHandleOwnership transfer)
	{
		var proxy = GetProxyForManagedType (type);
		if (proxy is null) {
			return false;
		}

		return proxy.CreateInstance (handle, transfer) != null;
	}

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

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

	/// <summary>
	/// Creates a managed peer instance for a Java object being constructed.
	/// Called from generated UCO constructor wrappers (nctor_*_uco).
	/// </summary>
	internal static void ActivateInstance (IntPtr self, Type targetType)
	{
		var instance = s_instance;
		if (instance is null) {
			throw new InvalidOperationException ("TrimmableTypeMap has not been initialized.");
		}

		// Look up the proxy via JNI class name → TypeMap dictionary.
		// We can't use targetType.GetCustomAttribute<JavaPeerProxy>() because the
		// self-application attribute is on the proxy type, not the target type.
		var selfRef = new JniObjectReference (self);
		var jniClass = JniEnvironment.Types.GetObjectClass (selfRef);
		var className = JniEnvironment.Types.GetJniTypeNameFromClass (jniClass);
		JniObjectReference.Dispose (ref jniClass);

		if (className is null || !instance._typeMap.TryGetValue (className, out var proxyType)) {
			throw new InvalidOperationException (
				$"Failed to create peer for type '{targetType.FullName}' (jniClass='{className}'). " +
				"Ensure the type has a generated proxy in the TypeMap assembly.");
		}

		var proxy = proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		if (proxy is null || proxy.CreateInstance (self, JniHandleOwnership.DoNotTransfer) is null) {
			throw new InvalidOperationException (
				$"Failed to create peer for type '{targetType.FullName}'. " +
				"Ensure the type has a generated proxy in the TypeMap assembly.");
		}
	}

	[UnmanagedCallersOnly]
	static void OnRegisterNatives (IntPtr jnienv, IntPtr klass, IntPtr nativeClassHandle)
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
				using var jniType = new JniType (className);
				acw.RegisterNatives (jniType);
			}
		} catch (Exception ex) {
			Environment.FailFast ($"TrimmableTypeMap: Failed to register natives for class '{className}'.", ex);
		}
	}
}
