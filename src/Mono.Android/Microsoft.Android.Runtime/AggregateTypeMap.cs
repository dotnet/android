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
sealed class AggregateTypeMap : ITypeMapWithAliasing
{
	readonly SingleUniverseTypeMap[] _universes;

	public AggregateTypeMap (SingleUniverseTypeMap[] universes)
	{
		ArgumentNullException.ThrowIfNull (universes);
		_universes = universes;
	}

	public IEnumerable<Type> GetTypes (string jniName)
	{
		foreach (var universe in _universes) {
			foreach (var type in universe.GetTypes (jniName)) {
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

	public bool TryGetArrayType (string jniElementTypeName, int rank, [NotNullWhen (true)] out Type? arrayType)
	{
		// First-wins: each (peer, rank) pair has its TypeMap entry in exactly one
		// assembly. Walk the universes and stop at the first hit.
		foreach (var universe in _universes) {
			if (universe.TryGetArrayType (jniElementTypeName, rank, out arrayType)) {
				return true;
			}
		}
		arrayType = null;
		return false;
	}
}
