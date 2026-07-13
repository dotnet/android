#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.Android.Runtime;

/// <summary>
/// An <see cref="ITypeMap"/> universe backed by a precompiled binary blob (see
/// <see cref="PrecompiledTypeMapBlobFormat"/>) blitted as RVA data into the generated root typemap
/// assembly. Replaces the CoreCLR <c>TypeMapping.GetOrCreate*TypeMapping</c> path, whose eager
/// dictionary materialization dominates trimmable-typemap startup.
///
/// Initialization is O(1): the instance only captures a pointer to the (immovable, image-mapped) blob,
/// its length, and the module used to resolve proxy metadata tokens. Nothing is parsed and no
/// <see cref="Type"/> is resolved until a lookup actually hits an entry, at which point the stored
/// <c>TypeRef</c> token is resolved lazily via <see cref="Module.ResolveType(int)"/> — loading only the
/// one referenced <c>_*.TypeMap.dll</c>, never every mapped assembly.
/// </summary>
sealed unsafe class PrecompiledTypeMap : ITypeMap
{
	readonly byte* _blob;
	readonly int _length;
	readonly Module _tokenModule;

	/// <param name="blob">Pointer to the universe blob (an RVA field address in the root module).</param>
	/// <param name="length">Length of the blob in bytes.</param>
	/// <param name="tokenModule">Module whose metadata tokens the blob's proxy tokens refer to.</param>
	public PrecompiledTypeMap (void* blob, int length, Module tokenModule)
	{
		ArgumentNullException.ThrowIfNull (blob);
		ArgumentOutOfRangeException.ThrowIfLessThan (length, PrecompiledTypeMapBlobFormat.HeaderSize);
		ArgumentNullException.ThrowIfNull (tokenModule);

		_blob = (byte*) blob;
		_length = length;
		_tokenModule = tokenModule;

		if (!PrecompiledTypeMapBlobFormat.IsValid (Blob)) {
			throw new ArgumentException ("Precompiled typemap blob has an invalid header or version.", nameof (blob));
		}
	}

	ReadOnlySpan<byte> Blob => new ReadOnlySpan<byte> (_blob, _length);

	public IEnumerable<Type> GetProxyTypes (string jniName)
	{
		ArgumentNullException.ThrowIfNull (jniName);

		// The blob (a ReadOnlySpan) must not be touched inside an iterator — C# forbids ref structs in
		// iterator blocks — so token lookup happens in a non-iterator helper that returns a plain array,
		// which the ResolveProxyTypes iterator then materializes.
		return ResolveProxyTypes (GetExternalProxyTokens (jniName));
	}

	/// <summary>
	/// UTF-8 overload of <see cref="GetProxyTypes(string)"/>. The blob stores JNI names as UTF-8 and
	/// hashes UTF-8 bytes, so a UTF-8 JNI name (e.g. handed straight from JNI) is matched with no
	/// string allocation or re-encoding. Not yet on <see cref="ITypeMap"/>; wiring the JNI retrieval and
	/// caches to feed UTF-8 is a follow-up.
	/// </summary>
	public IEnumerable<Type> GetProxyTypes (ReadOnlySpan<byte> jniNameUtf8) =>
		ResolveProxyTypes (GetExternalProxyTokens (jniNameUtf8));

	IEnumerable<Type> ResolveProxyTypes (int[]? tokens)
	{
		if (tokens is null) {
			yield break;
		}

		foreach (int token in tokens) {
			var proxyType = ResolveProxyType (token);
			if (proxyType is not null) {
				yield return proxyType;
			}
		}
	}

	int[]? GetExternalProxyTokens (string jniName)
	{
		var blob = Blob;
		if (!PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, jniName, out int tokenCount, out int tokensDataOffset)) {
			return null;
		}
		return ReadTokens (blob, tokenCount, tokensDataOffset);
	}

	int[]? GetExternalProxyTokens (ReadOnlySpan<byte> jniNameUtf8)
	{
		var blob = Blob;
		if (!PrecompiledTypeMapBlobFormat.TryGetExternalTokens (blob, jniNameUtf8, out int tokenCount, out int tokensDataOffset)) {
			return null;
		}
		return ReadTokens (blob, tokenCount, tokensDataOffset);
	}

	static int[] ReadTokens (ReadOnlySpan<byte> blob, int tokenCount, int tokensDataOffset)
	{
		var tokens = new int [tokenCount];
		for (int i = 0; i < tokenCount; i++) {
			tokens [i] = PrecompiledTypeMapBlobFormat.ReadTokenAt (blob, tokensDataOffset, i);
		}
		return tokens;
	}

	public bool TryGetProxyType (Type managedType, [NotNullWhen (true)] out Type? proxyType)
	{
		ArgumentNullException.ThrowIfNull (managedType);

		proxyType = null;

		string? assemblyQualifiedName = managedType.AssemblyQualifiedName;
		if (assemblyQualifiedName is null) {
			return false;
		}

		// Slice rather than substring so the UTF-8 encoding (stackalloc'd in the blob format) is the
		// only per-lookup work — no intermediate simplified-name string is allocated.
		ReadOnlySpan<char> key = GetSimplifiedAssemblyQualifiedTypeName (assemblyQualifiedName);
		if (!PrecompiledTypeMapBlobFormat.TryGetProxyToken (Blob, key, out int token)) {
			return false;
		}

		proxyType = ResolveProxyType (token);
		return proxyType is not null;
	}

	// Resolves a proxy TypeRef token embedded in the blob. The tokens index the root typemap module's
	// metadata, which is generated *after* ILLink from the already-linked proxy types and is not itself
	// re-trimmed, so the tokens are stable — the IL2026 "trimming changes metadata tokens" hazard does
	// not apply to this controlled, build-time-generated table.
	[UnconditionalSuppressMessage ("Trimming", "IL2026",
		Justification = "Blob tokens are TypeRefs into the post-ILLink-generated root typemap module, which is not re-trimmed; the referenced proxies were scanned from the linked assemblies and are preserved.")]
	Type? ResolveProxyType (int token) => _tokenModule.ResolveType (token);

	public bool TryGetArrayProxyType (string managedTypeKey, int rankIndex, [NotNullWhen (true)] out Type? proxyType)
	{
		// Array proxies are not yet precompiled into the blob. Callers fall back to the generic
		// array-creation path, matching behavior when a per-rank array map is absent.
		proxyType = null;
		return false;
	}

	// Mirrors ManagedTypeMapping.GetSimplifiedAssemblyQualifiedTypeName: keeps the full type name and
	// simple assembly name, dropping version/culture/public-key so the key matches the build-time key
	// "Namespace.Type, AssemblyName". Returns a slice to avoid allocating an intermediate string.
	static ReadOnlySpan<char> GetSimplifiedAssemblyQualifiedTypeName (string assemblyQualifiedName)
	{
		int commaIndex = assemblyQualifiedName.IndexOf (',');
		if (commaIndex < 0) {
			return assemblyQualifiedName.AsSpan ();
		}
		int secondCommaIndex = assemblyQualifiedName.IndexOf (',', commaIndex + 1);
		return secondCommaIndex < 0
			? assemblyQualifiedName.AsSpan ()
			: assemblyQualifiedName.AsSpan (0, secondCommaIndex);
	}
}
