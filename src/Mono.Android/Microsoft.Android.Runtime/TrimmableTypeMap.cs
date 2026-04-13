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
	readonly IReadOnlyDictionary<Type, Type> _proxyTypeMap;
	readonly ConcurrentDictionary<Type, JavaPeerProxy?> _proxyCache = new ();

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
	/// Finds the proxy for a managed type using the generated proxy type map.
	/// Results are cached per type.
	/// </summary>
	internal JavaPeerProxy? GetProxyForManagedType (Type managedType)
		=> _proxyCache.GetOrAdd (managedType, static (type, self) => self.ResolveProxyForManagedType (type), this);

	JavaPeerProxy? ResolveProxyForManagedType (Type managedType)
	{
		if (!_proxyTypeMap.TryGetValue (managedType, out var proxyType)) {
			return null;
		}

		return proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
	}

	JavaPeerProxy? ResolveProxyForJniName (string jniName)
	{
		if (!_typeMap.TryGetValue (jniName, out var managedType)) {
			return null;
		}

		return GetProxyForManagedType (managedType);
	}

	internal bool TryGetJniNameForManagedType (Type managedType, [NotNullWhen (true)] out string? jniName)
	{
		var proxy = GetProxyForManagedType (managedType);
		if (proxy is not null) {
			jniName = proxy.JniName;
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

		// Look up the proxy via JNI class name → managed type → TypeMapAssociation.
		// We can't use targetType.GetCustomAttribute<JavaPeerProxy>() because the
		// self-application attribute is on the proxy type, not the target type.
		var selfRef = new JniObjectReference (self);
		var jniClass = JniEnvironment.Types.GetObjectClass (selfRef);
		var className = JniEnvironment.Types.GetJniTypeNameFromClass (jniClass);
		JniObjectReference.Dispose (ref jniClass);

		if (className is null) {
			throw new InvalidOperationException (
				$"Failed to create peer for type '{targetType.FullName}' (jniClass='{className}'). " +
				"Ensure the type has a generated proxy in the TypeMap assembly.");
		}

		var proxy = instance.ResolveProxyForJniName (className);
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

			var proxy = s_instance.GetProxyForManagedType (type);
			if (proxy is IAndroidCallableWrapper acw) {
				using var jniType = new JniType (className);
				acw.RegisterNatives (jniType);
			}
		} catch (Exception ex) {
			Environment.FailFast ($"TrimmableTypeMap: Failed to register natives for class '{className}'.", ex);
		}
	}
}
