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
		// Use the string overload of JniType which resolves via Class.forName with the
		// runtime's ClassLoader. The UTF-8 span overload uses raw JNI FindClass which
		// resolves via the system ClassLoader — a different class instance than the one
		// JCWs reference via the app ClassLoader.
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

	JavaPeerProxy? GetProxyForManagedType (Type managedType)
	{
		var proxy = _proxyCache.GetOrAdd (managedType, static (type, self) => {
			if (!self._proxyTypeMap.TryGetValue (type, out var proxyType)) {
				return s_noPeerSentinel;
			}

			return proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false) ?? s_noPeerSentinel;
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
				// Use the class reference passed from Java (via C++) — not JniType(className)
				// which resolves via FindClass and may get a different class from a different ClassLoader.
				var jniType = new JniType (ref classRef, JniObjectReferenceOptions.Copy);
				try {
					acw.RegisterNatives (jniType);
				} finally {
					jniType.Dispose ();
				}
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
