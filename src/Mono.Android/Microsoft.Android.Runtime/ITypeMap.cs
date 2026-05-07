#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Android.Runtime;

/// <summary>
/// Abstraction over a generated typemap universe.
/// Both Debug (per-assembly universes) and Release (single merged universe)
/// go through this interface, so <see cref="TrimmableTypeMap"/> doesn't need
/// to know about aliasing mechanics or per-rank array map storage.
/// </summary>
interface ITypeMap
{
	/// <summary>
	/// Returns all proxy types mapped to a JNI name, resolving alias holders.
	/// </summary>
	IEnumerable<Type> GetProxyTypes (string jniName);

	/// <summary>
	/// Resolves a managed type to its proxy type (the generated type that
	/// carries the <see cref="JavaPeerProxy"/> attribute).
	/// </summary>
	bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType);

	/// <summary>
	/// Resolves a JNI leaf name and 0-based array rank index to a managed array type.
	/// </summary>
	bool TryGetArrayType (string jniName, int rankIndex, [NotNullWhen (true)] out Type? arrayType);
}
