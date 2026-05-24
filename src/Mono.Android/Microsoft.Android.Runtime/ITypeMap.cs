#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop;

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
	/// Returns all proxies mapped to a JNI name, resolving alias holders.
	/// </summary>
	IEnumerable<JavaPeerProxy> GetProxies (string jniName);

	/// <summary>
	/// Resolves a managed type to its proxy.
	/// </summary>
	bool TryGetProxy (Type managedType, [NotNullWhen (true)] out JavaPeerProxy? proxy);

	/// <summary>
	/// Resolves a JNI leaf name and 0-based array rank index to a managed array type.
	/// </summary>
	bool TryGetArrayType (string jniName, int rankIndex, [NotNullWhen (true)] out Type? arrayType);
}
