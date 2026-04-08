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
using Java.Interop.Tools.TypeNameMappings;

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
	readonly ConcurrentDictionary<Type, string?> _jniNameCache = new ();
	readonly ConcurrentDictionary<string, JavaPeerProxy?> _peerProxyCache = new (StringComparer.Ordinal);

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
	{
		if (!_typeMap.TryGetValue (jniSimpleReference, out var mappedType)) {
			type = null;
			return false;
		}

		// External typemap entries point at the generated proxy for ACW-backed types.
		// The JniTypeManager, however, must surface the real managed peer type so
		// Java object activation and virtual dispatch resolve to the user's override
		// instead of the bound Android base type.
		var proxy = mappedType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		if (proxy is null) {
			type = null;
			return false;
		}
		type = proxy.TargetType;
		return true;
	}

	/// <summary>
	/// Finds the proxy for a managed type by resolving its JNI name (from [Register] or
	/// [JniTypeSignature] attributes) and looking it up in the TypeMap dictionary.
	/// Results are cached per type.
	/// </summary>
	internal JavaPeerProxy? GetProxyForManagedType (Type managedType)
	{
		return _proxyCache.GetOrAdd (managedType, static (type, self) => {
			// First check if the type itself IS a proxy (has self-applied attribute)
			var direct = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			if (direct is not null) {
				return direct;
			}

			// Resolve the JNI name from attributes first, then fall back to the
			// generated TypeMap entries for ACW/component types which don't carry
			// [Register]/[JniTypeSignature] themselves.
			if (!self.TryGetJniName (type, out var jniName)) {
				return null;
			}

			// Look up the proxy type in the TypeMap dictionary
			if (!self._typeMap.TryGetValue (jniName, out var proxyType)) {
				return null;
			}

			return proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		}, this);
	}

	internal bool TryGetJniName (Type type, [NotNullWhen (true)] out string? jniName)
	{
		if (_jniNameCache.TryGetValue (type, out jniName)) {
			return jniName != null;
		}

		if (TryGetJniNameForType (type, out jniName)) {
			_jniNameCache [type] = jniName;
			return true;
		}

		if (TryGetCompatJniNameForAndroidComponent (type, out jniName)) {
			_jniNameCache [type] = jniName;
			return true;
		}

		// Prefer the JavaNativeTypeManager calculation for user/application types,
		// as it matches the ACW generation rules used during the build.
		if (typeof (IJavaPeerable).IsAssignableFrom (type)) {
			jniName = JavaNativeTypeManager.ToJniName (type);
			if (!string.IsNullOrEmpty (jniName) && jniName != "java/lang/Object") {
				_jniNameCache [type] = jniName;
				return true;
			}
		}

		jniName = null;
		return false;
	}

	internal JavaPeerProxy? GetProxyForPeer (IntPtr handle, Type? targetType = null)
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
					if (_peerProxyCache.TryGetValue (className, out var cached)) {
						if (cached != null && (targetType is null || targetType.IsAssignableFrom (cached.TargetType))) {
							return cached;
						}
					} else if (_typeMap.TryGetValue (className, out var mappedType)) {
						var proxy = mappedType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
						_peerProxyCache [className] = proxy;
						if (proxy != null && (targetType is null || targetType.IsAssignableFrom (proxy.TargetType))) {
							return proxy;
						}
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

	internal IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType = null)
	{
		var proxy = GetProxyForPeer (handle, targetType);
		if (proxy is null && targetType is not null) {
			proxy = GetProxyForManagedType (targetType);
		}

		return proxy?.CreateInstance (handle, transfer);
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

	static bool TryGetCompatJniNameForAndroidComponent (Type type, [NotNullWhen (true)] out string? jniName)
	{
		if (!IsAndroidComponentType (type)) {
			jniName = null;
			return false;
		}

		var (typeName, parentJniName, ns) = GetCompatTypeNameParts (type);
		jniName = parentJniName is not null
			? $"{parentJniName}_{typeName}"
			: ns.Length == 0
				? typeName
				: $"{ns.ToLowerInvariant ().Replace ('.', '/')}/{typeName}";
		return true;
	}

	static bool IsAndroidComponentType (Type type)
	{
		return type.IsDefined (typeof (global::Android.App.ActivityAttribute), inherit: false) ||
			type.IsDefined (typeof (global::Android.App.ApplicationAttribute), inherit: false) ||
			type.IsDefined (typeof (global::Android.App.InstrumentationAttribute), inherit: false) ||
			type.IsDefined (typeof (global::Android.App.ServiceAttribute), inherit: false) ||
			type.IsDefined (typeof (global::Android.Content.BroadcastReceiverAttribute), inherit: false) ||
			type.IsDefined (typeof (global::Android.Content.ContentProviderAttribute), inherit: false);
	}

	static (string TypeName, string? ParentJniName, string Namespace) GetCompatTypeNameParts (Type type)
	{
		var nameParts = new List<string> { SanitizeTypeName (type.Name) };
		var current = type;
		string? parentJniName = null;

		while (current.DeclaringType is Type parentType) {
			if (TryGetJniNameForType (parentType, out var explicitJniName) ||
					TryGetCompatJniNameForAndroidComponent (parentType, out explicitJniName)) {
				parentJniName = explicitJniName;
				break;
			}

			nameParts.Add (SanitizeTypeName (parentType.Name));
			current = parentType;
		}

		nameParts.Reverse ();
		return (string.Join ("_", nameParts), parentJniName, current.Namespace ?? "");
	}

	static string SanitizeTypeName (string name)
	{
		var tick = name.IndexOf ('`');
		return (tick >= 0 ? name.Substring (0, tick) : name).Replace ('+', '_');
	}

	/// <summary>
	/// Creates a peer instance using the proxy's CreateInstance method.
	/// Given a managed type, resolves the JNI name, finds the proxy, and calls CreateInstance.
	/// </summary>
	internal bool TryCreatePeer (Type type, IntPtr handle, JniHandleOwnership transfer)
	{
		return CreatePeer (handle, transfer, type) != null;
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
	/// Called from generated no-op UCO constructors for open generic types.
	/// When called outside WithinNewObjectScope (i.e., from FinishCreateInstance),
	/// raises NotSupportedException via JNI to match the legacy n_Activate behavior.
	/// </summary>
	internal static void ThrowIfOpenGenericActivation ()
	{
		if (!JniEnvironment.WithinNewObjectScope) {
			JniEnvironment.Runtime.RaisePendingException (
				new NotSupportedException (
					"Constructing instances of generic types from Java is not supported, as the type parameters cannot be determined."));
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
