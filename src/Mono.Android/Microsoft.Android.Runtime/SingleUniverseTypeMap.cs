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

	// Per-rank array dictionaries (1-indexed; index 0 unused). Null when the typemap
	// universe was generated without array entries (e.g. CoreCLR builds with
	// $(PublishAot)==false). Only consulted under NativeAOT via TryGetArrayType.
	readonly IReadOnlyDictionary<string, Type>?[] _arrayMaps = new IReadOnlyDictionary<string, Type>? [4];

	public SingleUniverseTypeMap (IReadOnlyDictionary<string, Type> typeMap, IReadOnlyDictionary<Type, Type> proxyTypeMap)
		: this (typeMap, proxyTypeMap, null, null, null)
	{
	}

	public SingleUniverseTypeMap (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyTypeMap,
		IReadOnlyDictionary<string, Type>? arrayMapRank1,
		IReadOnlyDictionary<string, Type>? arrayMapRank2,
		IReadOnlyDictionary<string, Type>? arrayMapRank3)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyTypeMap);
		_typeMap = typeMap;
		_proxyTypeMap = proxyTypeMap;
		_arrayMaps [1] = arrayMapRank1;
		_arrayMaps [2] = arrayMapRank2;
		_arrayMaps [3] = arrayMapRank3;
	}

	public IEnumerable<Type> GetTypes (string jniName)
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

	public bool TryGetArrayType (string jniElementTypeName, int rank, [NotNullWhen (true)] out Type? arrayType)
	{
		if (rank >= 1 && rank <= 3) {
			var dict = _arrayMaps [rank];
			if (dict is not null && dict.TryGetValue (jniElementTypeName, out arrayType)) {
				return true;
			}
		}
		arrayType = null;
		return false;
	}
}
