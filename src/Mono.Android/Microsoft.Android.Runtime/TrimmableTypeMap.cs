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
	// ConcurrentDictionary doesn't accept null values, so misses are cached with s_noPeerSentinel.
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

	internal bool TryGetTargetType (string jniSimpleReference, [NotNullWhen (true)] out Type? type)
	{
		if (!_typeMap.TryGetValue (jniSimpleReference, out var mappedType)) {
			type = null;
			return false;
		}

		// External typemap entries for ACW-backed types resolve to the generated proxy-bearing
		// helper (including alias slots such as "jni/name[1]"). Surface the actual managed peer
		// when a JavaPeerProxy attribute is present so activation and virtual dispatch land on
		// the user's type instead of the generated helper.
		var proxy = mappedType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		type = proxy?.TargetType ?? mappedType;
		return true;
	}

	/// <summary>
	/// Finds the proxy for a managed type using the generated proxy type map.
	/// Results are cached per type.
	/// </summary>
	JavaPeerProxy? GetProxyForManagedType (Type managedType)
	{
		var proxy = _proxyCache.GetOrAdd (managedType, static (type, self) => self.ResolveProxyForManagedType (type) ?? s_noPeerSentinel, this);
		return ReferenceEquals (proxy, s_noPeerSentinel) ? null : proxy;
	}

	JavaPeerProxy? ResolveProxyForManagedType (Type managedType)
	{
		if (!_proxyTypeMap.TryGetValue (managedType, out var proxyType)) {
			return null;
		}

		return proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
	}

	internal bool TryGetJniNameForManagedType (Type managedType, [NotNullWhen (true)] out string? jniName)
	{
		var proxy = GetProxyForManagedType (managedType);
		if (proxy is null) {
			jniName = null;
			return false;
		}

		jniName = proxy.JniName;
		return true;
	}

	JavaPeerProxy? GetProxyForJavaObject (IntPtr handle, Type? targetType = null)
	{
		if (handle == IntPtr.Zero) {
			return null;
		}

		var selfRef = new JniObjectReference (handle);
		var jniClass = JniEnvironment.Types.GetObjectClass (selfRef);

		try {
			while (jniClass.IsValid) {
				var className = JniEnvironment.Types.GetJniTypeNameFromClass (jniClass);
				if (className != null) {
					var proxy = GetProxyForJavaType (className);
					if (proxy != null && (targetType is null || targetType.IsAssignableFrom (proxy.TargetType))) {
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

	JavaPeerProxy? GetProxyForJavaType (string className)
	{
		var proxy = _peerProxyCache.GetOrAdd (className, static (name, self) => self.ResolveProxyForJavaType (name) ?? s_noPeerSentinel, this);
		return ReferenceEquals (proxy, s_noPeerSentinel) ? null : proxy;
	}

	JavaPeerProxy? ResolveProxyForJavaType (string className)
	{
		if (!TryGetTargetType (className, out var managedType)) {
			return null;
		}

		return GetProxyForManagedType (managedType);
	}

	internal IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType = null)
	{
		var proxy = GetProxyForJavaObject (handle, targetType);
		if (proxy is null && targetType is not null) {
			proxy = GetProxyForManagedType (targetType);
			// Verify the Java object is actually assignable to the target Java type
			// before creating the peer. Without this, we'd create invalid peers
			// (e.g., IAppendableInvoker wrapping a java.lang.Integer).
			if (proxy is not null && TryGetJniNameForManagedType (targetType, out var targetJniName)) {
				var selfRef = new JniObjectReference (handle);
				var objClass = default (JniObjectReference);
				var targetClass = default (JniObjectReference);
				try {
					objClass = JniEnvironment.Types.GetObjectClass (selfRef);
					targetClass = JniEnvironment.Types.FindClass (targetJniName);
					if (!JniEnvironment.Types.IsAssignableFrom (objClass, targetClass)) {
						proxy = null;
					}
				} finally {
					JniObjectReference.Dispose (ref objClass);
					JniObjectReference.Dispose (ref targetClass);
				}
			}
		}

		return proxy?.CreateInstance (handle, transfer);
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

	sealed class MissingJavaPeerProxy : JavaPeerProxy
	{
		public MissingJavaPeerProxy () : base ("<missing>", typeof (Java.Lang.Object), null)
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

}
