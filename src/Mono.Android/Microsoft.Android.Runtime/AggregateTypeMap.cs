#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

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

	public IEnumerable<JavaPeerProxy> GetProxies (string jniName)
	{
		foreach (var universe in _universes) {
			foreach (var proxy in universe.GetProxies (jniName)) {
				yield return proxy;
			}
		}
	}

	public bool TryGetProxy (Type managedType, [NotNullWhen (true)] out JavaPeerProxy? proxy)
	{
		// First-wins: each managed type exists in exactly one assembly
		foreach (var universe in _universes) {
			if (universe.TryGetProxy (managedType, out proxy)) {
				return true;
			}
		}
		proxy = null;
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
