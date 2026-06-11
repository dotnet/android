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
/// and provides peer creation, invoker resolution, container factories, and native
/// method registration. All proxy attribute access is encapsulated here.
/// </summary>
public class TrimmableTypeMap
{
	internal const DynamicallyAccessedMemberTypes MethodsConstructors =
		DynamicallyAccessedMemberTypes.PublicMethods |
		DynamicallyAccessedMemberTypes.NonPublicMethods |
		DynamicallyAccessedMemberTypes.NonPublicNestedTypes |
		DynamicallyAccessedMemberTypes.PublicConstructors |
		DynamicallyAccessedMemberTypes.NonPublicConstructors;

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
	readonly ConcurrentDictionary<string, JavaPeerProxy[]> _jniProxyCache = new (StringComparer.Ordinal);
	readonly ConcurrentDictionary<(string ClassName, Type TargetType), JavaPeerProxy> _interfaceProxyCache = new ();

	TrimmableTypeMap (ITypeMap typeMap)
	{
		_typeMap = typeMap;
	}

	/// <summary>
	/// Initializes the singleton with a single merged typemap universe and optional
	/// per-rank array dictionaries (consulted by <c>JNIEnv.ArrayCreateInstance</c> under NativeAOT).
	/// </summary>
	public static void Initialize (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyMap)
	{
		Initialize (typeMap, proxyMap, arrayMapsByRank: null);
	}

	/// <summary>
	/// Initializes the singleton with a single merged typemap universe and optional
	/// per-rank array dictionaries (consulted by <c>JNIEnv.ArrayCreateInstance</c> under NativeAOT).
	/// </summary>
	/// <param name="arrayMapsByRank">0-indexed by (rank - 1); null when no array entries were emitted.</param>
	public static void Initialize (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyMap,
		IReadOnlyDictionary<string, Type>?[]? arrayMapsByRank)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyMap);
		InitializeCore (new SingleUniverseTypeMap (typeMap, proxyMap, arrayMapsByRank));
	}

	/// <summary>
	/// Initializes the singleton with a single merged typemap universe and per-assembly array maps.
	/// </summary>
	public static void Initialize (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyMap,
		IReadOnlyDictionary<string, Type>?[][]? arrayMapsByUniverseAndRank)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyMap);
		InitializeCore (new SingleUniverseTypeMap (typeMap, proxyMap, arrayMapsByUniverseAndRank));
	}

	/// <summary>
	/// Initializes the singleton with multiple per-assembly typemap universes and optional
	/// per-universe array dictionaries.
	/// </summary>
	public static void Initialize (
		IReadOnlyDictionary<string, Type>[] typeMaps,
		IReadOnlyDictionary<Type, Type>[] proxyMaps)
	{
		Initialize (typeMaps, proxyMaps, arrayMapsByUniverseAndRank: null);
	}

	/// <summary>
	/// Initializes the singleton with multiple per-assembly typemap universes and optional
	/// per-universe array dictionaries.
	/// </summary>
	/// <param name="arrayMapsByUniverseAndRank">Array maps indexed by universe, then by 0-based rank.</param>
	public static void Initialize (
		IReadOnlyDictionary<string, Type>[] typeMaps,
		IReadOnlyDictionary<Type, Type>[] proxyMaps,
		IReadOnlyDictionary<string, Type>?[][]? arrayMapsByUniverseAndRank)
	{
		ArgumentNullException.ThrowIfNull (typeMaps);
		ArgumentNullException.ThrowIfNull (proxyMaps);
		if (typeMaps.Length == 0) {
			throw new ArgumentException ("At least one typemap universe must be provided.", nameof (typeMaps));
		}
		if (typeMaps.Length != proxyMaps.Length) {
			throw new ArgumentException ($"typeMaps.Length ({typeMaps.Length}) must equal proxyMaps.Length ({proxyMaps.Length}).", nameof (proxyMaps));
		}
		if (arrayMapsByUniverseAndRank is not null && arrayMapsByUniverseAndRank.Length != typeMaps.Length) {
			throw new ArgumentException ($"arrayMapsByUniverseAndRank.Length ({arrayMapsByUniverseAndRank.Length}) must equal typeMaps.Length ({typeMaps.Length}).", nameof (arrayMapsByUniverseAndRank));
		}

		var universes = new SingleUniverseTypeMap [typeMaps.Length];
		for (int i = 0; i < typeMaps.Length; i++) {
			universes [i] = new SingleUniverseTypeMap (typeMaps [i], proxyMaps [i], arrayMapsByUniverseAndRank? [i]);
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

	/// <summary>
	/// Returns all target types mapped to a JNI name. For non-alias entries, returns a
	/// single-element array. For alias groups, returns the surviving target types from
	/// each alias key. Returns false when no mapping exists or all aliases were trimmed.
	/// </summary>
	internal bool TryGetTargetTypes (string jniName, [NotNullWhen (true)] out TargetTypeInfo[]? types)
	{
		var proxies = GetProxiesForJniName (jniName);
		if (proxies.Length == 0) {
			types = null;
			return false;
		}

		types = new TargetTypeInfo [proxies.Length];
		for (int i = 0; i < proxies.Length; i++) {
			types [i] = new TargetTypeInfo (proxies [i].TargetType);
		}
		return true;
	}

	internal sealed class TargetTypeInfo
	{
		public TargetTypeInfo (
				[DynamicallyAccessedMembers (MethodsConstructors)]
				Type type)
		{
			Type = type;
		}

		[DynamicallyAccessedMembers (MethodsConstructors)]
		public Type Type { get; }
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

					// When targetType is an interface, also check the Java interfaces
					// at each level. getInterfaces() only returns directly declared
					// interfaces so we must call it at each class in the hierarchy.
					// This handles the case where an intermediate class entry (e.g.,
					// X509ExtendedTrustManager) was trimmed but the Java interface
					// entry (e.g., X509TrustManager) survives.
					if (targetType is { IsInterface: true } && className != null) {
						var result = GetProxyForJavaInterfaces (self, jniClass, className, targetType);
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

		static JavaPeerProxy? GetProxyForJavaInterfaces (TrimmableTypeMap self, JniObjectReference jniClass, string className, Type targetType)
		{
			var proxy = self._interfaceProxyCache.GetOrAdd (
				(className, targetType),
				_ => TryMatchInterfaces (self, jniClass, targetType) ?? s_noPeerSentinel);
			return ReferenceEquals (proxy, s_noPeerSentinel) ? null : proxy;
		}

		// getInterfaces() returns only directly declared interfaces (not transitive),
		// so we recurse into super-interfaces to find the matching TypeMap entry.
		static JavaPeerProxy? TryMatchInterfaces (TrimmableTypeMap self, JniObjectReference jniClass, Type targetType)
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
						if (ifaceName != null) {
							var proxy = self.GetProxyForJniClass (ifaceName, targetType);
							if (proxy != null && TargetTypeMatches (targetType, proxy.TargetType)) {
								return proxy;
							}
						}

						// Recurse into super-interfaces
						var result = TryMatchInterfaces (self, iface, targetType);
						if (result != null) {
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
	}

	internal IJavaPeerable? CreateInstance (
			IntPtr handle,
			[DynamicallyAccessedMembers (Constructors)]
			Type? targetType = null)
	{
		var proxy = GetProxyForJavaObject (handle, targetType);

		IJavaPeerable? peer;
		if (ShouldActivateClosedGenericTarget (proxy, targetType)) {
			peer = ActivateUsingReflection (targetType, handle, JniHandleOwnership.DoNotTransfer);
		} else {
			peer = proxy?.CreateInstance (handle, JniHandleOwnership.DoNotTransfer);
		}
		if (peer is not null) {
			MarkCreatedPeer (peer);
		}
		return peer;
	}

	internal IJavaPeerable? CreateInstanceWithoutReflectionFallback (IntPtr handle, Type? targetType = null)
	{
		var peer = GetProxyForJavaObject (handle, targetType)?.CreateInstance (handle, JniHandleOwnership.DoNotTransfer);
		if (peer is not null) {
			MarkCreatedPeer (peer);
		}
		return peer;
	}

	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

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

	/// <summary>AOT-safe lookup of the closed managed array type for the given element type.</summary>
	internal bool TryGetArrayType (Type elementType, [NotNullWhen (true)] out Type? arrayType)
	{
		arrayType = null;

		// Walk array nesting to the leaf; rankIndex = depth = (rank - 1).
		// Reject multi-dim arrays (byte[,]) — JNI only supports szarrays.
		var leaf = elementType;
		int rankIndex = 0;
		while (leaf.IsArray) {
			if (!leaf.IsSZArray) {
				return false;
			}
			var next = leaf.GetElementType ();
			if (next is null) {
				return false;
			}
			leaf = next;
			rankIndex++;
		}

		bool isPrimitiveLeaf = leaf.IsPrimitive;
		string? leafJniName = isPrimitiveLeaf
			? TryGetPrimitiveJniName (leaf, out var p) ? p : null
			: TryGetJniNameForManagedType (leaf, out var jni) ? jni : null;

		if (leafJniName is not null && _typeMap.TryGetArrayType (leafJniName, rankIndex, out arrayType)) {
			return true;
		}

		if (isPrimitiveLeaf) {
			arrayType = MakePrimitiveArrayType (elementType);
			return true;
		}

		return false;
	}

	static Type MakePrimitiveArrayType (Type elementType)
	{
#pragma warning disable IL3050 // Primitive array types are runtime intrinsic; no generated generic code is needed.
		return elementType.MakeArrayType ();
#pragma warning restore IL3050
	}

	/// <summary>JNI single-letter encoding for primitive element types.</summary>
	static bool TryGetPrimitiveJniName (Type primitive, [NotNullWhen (true)] out string? jni)
	{
		if (primitive == typeof (bool))   { jni = "Z"; return true; }
		if (primitive == typeof (byte))   { jni = "B"; return true; }
		if (primitive == typeof (char))   { jni = "C"; return true; }
		if (primitive == typeof (short))  { jni = "S"; return true; }
		if (primitive == typeof (int))    { jni = "I"; return true; }
		if (primitive == typeof (long))   { jni = "J"; return true; }
		if (primitive == typeof (float))  { jni = "F"; return true; }
		if (primitive == typeof (double)) { jni = "D"; return true; }
		jni = null;
		return false;
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
