using System;
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
		using var stream = new System.IO.MemoryStream ();
		using var writer = new System.IO.BinaryWriter (stream, Encoding.UTF8);
		foreach (var entry in data.Entries) {
			writer.Write (entry.JniName);
			writer.Write (entry.ProxyTypeReference);
			writer.Write (entry.TargetTypeReference ?? "");
		}
		foreach (var proxy in data.ProxyTypes) {
			writer.Write (proxy.TypeName);
			writer.WriteTypeRef (proxy.TargetType);
			writer.Write ((byte)(proxy.ActivationCtor?.Style ?? 0));
			if (proxy.ActivationCtor is not null) {
				writer.WriteTypeRef (proxy.ActivationCtor.DeclaringType);
			}
			writer.Write ((byte)(proxy.InvokerActivationCtorStyle ?? 0));
		}
		foreach (var assoc in data.Associations) {
			writer.Write (assoc.SourceTypeReference);
			writer.Write (assoc.AliasProxyTypeReference);
		}
		writer.Flush ();
		return sha.ComputeHash (stream.ToArray ());
	}

	static void WriteTypeRef (this System.IO.BinaryWriter writer, TypeRefData type)
	{
		writer.Write (type.ManagedTypeName);
		writer.Write (type.AssemblyName);
		writer.Write (type.IsEnum ? (byte) 1 : (byte) 0);
		writer.Write (type.GenericArguments.Count);
		foreach (var argument in type.GenericArguments) {
			writer.WriteTypeRef (argument);
		}
	}
}
