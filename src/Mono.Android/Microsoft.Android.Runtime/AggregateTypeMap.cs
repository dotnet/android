#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Wraps N <see cref="SingleUniverseTypeMap"/> instances and flattens
/// results across all universes. Debug-only — each assembly has its own
/// universe with an isolated <c>TypeMapLazyDictionary</c>.
/// </summary>
sealed class AggregateTypeMap : ITypeMap
{
	readonly SingleUniverseTypeMap[] _universes;

	public AggregateTypeMap (SingleUniverseTypeMap[] universes)
	{
		ArgumentNullException.ThrowIfNull (universes);
		_universes = universes;
	}

	public IEnumerable<Type> GetProxyTypes (string jniName)
	{
		foreach (var universe in _universes) {
			foreach (var type in universe.GetProxyTypes (jniName)) {
				yield return type;
			}
		}
	}

	public bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType)
	{
		// First-wins: each managed type exists in exactly one assembly
		foreach (var universe in _universes) {
			if (universe.TryGetProxyType (managedType, out proxyType)) {
				return true;
			}
		}
		proxyType = null;
		return false;
	}

	public bool TryGetArrayType (string jniName, int rankIndex, [NotNullWhen (true)] out Type? arrayType)
	{
		foreach (var universe in _universes) {
			if (universe.TryGetArrayType (jniName, rankIndex, out arrayType)) {
				return true;
			}
		}

		arrayType = null;
		return false;
	}
}
