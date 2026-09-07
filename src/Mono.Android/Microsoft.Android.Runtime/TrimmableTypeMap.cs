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
/// Central type map for the trimmable typemap path. Owns the ITypeMap
/// and provides peer creation, invoker resolution, and native
/// method registration. All proxy attribute access is encapsulated here.
/// </summary>
public class TrimmableTypeMap
{
	static readonly Lock s_initLock = new ();
	static readonly JavaPeerProxy s_noPeerSentinel = new MissingJavaPeerProxy ();
	static TrimmableTypeMap? s_instance;
	static bool s_nativeMethodsRegistered;
	static JniMethodInfo? s_classGetInterfacesMethod;

	internal static TrimmableTypeMap Instance =>
		s_instance ?? throw new InvalidOperationException (
			"TrimmableTypeMap has not been initialized. Ensure RuntimeFeature.TrimmableTypeMap is enabled and the JNI runtime is initialized.");

	readonly ITypeMap _typeMap;
	readonly ConcurrentDictionary<Type, JavaPeerProxy> _proxyCache = new ();
	readonly ConcurrentDictionary<string, object> _jniProxyCache = new (StringComparer.Ordinal);
	readonly ConcurrentDictionary<(string ClassName, Type TargetType), JavaPeerProxy> _interfaceProxyCache = new ();

	TrimmableTypeMap (ITypeMap typeMap)
	{
		_typeMap = typeMap;
	}

	/// <summary>
	/// Initializes the singleton with a single merged typemap universe.
	/// </summary>
	public static void Initialize (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyMap)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyMap);
		InitializeCore (new SingleUniverseTypeMap (typeMap, proxyMap));
	}

	/// <summary>
	/// Initializes the singleton with multiple per-assembly typemap universes.
	/// </summary>
	public static void Initialize (
		IReadOnlyDictionary<string, Type>[] typeMaps,
		IReadOnlyDictionary<Type, Type>[] proxyMaps)
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

	static void InitializeCore (ITypeMap typeMap)
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

	internal IEnumerable<Type> GetTargetTypes (string jniName)
	{
		var cacheEntry = GetProxyCacheEntryForJniName (jniName);
		if (cacheEntry is JavaPeerProxy proxy) {
			yield return proxy.TargetType;
			yield break;
		}

		foreach (var aliasProxy in GetProxyArrayCacheEntry (cacheEntry)) {
			yield return aliasProxy.TargetType;
		}
	}

	/// <summary>
	/// Returns the first target type mapped to a JNI name without materializing all target types.
	/// </summary>
	internal bool TryGetTargetType (string jniName, [NotNullWhen (true)] out Type? type)
	{
		var cacheEntry = GetProxyCacheEntryForJniName (jniName);
		if (cacheEntry is JavaPeerProxy proxy) {
			type = proxy.TargetType;
			return true;
		}

		var proxies = GetProxyArrayCacheEntry (cacheEntry);
		if (proxies.Length == 0) {
			type = null;
			return false;
		}

		type = proxies [0].TargetType;
		return true;
	}

	/// <summary>
	/// Resolves and caches proxies for a JNI name. Non-alias entries are cached directly as
	/// <see cref="JavaPeerProxy"/> instances. Alias groups are cached as arrays, and misses as
	/// an empty array.
	/// </summary>
	object GetProxyCacheEntryForJniName (string jniName)
	{
		return _jniProxyCache.GetOrAdd (jniName, static (name, self) => {
			var builder = new JniProxyCacheBuilder ();
			self._typeMap.CollectProxyTypes (name, ref builder);
			return builder.Build ();
		}, this);
	}

	internal static JavaPeerProxy[] GetProxyArrayCacheEntry (object cacheEntry)
	{
		if (cacheEntry is JavaPeerProxy[] proxies) {
			return proxies;
		}

		throw new InvalidOperationException (
			$"Unexpected JNI proxy cache entry type '{cacheEntry.GetType ().FullName}'. " +
			$"Expected {nameof (JavaPeerProxy)} or {nameof (JavaPeerProxy)}[].");
	}

	/// <summary>
	/// Resolves the best proxy for a JNI class name, handling both direct entries and alias groups.
	/// When targetType is available, finds the proxy whose TargetType matches.
	/// When targetType is null, returns the first available proxy.
	/// </summary>
	JavaPeerProxy? GetProxyForJniClass (string className, Type? targetType)
	{
		var cacheEntry = GetProxyCacheEntryForJniName (className);
		if (cacheEntry is JavaPeerProxy singleProxy) {
			return targetType is null || TargetTypeMatches (targetType, singleProxy.TargetType)
				? singleProxy
				: null;
		}

		var proxies = GetProxyArrayCacheEntry (cacheEntry);
		if (proxies.Length == 0) {
			return null;
		}
		if (targetType is null) {
			return proxies [0];
		}
		foreach (var proxy in proxies) {
			if (TargetTypeMatches (targetType, proxy.TargetType)) {
				return proxy;
			}
		}
		return null;
	}

	internal static void RegisterNativeMethods (object cacheEntry, JniType jniType)
	{
		if (cacheEntry is JavaPeerProxy singleProxy) {
			if (singleProxy is IAndroidCallableWrapper acw) {
				acw.RegisterNatives (jniType);
			}
			return;
		}

		foreach (var proxy in GetProxyArrayCacheEntry (cacheEntry)) {
			if (proxy is IAndroidCallableWrapper acw) {
				acw.RegisterNatives (jniType);
			}
		}
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

		if (TryResolveProxyFromSealedTargetType (handle, targetType, out var proxy)) {
			return proxy;
		}

		return TryGetProxyFromHierarchy (handle, targetType) ??
			TryGetProxyFromTargetType (handle, targetType);
	}

	bool TryResolveProxyFromSealedTargetType (
		IntPtr handle,
		Type? targetType,
		out JavaPeerProxy? proxy)
	{
		proxy = null;
		// App peers can use class loaders which differ from the runtime fallback loader.
		if (targetType is null ||
				!targetType.IsSealed ||
				targetType.Assembly != typeof (Java.Lang.Object).Assembly) {
			return false;
		}

		var targetProxy = GetProxyForManagedType (targetType);
		if (targetProxy is null) {
			return false;
		}

		var targetClass = default (JniObjectReference);
		try {
			targetClass = JniEnvironment.Types.FindClass (targetProxy.JniName);
			var reference = new JniObjectReference (handle);
			if (JniEnvironment.Types.IsInstanceOf (reference, targetClass)) {
				proxy = targetProxy;
			}
			return true;
		} catch (Java.Lang.ClassNotFoundException) {
			return false;
		} finally {
			JniObjectReference.Dispose (ref targetClass);
		}
	}

	JavaPeerProxy? TryGetProxyFromHierarchy (IntPtr handle, Type? targetType)
	{
		var selfRef = new JniObjectReference (handle);
		var jniClass = JniEnvironment.Types.GetObjectClass (selfRef);

		try {
			while (jniClass.IsValid) {
				var className = JniEnvironment.Types.GetJniTypeNameFromClass (jniClass);
				if (className is not null) {
					var proxy = GetProxyForJniClass (className, targetType);
					if (proxy is not null) {
						return proxy;
					}
				}

				// When targetType is an interface, also check the Java interfaces
				// at each level. getInterfaces() only returns directly declared
				// interfaces so we must call it at each class in the hierarchy.
				// This handles the case where an intermediate class entry (e.g.,
				// X509ExtendedTrustManager) was trimmed but the Java interface
				// entry (e.g., X509TrustManager) survives.
				if (targetType is { IsInterface: true } && className != null) {
					var result = GetProxyForJavaInterfaces (jniClass, className, targetType);
					if (result != null) {
						return result;
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

	JavaPeerProxy? GetProxyForJavaInterfaces (JniObjectReference jniClass, string className, Type targetType)
	{
		var proxy = _interfaceProxyCache.GetOrAdd (
			(className, targetType),
			_ => TryMatchInterfaces (jniClass, targetType) ?? s_noPeerSentinel);
		return ReferenceEquals (proxy, s_noPeerSentinel) ? null : proxy;
	}

	// getInterfaces() returns only directly declared interfaces (not transitive),
	// so we recurse into super-interfaces to find the matching TypeMap entry.
	JavaPeerProxy? TryMatchInterfaces (JniObjectReference jniClass, Type targetType)
	{
		var interfaces = JniEnvironment.InstanceMethods.CallObjectMethod (jniClass, GetClassGetInterfacesMethod ());
		try {
			if (!interfaces.IsValid) {
				return null;
			}

			int count = JniEnvironment.Arrays.GetArrayLength (interfaces);
			for (int i = 0; i < count; i++) {
				var iface = JniEnvironment.Arrays.GetObjectArrayElement (interfaces, i);
				try {
					var ifaceName = JniEnvironment.Types.GetJniTypeNameFromClass (iface);
					if (ifaceName is not null) {
						var proxy = GetProxyForJniClass (ifaceName, targetType);
						if (proxy is not null) {
							return proxy;
						}
					}

					// Recurse into super-interfaces
					var result = TryMatchInterfaces (iface, targetType);
					if (result is not null) {
						return result;
					}
				} finally {
					JniObjectReference.Dispose (ref iface);
				}
			}
		} finally {
			JniObjectReference.Dispose (ref interfaces);
		}

		return null;
	}

	static JniMethodInfo GetClassGetInterfacesMethod ()
	{
		var method = s_classGetInterfacesMethod;
		if (method != null) {
			return method;
		}

		var classClass = JniEnvironment.Types.FindClass ("java/lang/Class");
		try {
			method = JniEnvironment.InstanceMethods.GetMethodID (classClass, "getInterfaces", "()[Ljava/lang/Class;");
		} finally {
			JniObjectReference.Dispose (ref classClass);
		}

		var previous = Interlocked.CompareExchange (ref s_classGetInterfacesMethod, method, null);
		return previous ?? method;
	}

	JavaPeerProxy? TryGetProxyFromTargetType (IntPtr handle, Type? targetType)
	{
		if (targetType is null) {
			return null;
		}

		var proxy = GetProxyForManagedType (targetType);
		// Verify the Java object is actually assignable to the target Java type
		// before returning the fallback proxy. Without this, we'd create invalid peers
		// (e.g., IAppendableInvoker wrapping a java.lang.Integer).
		if (proxy is null || !TryGetJniNameForManagedType (targetType, out var targetJniName)) {
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
				// TrimmableTypeMapValueManager.CreatePeer can surface the correct
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

	internal IJavaPeerable? CreateInstance (
		IntPtr handle,
		[DynamicallyAccessedMembers (Constructors)]
		Type? targetType = null)
	{
		var proxy = GetProxyForJavaObject (handle, targetType);

		IJavaPeerable? peer;
		if (ShouldActivateClosedGenericTarget (proxy, targetType)) {
			peer = ActivateUsingReflection (targetType, handle, ImplicitPeerOwnership);
		} else {
			peer = proxy?.CreateInstance (handle, ImplicitPeerOwnership);
		}
		return RegisterCreatedPeer (peer);
	}

	internal IJavaPeerable? CreateInstanceWithoutReflectionFallback (IntPtr handle, Type? targetType = null)
	{
		var peer = GetProxyForJavaObject (handle, targetType)?.CreateInstance (handle, ImplicitPeerOwnership);
		return RegisterCreatedPeer (peer);
	}

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
	const JniHandleOwnership ImplicitPeerOwnership = JniHandleOwnership.DoNotTransfer | JniHandleOwnership.DoNotRegister;

	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static  readonly    Type[]  XAConstructorSignature  = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };

	static bool ShouldActivateClosedGenericTarget (
			[NotNullWhen (true)] JavaPeerProxy? proxy,
			[NotNullWhen (true)] Type? targetType)
	{
		return proxy is not null &&
			proxy.TargetType.IsGenericTypeDefinition &&
			targetType is not null &&
			targetType.IsGenericType &&
			!targetType.IsGenericTypeDefinition;
	}

	static IJavaPeerable? ActivateUsingReflection (
			[DynamicallyAccessedMembers (Constructors)]
			Type closedType,
			IntPtr handle,
			JniHandleOwnership transfer)
	{
		var ctor = closedType.GetConstructor (ActivationConstructorBindingFlags, null, XAConstructorSignature, null);
		if (ctor is null) {
			return null;
		}

		return (IJavaPeerable) ctor.Invoke ([handle, transfer]);
	}

	static void MarkCreatedPeer (IJavaPeerable peer)
	{
		var peerState = peer.JniManagedPeerState | JniManagedPeerStates.Replaceable;
		if (global::Java.Interop.Runtime.IsGCUserPeer (peer.PeerReference.Handle)) {
			peerState |= JniManagedPeerStates.Activatable;
		}
		peer.SetJniManagedPeerState (peerState);
	}

	static IJavaPeerable? RegisterCreatedPeer (IJavaPeerable? peer)
	{
		if (peer is null) {
			return null;
		}

		// Mark the peer Replaceable *before* registering it. AddPeer() lets a
		// non-replaceable peer evict an existing replaceable one, so registering during
		// construction — which is what ConstructPeerCore does unless the activation
		// constructor is told not to — would let this implicit intermediary evict the
		// peer an earlier caller is already holding, leaving that caller with a wrapper
		// the runtime no longer knows about. See dotnet/android#10973.
		MarkCreatedPeer (peer);
		JniEnvironment.Runtime.ValueManager.AddPeer (peer);
		return peer;
	}

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
	/// Open generic <em>interface</em> peers are intentionally not matched here:
	/// matching on <c>Type.GetInterfaces()</c> would force a trimmer
	/// <c>DynamicallyAccessedMembers(Interfaces)</c> annotation up the chain
	/// (ultimately into Java.Interop's <c>CreatePeer</c> API). Interface peer
	/// discovery is handled from the Java class metadata instead.
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

			var cacheEntry = s_instance.GetProxyCacheEntryForJniName (className);
			if (cacheEntry is JavaPeerProxy[] proxies && proxies.Length == 0) {
				return;
			}

			// Use the class reference passed from Java (via C++) — not JniType(className)
			// which resolves via FindClass and may get a different class from a different ClassLoader.
			// Registering natives on that other instance is silently wrong.
			using var jniType = new JniType (ref classRef, JniObjectReferenceOptions.Copy);
			RegisterNativeMethods (cacheEntry, jniType);
		} catch (Exception ex) {
			Environment.FailFast ($"TrimmableTypeMap: Failed to register natives for class '{className}'.", ex);
		}
	}

	sealed class MissingJavaPeerProxy : JavaPeerProxy
	{
		public MissingJavaPeerProxy () : base ("<missing>", typeof (Java.Lang.Object))
		{
		}

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;
	}

}

struct JniProxyCacheBuilder
{
	static readonly JavaPeerProxy[] Empty = [];

	JavaPeerProxy? first;
	List<JavaPeerProxy>? multiple;

	public void Add (JavaPeerProxy proxy)
	{
		var firstProxy = first;
		if (firstProxy is null) {
			first = proxy;
			return;
		}

		multiple ??= new List<JavaPeerProxy> (2) { firstProxy };
		multiple.Add (proxy);
	}

	public object Build ()
	{
		if (multiple is not null) {
			return multiple.ToArray ();
		}

		return first is JavaPeerProxy proxy ? proxy : Empty;
	}
}
