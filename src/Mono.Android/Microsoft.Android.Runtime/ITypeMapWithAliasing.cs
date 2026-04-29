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
	/// Returns all types mapped to a JNI name, resolving alias holders.
	/// For non-alias entries this yields a single type. For alias groups
	/// it follows each alias key and yields the surviving target types.
	/// </summary>
	IEnumerable<Type> GetTypes (string jniName);

	/// <summary>
	/// Resolves a managed type to its proxy type (the generated type that
	/// carries the <see cref="JavaPeerProxy"/> attribute).
	/// </summary>
	bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType);

	/// <summary>
	/// Looks up the closed managed array type for a given element JNI name and rank.
	/// E.g. <c>("java/lang/String", 2)</c> &#8594; <c>typeof(string[][])</c>.
	/// </summary>
	/// <param name="jniElementTypeName">
	/// The JNI name of the array element type (the bare element name, NOT the JNI array
	/// form — no leading <c>'['</c>). E.g. <c>"java/lang/String"</c>, not
	/// <c>"[Ljava/lang/String;"</c>.
	/// </param>
	/// <param name="rank">1-based array rank. Supported values: 1, 2, 3.</param>
	/// <param name="arrayType">The closed managed array type on success.</param>
	/// <returns>True when an entry exists for the (element, rank) pair.</returns>
	/// <remarks>
	/// Returns false when no per-rank dictionary was supplied at initialization
	/// (e.g. CoreCLR builds with <c>$(PublishAot) == false</c>) — the runtime fork
	/// in <c>JNIEnv.ArrayCreateInstance</c> short-circuits to <c>Array.CreateInstance</c>
	/// in that case so the lookup is never reached.
	/// </remarks>
	bool TryGetArrayType (string jniElementTypeName, int rank, [NotNullWhen (true)] out Type? arrayType);
}
