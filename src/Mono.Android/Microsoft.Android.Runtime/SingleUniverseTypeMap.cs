#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Wraps a single <see cref="IReadOnlyDictionary{String, Type}"/> universe
/// and its proxy type map. Handles <see cref="JavaPeerAliasesAttribute"/>
/// alias resolution within that universe.
/// Used in both Debug (one per assembly) and Release (single merged).
/// </summary>
sealed class SingleUniverseTypeMap : ITypeMapWithAliasing
{
	readonly IReadOnlyDictionary<string, Type> _typeMap;
	readonly IReadOnlyDictionary<Type, Type> _proxyTypeMap;

	public SingleUniverseTypeMap (IReadOnlyDictionary<string, Type> typeMap, IReadOnlyDictionary<Type, Type> proxyTypeMap)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyTypeMap);
		_typeMap = typeMap;
		_proxyTypeMap = proxyTypeMap;
	}

	public IEnumerable<Type> GetTargetTypes (string jniName)
	{
		foreach (var type in GetEntryTypes (jniName)) {
			var proxy = type.GetCustomAttribute<JavaPeerProxy> (inherit: false);
			yield return proxy?.TargetType ?? type;
		}
	}

	public IEnumerable<Type> GetProxyTypes (string jniName)
	{
		foreach (var type in GetEntryTypes (jniName)) {
			if (type.GetCustomAttribute<JavaPeerProxy> (inherit: false) is not null) {
				yield return type;
			}
		}
	}

	IEnumerable<Type> GetEntryTypes (string jniName)
	{
		if (!_typeMap.TryGetValue (jniName, out var mappedType)) {
			yield break;
		}

		var aliases = mappedType.GetCustomAttribute<JavaPeerAliasesAttribute> (inherit: false);
		if (aliases is null) {
			yield return mappedType;
			yield break;
		}

		foreach (var key in aliases.Aliases) {
			if (_typeMap.TryGetValue (key, out var aliasEntryType)) {
				yield return aliasEntryType;
			}
		}
	}

	public bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType)
	{
		if (!_proxyTypeMap.TryGetValue (managedType, out var mappedProxyType)) {
			proxyType = null;
			return false;
		}

		// Fast path: direct proxy
		if (mappedProxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false) is not null) {
			proxyType = mappedProxyType;
			return true;
		}

		// Slow path: alias holder — find the alias whose target type matches
		var aliases = mappedProxyType.GetCustomAttribute<JavaPeerAliasesAttribute> (inherit: false);
		if (aliases is not null) {
			foreach (var key in aliases.Aliases) {
				if (_typeMap.TryGetValue (key, out var aliasProxyType)) {
					var aliasProxy = aliasProxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
					if (aliasProxy is not null && TrimmableTypeMap.TargetTypeMatches (managedType, aliasProxy.TargetType)) {
						proxyType = aliasProxyType;
						return true;
					}
				}
			}
		}

		proxyType = null;
		return false;
	}
}
