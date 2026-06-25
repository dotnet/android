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
sealed class SingleUniverseTypeMap : ITypeMap
{
	readonly IReadOnlyDictionary<string, Type> _typeMap;
	readonly IReadOnlyDictionary<Type, Type> _proxyTypeMap;
	readonly IReadOnlyDictionary<string, Type>?[][] _arrayMapsByUniverseAndRank;

	public SingleUniverseTypeMap (IReadOnlyDictionary<string, Type> typeMap, IReadOnlyDictionary<Type, Type> proxyTypeMap)
		: this (typeMap, proxyTypeMap, arrayMapsByRank: null)
	{
	}

	public SingleUniverseTypeMap (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyTypeMap,
		IReadOnlyDictionary<string, Type>?[]? arrayMapsByRank)
		: this (typeMap, proxyTypeMap, arrayMapsByRank is null ? null : [arrayMapsByRank])
	{
	}

	public SingleUniverseTypeMap (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyTypeMap,
		IReadOnlyDictionary<string, Type>?[][]? arrayMapsByUniverseAndRank)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyTypeMap);
		_typeMap = typeMap;
		_proxyTypeMap = proxyTypeMap;
		_arrayMapsByUniverseAndRank = arrayMapsByUniverseAndRank ?? [];
	}

	public IEnumerable<Type> GetProxyTypes (string jniName)
	{
		if (!_typeMap.TryGetValue (jniName, out var mappedType)) {
			yield break;
		}

		// Fast path: non-alias entry
		if (mappedType.GetCustomAttribute<JavaPeerProxy> (inherit: false) is not null) {
			yield return mappedType;
			yield break;
		}

		// Slow path: alias holder — follow each alias key
		var aliases = mappedType.GetCustomAttribute<JavaPeerAliasesAttribute> (inherit: false);
		if (aliases is null) {
			yield break;
		}

		foreach (var key in aliases.Aliases) {
			if (_typeMap.TryGetValue (key, out var aliasEntryType) &&
				aliasEntryType.GetCustomAttribute<JavaPeerProxy> (inherit: false) is not null) {
				yield return aliasEntryType;
			}
		}
	}

	public bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType)
	{
		return _proxyTypeMap.TryGetValue (managedType, out proxyType);
	}

	public bool TryGetArrayProxyType (string jniName, int rankIndex, [NotNullWhen (true)] out Type? proxyType)
	{
		foreach (var arrayMapsByRank in _arrayMapsByUniverseAndRank) {
			if ((uint)rankIndex < (uint)arrayMapsByRank.Length &&
					arrayMapsByRank [rankIndex] is { } dict &&
					dict.TryGetValue (jniName, out proxyType)) {
				return true;
			}
		}

		proxyType = null;
		return false;
	}
}
