#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Android.OS;
using Java.Interop;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Runtime;

namespace Android.Runtime
{
	/// <summary>
	/// Provides type mappings for Java-to-.NET type resolution using compile-time generated attributes.
	/// Used for NativeAOT and CoreCLR runtimes where the Type Mapping API is available.
	/// </summary>
	class TypeMapAttributeTypeMap : ITypeMap
	{
		const string TypeMapsAssemblyName = "_Microsoft.Android.TypeMaps";

		readonly IReadOnlyDictionary<string, Type> _externalTypeMap;

		readonly ConcurrentDictionary<Type, JavaPeerProxy?> _proxyInstances = new ();
		readonly ConcurrentDictionary<Type, Type[]?> _aliasCache = new ();
		readonly ConcurrentDictionary<Type, string> _jniNameCache = new ();
		readonly ConcurrentDictionary<string, Type?> _classToTypeCache = new ();

		public TypeMapAttributeTypeMap ()
		{
			LoadTypeMapsAssembly ();

			if (RuntimeFeature.IsCoreClrRuntime) {
				_externalTypeMap = TypeMapping.GetOrCreateExternalTypeMapping<Java.Lang.Object> ();
			} else {
				_externalTypeMap = CollectTypeMapEntriesForMonoVM ();
			}
		}

		void LoadTypeMapsAssembly ()
		{
			var typeMapsAssembly = Assembly.Load (TypeMapsAssemblyName);
			Assembly.SetEntryAssembly (typeMapsAssembly);
		}

		static IReadOnlyDictionary<string, Type> CollectTypeMapEntriesForMonoVM ()
		{
			var typeMapsAssembly = Assembly.Load (TypeMapsAssemblyName);
			var result = new Dictionary<string, Type> (StringComparer.Ordinal);
			var typeMapAttrType = typeof (TypeMapAttribute<>);

			foreach (var attrData in typeMapsAssembly.GetCustomAttributesData ()) {
				if (!attrData.AttributeType.IsGenericType)
					continue;
				if (attrData.AttributeType.GetGenericTypeDefinition () != typeMapAttrType)
					continue;

				try {
					var args = attrData.ConstructorArguments;
					if (args.Count < 2)
						continue;

					var jniName = args[0].Value as string;
					var targetType = args[1].Value as Type;

					if (jniName != null && targetType != null) {
						result[jniName] = targetType;
					}
				} catch {
					// Skip entries that fail to resolve
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public Type? TryGetExactTypeMapping (string jniTypeName)
		{
			_externalTypeMap.TryGetValue (jniTypeName, out Type? type);
			return type;
		}

		/// <inheritdoc/>
		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (!_externalTypeMap.TryGetValue (jniSimpleReference, out Type? type)) {
				types = null;
				return false;
			}

			var cachedAliases = _aliasCache.GetOrAdd (type, t => ResolveAliases (t, _externalTypeMap));
			types = cachedAliases ?? [type];
			return true;
		}

		static Type[]? ResolveAliases (Type type, IReadOnlyDictionary<string, Type> externalTypeMap)
		{
			var aliasesAttr = type.GetCustomAttribute<JavaInteropAliasesAttribute> ();
			if (aliasesAttr == null) {
				return null;
			}

			var aliasedTypes = new List<Type> ();
			foreach (var aliasKey in aliasesAttr.AliasKeys) {
				if (externalTypeMap.TryGetValue (aliasKey, out Type? aliasedType)) {
					aliasedTypes.Add (aliasedType);
				}
			}
			return aliasedTypes.Count > 0 ? aliasedTypes.ToArray () : null;
		}

		/// <inheritdoc/>
		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			// Use GetOrAdd for thread-safe caching (empty string = not found)
			var cached = _jniNameCache.GetOrAdd (type, ComputeJniNameForType);
			if (cached.Length == 0) {
				jniName = null;
				return false;
			}
			jniName = cached;
			return true;
		}

		string ComputeJniNameForType (Type type)
		{
			var attrs = type.GetCustomAttributes (typeof (IJniNameProviderAttribute), inherit: false);
			if (attrs.Length > 0 && attrs[0] is IJniNameProviderAttribute jniNameProvider && !string.IsNullOrEmpty (jniNameProvider.Name)) {
				return jniNameProvider.Name.Replace ('.', '/');
			}

			var sigAttr = type.GetCustomAttribute<JniTypeSignatureAttribute> (inherit: false);
			if (sigAttr != null && !string.IsNullOrEmpty (sigAttr.SimpleReference)) {
				return sigAttr.SimpleReference;
			}

			var jniName = JavaNativeTypeManager.ToJniName (type);
			return string.IsNullOrEmpty (jniName) ? "" : jniName;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetJniNamesForType (Type type)
		{
			if (TryGetJniNameForType (type, out string? jniName)) {
				return [jniName];
			}
			return [];
		}

		/// <inheritdoc/>
		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			// Look up the proxy for the interface/abstract class
			var proxy = GetProxyForManagedType (type);
			if (proxy?.InvokerType != null) {
				invokerType = proxy.InvokerType;
				return true;
			}

			invokerType = null;
			return false;
		}

		/// <inheritdoc/>
		public JavaPeerProxy? GetProxyForManagedType (Type managedType)
		{
			var proxy = GetProxyForType (managedType);
			if (proxy != null) {
				return proxy;
			}

			if (TryGetJniNameForType (managedType, out string? jniName)) {
				if (_externalTypeMap.TryGetValue (jniName, out Type? proxyType)) {
					return GetProxyForType (proxyType);
				}
			}

			return null;
		}

		JavaPeerProxy? GetProxyForType (Type type)
		{
			return _proxyInstances.GetOrAdd (type, static t => t.GetCustomAttribute<JavaPeerProxy> (inherit: false));
		}

		/// <inheritdoc/>
		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			return PeerCreationHelper.CreatePeer (
				handle,
				transfer,
				targetType,
				typeMap: this,
				typeResolver: ResolveTypeWithCaching,
				instanceCreator: CreateInstance,
				resolveInvokerTypes: true);
		}

		/// <summary>
		/// Resolves type from class pointer with caching and array type handling.
		/// </summary>
		Type? ResolveTypeWithCaching (IntPtr class_ptr, string class_name)
		{
			return _classToTypeCache.GetOrAdd (class_name, _ => FindTypeInHierarchy (class_ptr, class_name));
		}

		IJavaPeerable? CreateInstance (Type type, IntPtr handle, JniHandleOwnership transfer)
		{
			var proxy = GetProxyForType (type) ?? GetProxyForManagedType (type);
			if (proxy is null) {
				return null;
			}

			return proxy.CreateInstance (handle, transfer);
		}

		/// <summary>
		/// Walks the Java class hierarchy to find a registered .NET type.
		/// </summary>
		Type? FindTypeInHierarchy (IntPtr class_ptr, string class_name)
		{
			if (class_name.StartsWith ("[", StringComparison.Ordinal)) {
				return GetArrayType (class_name);
			}

			return PeerCreationHelper.WalkHierarchy (class_ptr, class_name, this);
		}

		Type? GetArrayType (string jniName)
		{
			if (jniName.StartsWith ("[[", StringComparison.Ordinal)) {
				return null; // Nested arrays not supported
			}

			return jniName switch {
				"[Z" => typeof (JavaArray<bool>),
				"[B" => typeof (JavaArray<byte>),
				"[C" => typeof (JavaArray<char>),
				"[S" => typeof (JavaArray<short>),
				"[I" => typeof (JavaArray<int>),
				"[J" => typeof (JavaArray<long>),
				"[F" => typeof (JavaArray<float>),
				"[D" => typeof (JavaArray<double>),
				_ => GetObjectArrayType (jniName)
			};
		}

		Type? GetObjectArrayType (string jniName)
		{
			if (_externalTypeMap.TryGetValue (jniName, out Type? arrayType)) {
				return arrayType;
			}

			// Fallback for java/lang/String arrays
			if (jniName.StartsWith ("[L", StringComparison.Ordinal) && jniName.EndsWith (";", StringComparison.Ordinal)) {
				string elementJni = jniName.Substring (2, jniName.Length - 3);
				if (string.Equals (elementJni, "java/lang/String", StringComparison.Ordinal)) {
					return typeof (JavaArray<string>);
				}
			}

			return null;
		}

		/// <inheritdoc/>
		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			string classNameStr = className.ToString ();

			if (!_externalTypeMap.TryGetValue (classNameStr, out Type? type)) {
				throw new TypeMapException (
					$"XA4301: Type lookup failed for '{classNameStr}'. " +
					"Ensure the type has [Register] attribute and is not trimmed away.");
			}

			JavaPeerProxy? proxy = GetProxyForType (type!);
			if (proxy is IAndroidCallableWrapper acw) {
				return acw.GetFunctionPointer (methodIndex);
			}

			if (proxy is null) {
				throw new TypeMapException (
					$"XA4302: No proxy found for type '{type!.FullName}' when looking up function pointer for '{classNameStr}' at index {methodIndex}.");
			}

			throw new TypeMapException (
				$"XA4303: Type '{type!.FullName}' is an MCW and does not have function pointers. " +
				$"Requested function pointer for '{classNameStr}' at index {methodIndex}.");
		}

		/// <summary>
		/// Creates a 1D .NET array of the specified element type using the pre-generated proxy.
		/// </summary>
		public Array CreateArray (Type elementType, int length, int rank)
		{
			if (rank < 1 || rank > 3) {
				throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1, 2, or 3");
			}

			if (elementType.IsArray) {
				var innerType = elementType.GetElementType ()
					?? throw new InvalidOperationException ($"Cannot get element type from array type {elementType.FullName}");
				return CreateArray (innerType, length, rank + 1);
			}

			if (!TryGetJniNameForType (elementType, out string? jniName)) {
				throw new InvalidOperationException ($"No JNI name found for element type {elementType.FullName}");
			}

			if (!_externalTypeMap.TryGetValue (jniName, out Type? proxyType)) {
				throw new InvalidOperationException ($"No proxy registered for {jniName}");
			}

			var proxy = GetProxyForType (proxyType)
				?? throw new InvalidOperationException ($"No proxy instance for {proxyType.FullName}");

			return proxy.GetDerivedTypeFactory ().CreateArray (length, rank);
		}
	}
}
