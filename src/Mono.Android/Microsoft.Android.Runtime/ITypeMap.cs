#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Abstraction over a generated typemap universe.
/// Both Debug (per-assembly universes) and Release (single merged universe)
/// go through this interface, so <see cref="TrimmableTypeMap"/> doesn't need
/// to know about aliasing mechanics.
/// </summary>
interface ITypeMap
{
	/// <summary>
	/// Adds all proxy types mapped to a JNI name, resolving alias holders.
	/// </summary>
	void CollectProxyTypes (string jniName, ref JniProxyCacheBuilder builder);

	/// <summary>
	/// Resolves a managed type to its proxy type (the generated type that
	/// carries the <see cref="JavaPeerProxy"/> attribute).
	/// </summary>
	bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType);
}
