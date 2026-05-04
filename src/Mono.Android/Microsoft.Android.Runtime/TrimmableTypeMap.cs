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
/// Central type map for the trimmable typemap path. Owns the ITypeMapWithAliasing
/// and provides peer creation, invoker resolution, container factories, and native
/// method registration. All proxy attribute access is encapsulated here.
/// </summary>
public class TrimmableTypeMap
{
	static readonly Lock s_initLock = new ();
	static readonly JavaPeerProxy s_noPeerSentinel = new MissingJavaPeerProxy ();
	static TrimmableTypeMap? s_instance;
	static bool s_nativeMethodsRegistered;

	internal static TrimmableTypeMap Instance =>
		s_instance ?? throw new InvalidOperationException (
			"TrimmableTypeMap has not been initialized. Ensure RuntimeFeature.TrimmableTypeMap is enabled and the JNI runtime is initialized.");

	readonly ITypeMapWithAliasing _typeMap;
	readonly ConcurrentDictionary<Type, JavaPeerProxy> _proxyCache = new ();
	readonly ConcurrentDictionary<string, JavaPeerProxy[]> _jniProxyCache = new (StringComparer.Ordinal);

	TrimmableTypeMap (ITypeMapWithAliasing typeMap)
	{
		_typeMap = typeMap;
	}

	/// <summary>
	/// Initializes the singleton with a single merged typemap universe.
	/// Called from <see cref="TypeMapLoader.Initialize"/> in the generated root assembly
	/// (_Microsoft.Android.TypeMaps) when assembly typemaps are merged (Release builds).
	/// </summary>
	public static void Initialize (IReadOnlyDictionary<string, Type> typeMap, IReadOnlyDictionary<Type, Type> proxyMap)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyMap);
		InitializeCore (new SingleUniverseTypeMap (typeMap, proxyMap));
	}

	/// <summary>
	/// Initializes the singleton with multiple per-assembly typemap universes.
	/// Called from <see cref="TypeMapLoader.Initialize"/> in the generated root assembly
	/// (_Microsoft.Android.TypeMaps) when each assembly has its own typemap universe (Debug builds).
	/// </summary>
	public static void Initialize (IReadOnlyDictionary<string, Type>[] typeMaps, IReadOnlyDictionary<Type, Type>[] proxyMaps)
	{
		ArgumentNullException.ThrowIfNull (typeMaps);
		ArgumentNullException.ThrowIfNull (proxyMaps);
		if (typeMaps.Length == 0) {
			throw new ArgumentException ("At least one typemap universe must be provided.", nameof (typeMaps));
		}
		if (typeMaps.Length != proxyMaps.Length) {
			throw new ArgumentException ($"typeMaps.Length ({typeMaps.Length}) must equal proxyMaps.Length ({proxyMaps.Length}).", nameof (proxyMaps));
		}
		var universes = new SingleUniverseTypeMap [typeMaps.Length];
		for (int i = 0; i < typeMaps.Length; i++) {
			universes [i] = new SingleUniverseTypeMap (typeMaps [i], proxyMaps [i]);
		}
		InitializeCore (new AggregateTypeMap (universes));
	}

	static void InitializeCore (ITypeMapWithAliasing typeMap)
	{
		lock (s_initLock) {
			if (s_instance is not null) {
				throw new InvalidOperationException ("TrimmableTypeMap has already been initialized.");
			}

			s_instance = new TrimmableTypeMap (typeMap);
		}
	}

	internal static unsafe void RegisterNativeMethods ()
	{
		lock (s_initLock) {
			if (s_nativeMethodsRegistered) {
				throw new InvalidOperationException ("TrimmableTypeMap native methods have already been registered.");
			}

			if (s_instance is null) {
				throw new InvalidOperationException (
					"TrimmableTypeMap has not been initialized. Ensure RuntimeFeature.TrimmableTypeMap is enabled and the JNI runtime is initialized.");
			}

			using var runtimeClass = new JniType ("mono/android/Runtime"u8);
			fixed (byte* name = "registerNatives"u8, sig = "(Ljava/lang/Class;)V"u8) {
				var onRegisterNatives = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnRegisterNatives;
				var method = new JniNativeMethod (name, sig, onRegisterNatives);
				JniEnvironment.Types.RegisterNatives (runtimeClass.PeerReference, [method]);
			}
			s_nativeMethodsRegistered = true;
		}
	}

	/// <summary>
	/// Returns all target types mapped to a JNI name. For non-alias entries, returns a
	/// single-element array. For alias groups, returns the surviving target types from
	/// each alias key. Returns false when no mapping exists or all aliases were trimmed.
	/// </summary>
	internal bool TryGetTargetTypes (string jniName, [NotNullWhen (true)] out Type[]? types)
	{
		var proxies = GetProxiesForJniName (jniName);
		if (proxies.Length == 0) {
			types = null;
			return false;
		}

		types = new Type [proxies.Length];
		for (int i = 0; i < proxies.Length; i++) {
			types [i] = proxies [i].TargetType;
		}
		return true;
	}

	/// <summary>
	/// Resolves and caches all proxies for a JNI name. For non-alias entries, returns a
	/// single-element array. For alias groups, resolves each alias key and returns the
	/// surviving proxies. Returns an empty array when no mapping exists or all aliases were trimmed.
	/// </summary>
	JavaPeerProxy[] GetProxiesForJniName (string jniName)
	{
		return _jniProxyCache.GetOrAdd (jniName, static (name, self) => {
			var result = new List<JavaPeerProxy> ();
			foreach (var type in self._typeMap.GetProxyTypes (name)) {
				var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
				if (proxy is not null) {
					result.Add (proxy);
				}
			}
			return result.Count > 0 ? result.ToArray () : [];
		}, this);
	}

	/// <summary>
	/// Resolves the best proxy for a JNI class name, handling both direct entries and alias groups.
	/// When targetType is available, finds the proxy whose TargetType matches.
	/// When targetType is null, returns the first available proxy.
	/// </summary>
	JavaPeerProxy? GetProxyForJniClass (string className, Type? targetType)
	{
		var proxies = GetProxiesForJniName (className);
		if (proxies.Length == 0) {
			return null;
		}
		if (proxies.Length == 1 || targetType is null) {
			return proxies [0];
		}
		foreach (var proxy in proxies) {
			if (TargetTypeMatches (targetType, proxy.TargetType)) {
				return proxy;
			}
		}
		return null;
	}
	JavaPeerProxy? GetProxyForManagedType (Type managedType)
	{
		if (managedType.IsGenericType && !managedType.IsGenericTypeDefinition) {
			managedType = managedType.GetGenericTypeDefinition ();
		}

		var proxy = _proxyCache.GetOrAdd (managedType, static (type, self) => {
			if (!self._typeMap.TryGetProxyType (type, out var proxyType)) {
				return s_noPeerSentinel;
			}

			return proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false) ?? s_noPeerSentinel;
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
						var proxy = self.GetProxyForJniClass (className, targetType);
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
				try {
					targetClass = JniEnvironment.Types.FindClass (targetJniName);
				} catch (Java.Lang.ClassNotFoundException) {
					// FindClass throws for managed types whose Java peer class is
					// not present in the APK (e.g. test types annotated with
					// [JniTypeSignature("__missing__")]). Treat as "no match" so
					// JavaMarshalValueManager.CreatePeer can surface the correct
					// ArgumentException instead of leaking ClassNotFoundException.
					return null;
				}
				var isAssignable = JniEnvironment.Types.IsAssignableFrom (objClass, targetClass);
				return isAssignable ? proxy : null;
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
		if (targetType == proxyTargetType) {
			return true;
		}

		// Open generic proxy: match only when targetType is a closed instantiation
		// of this generic (e.g. JavaList<int> matches the JavaList<> proxy).
		// IsAssignableFrom alone would incorrectly match unrelated open generics
		// that are technically subclasses (e.g. JavaArray<> is assignable to
		// JavaObject), and proxy.CreateInstance for an open generic always throws.
		if (proxyTargetType.IsGenericTypeDefinition) {
			for (Type? t = targetType; t is not null; t = t.BaseType) {
				if (t.IsGenericType && !t.IsGenericTypeDefinition &&
						t.GetGenericTypeDefinition () == proxyTargetType) {
					return true;
				}
			}
			return false;
		}

		return targetType.IsAssignableFrom (proxyTargetType);
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

			var proxies = s_instance.GetProxiesForJniName (className);
			if (proxies.Length == 0) {
				return;
			}

			// Use the class reference passed from Java (via C++) — not JniType(className)
			// which resolves via FindClass and may get a different class from a different ClassLoader.
			// Registering natives on that other instance is silently wrong.
			using var jniType = new JniType (ref classRef, JniObjectReferenceOptions.Copy);
			foreach (var proxy in proxies) {
				if (proxy is IAndroidCallableWrapper acw) {
					acw.RegisterNatives (jniType);
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
