using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class MetadataHelper
{
	/// <summary>
	/// Produces a deterministic MVID by hashing the module name together with content-dependent data.
	/// Assemblies with the same name but different content will have different MVIDs.
	/// </summary>
	public static Guid DeterministicMvid (string moduleName, ReadOnlySpan<byte> contentBytes = default)
	{
		using var sha = SHA256.Create ();
		byte [] nameBytes = Encoding.UTF8.GetBytes (moduleName);
		byte [] input = new byte [nameBytes.Length + contentBytes.Length];
		nameBytes.CopyTo (input, 0);
		contentBytes.CopyTo (input.AsSpan (nameBytes.Length));
		byte [] hash = sha.ComputeHash (input);
		byte [] guidBytes = new byte [16];
		Array.Copy (hash, guidBytes, 16);
		return new Guid (guidBytes);
	}

	/// <summary>
	/// Computes a content fingerprint for the given <see cref="TypeMapAssemblyData"/>.
	/// </summary>
	public static byte [] ComputeContentFingerprint (TypeMapAssemblyData data)
	{
		using var sha = SHA256.Create ();
		foreach (var entry in data.Entries) {
			AddString (sha, entry.JniName);
			AddString (sha, entry.ProxyTypeReference);
			AddString (sha, entry.TargetTypeReference ?? "");
		}
		foreach (var proxy in data.ProxyTypes) {
			AddString (sha, proxy.TypeName);
			AddString (sha, proxy.TargetType.ManagedTypeName);
			AddString (sha, proxy.TargetType.AssemblyName);
			AddByte (sha, (byte)(proxy.ActivationCtor?.Style ?? 0));
			AddByte (sha, (byte)(proxy.InvokerActivationCtorStyle ?? 0));
		}
		foreach (var assoc in data.Associations) {
			AddString (sha, assoc.SourceTypeReference);
			AddString (sha, assoc.AliasProxyTypeReference);
		}
		return FinishHash (sha);
	}

	public static byte [] ComputeRootContentFingerprint (Version systemRuntimeVersion, IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, int maxArrayRank)
	{
		using var sha = SHA256.Create ();
		AddVersion (sha, systemRuntimeVersion);
		AddByte (sha, useSharedTypemapUniverse ? (byte) 1 : (byte) 0);
		AddInt32 (sha, maxArrayRank);
		foreach (var name in perAssemblyTypeMapNames) {
			AddString (sha, name);
		}
		return FinishHash (sha);
	}

	static void AddString (HashAlgorithm hash, string value)
	{
		var bytes = Encoding.UTF8.GetBytes (value);
		AddInt32 (hash, bytes.Length);
		AddBytes (hash, bytes);
	}

	static void AddVersion (HashAlgorithm hash, Version version)
	{
		AddInt32 (hash, version.Major);
		AddInt32 (hash, version.Minor);
		AddInt32 (hash, version.Build);
		AddInt32 (hash, version.Revision);
	}

	static void AddInt32 (HashAlgorithm hash, int value)
	{
		byte [] bytes = [
			(byte) value,
			(byte) (value >> 8),
			(byte) (value >> 16),
			(byte) (value >> 24),
		];
		AddBytes (hash, bytes);
	}

	static void AddByte (HashAlgorithm hash, byte value)
	{
		AddBytes (hash, [value]);
	}

	static void AddBytes (HashAlgorithm hash, byte [] bytes)
	{
		if (bytes.Length != 0) {
			hash.TransformBlock (bytes, 0, bytes.Length, null, 0);
		}
	}

	static byte [] FinishHash (HashAlgorithm hash)
	{
		hash.TransformFinalBlock ([], 0, 0);
		if (hash.Hash is not null) {
			return hash.Hash;
		}
		throw new InvalidOperationException ("SHA256 did not produce a hash.");
	}
}
