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

	// Jagged: [rank-1][source]. Empty/null inner array → no entries for that rank.
	// Aggregate path uses inner-length 1 (one source per universe per rank); shared+arrays
	// path uses inner-length N (N per-asm dicts merged via first-hit walk).
	readonly IReadOnlyDictionary<string, Type>?[]?[] _arrayMapsByRank;

	public SingleUniverseTypeMap (IReadOnlyDictionary<string, Type> typeMap, IReadOnlyDictionary<Type, Type> proxyTypeMap)
		: this (typeMap, proxyTypeMap, arrayMapsByRank: null)
	{
	}

	public SingleUniverseTypeMap (
		IReadOnlyDictionary<string, Type> typeMap,
		IReadOnlyDictionary<Type, Type> proxyTypeMap,
		IReadOnlyDictionary<string, Type>?[]?[]? arrayMapsByRank)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		ArgumentNullException.ThrowIfNull (proxyTypeMap);
		_typeMap = typeMap;
		_proxyTypeMap = proxyTypeMap;
		_arrayMapsByRank = arrayMapsByRank ?? Array.Empty<IReadOnlyDictionary<string, Type>?[]?> ();
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
		int index = rank - 1;
		if ((uint)index < (uint)_arrayMapsByRank.Length) {
			var sources = _arrayMapsByRank [index];
			if (sources is not null) {
				foreach (var dict in sources) {
					if (dict is not null && dict.TryGetValue (jniElementTypeName, out arrayType)) {
						return true;
					}
				}
			}
		}
		arrayType = null;
		return false;
	}
}
