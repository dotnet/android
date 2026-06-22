using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class MetadataHelper
{
	/// <summary>
	/// Produces a deterministic MVID by hashing the module name together with content-dependent data.
	/// Assemblies with the same name but different content will have different MVIDs.
	/// </summary>
	public static Guid DeterministicMvid (string moduleName, ReadOnlySpan<byte> contentBytes = default)
	{
		using var hashBuilder = new DeterministicHashBuilder ();
		hashBuilder.AddString (moduleName);
		if (!contentBytes.IsEmpty) {
			hashBuilder.AddBytes (contentBytes);
		}
		byte [] hash = hashBuilder.ToHash ();
		byte [] guidBytes = new byte [16];
		Array.Copy (hash, guidBytes, 16);
		return new Guid (guidBytes);
	}

	/// <summary>
	/// Computes a content fingerprint for the given <see cref="TypeMapAssemblyData"/>.
	/// </summary>
	public static byte [] ComputeContentFingerprint (TypeMapAssemblyData data)
	{
		using var hash = new DeterministicHashBuilder ();
		foreach (var entry in data.Entries) {
			hash.AddString (entry.JniName);
			hash.AddString (entry.ProxyTypeReference);
			hash.AddString (entry.TargetTypeReference ?? "");
		}
		foreach (var proxy in data.ProxyTypes) {
			hash.AddString (proxy.TypeName);
			hash.AddString (proxy.TargetType.ManagedTypeName);
			hash.AddString (proxy.TargetType.AssemblyName);
			hash.AddByte ((byte)(proxy.ActivationCtor?.Style ?? 0));
			hash.AddByte ((byte)(proxy.InvokerActivationCtorStyle ?? 0));
		}
		foreach (var assoc in data.Associations) {
			hash.AddString (assoc.SourceTypeReference);
			hash.AddString (assoc.AliasProxyTypeReference);
		}
		return hash.ToHash ();
	}

	public static byte [] ComputeRootContentFingerprint (Version systemRuntimeVersion, IReadOnlyList<string> perAssemblyTypeMapNames, bool useSharedTypemapUniverse, int maxArrayRank)
	{
		using var hash = new DeterministicHashBuilder ();
		hash.AddVersion (systemRuntimeVersion);
		hash.AddByte (useSharedTypemapUniverse ? (byte) 1 : (byte) 0);
		hash.AddInt32 (maxArrayRank);
		foreach (var name in perAssemblyTypeMapNames) {
			hash.AddString (name);
		}
		return hash.ToHash ();
	}
}
