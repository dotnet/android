#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Wraps N <see cref="ITypeMap"/> instances and flattens results across all universes.
/// Used when each assembly has its own universe (per-assembly universes) — the aggregated
/// universes may be <see cref="SingleUniverseTypeMap"/> or <see cref="PrecompiledTypeMap"/>.
/// </summary>
sealed class AggregateTypeMap : ITypeMap
{
	readonly ITypeMap[] _universes;

	public AggregateTypeMap (ITypeMap[] universes)
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

	public bool TryGetArrayProxyType (string managedTypeKey, int rankIndex, [NotNullWhen (true)] out Type? proxyType)
	{
		foreach (var universe in _universes) {
			if (universe.TryGetArrayProxyType (managedTypeKey, rankIndex, out proxyType)) {
				return true;
			}
		}

		proxyType = null;
		return false;
	}
}
