#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Abstraction over the typemap dictionary that handles alias resolution.
/// Both Debug (per-assembly universes) and Release (single merged universe)
/// go through this interface, so <see cref="TrimmableTypeMap"/> doesn't
/// need to know about aliasing mechanics.
/// </summary>
interface ITypeMapWithAliasing
{
	/// <summary>
	/// Returns all managed target types mapped to a JNI name, resolving alias holders.
	/// Entries backed by generated proxy types return the proxy's target type.
	/// </summary>
	IEnumerable<Type> GetTargetTypes (string jniName);

	/// <summary>
	/// Returns generated proxy types mapped to a JNI name, resolving alias holders.
	/// Entries without generated proxies are ignored.
	/// </summary>
	IEnumerable<Type> GetProxyTypes (string jniName);

	/// <summary>
	/// Resolves a managed type to its proxy type (the generated type that
	/// carries the <see cref="JavaPeerProxy"/> attribute).
	/// </summary>
	bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType);
}
