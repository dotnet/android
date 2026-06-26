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
			writer.Write (proxy.UcoMethods.Count);
			foreach (var method in proxy.UcoMethods) {
				writer.WriteUcoMethod (method);
			}
			writer.Write (proxy.UcoConstructors.Count);
			foreach (var constructor in proxy.UcoConstructors) {
				writer.WriteUcoConstructor (constructor);
			}
			writer.Write (proxy.NativeRegistrations.Count);
			foreach (var registration in proxy.NativeRegistrations) {
				writer.WriteNativeRegistration (registration);
			}
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

	static void WriteUcoMethod (this System.IO.BinaryWriter writer, UcoMethodData method)
	{
		writer.Write (method.WrapperName);
		writer.Write (method.CallbackMethodName);
		writer.WriteTypeRef (method.CallbackType);
		writer.Write (method.JniSignature);
		writer.WriteExportMethodDispatch (method.ExportMethodDispatch);
	}

	static void WriteExportMethodDispatch (this System.IO.BinaryWriter writer, ExportMethodDispatchData? dispatch)
	{
		writer.Write (dispatch is not null);
		if (dispatch is null) {
			return;
		}

		writer.Write (dispatch.ManagedMethodName);
		writer.Write (dispatch.ParameterTypes.Count);
		foreach (var parameterType in dispatch.ParameterTypes) {
			writer.WriteTypeRef (parameterType);
		}
		writer.Write (dispatch.ParameterKinds.Count);
		foreach (var parameterKind in dispatch.ParameterKinds) {
			writer.Write ((int) parameterKind);
		}
		writer.WriteTypeRef (dispatch.ReturnType);
		writer.Write ((int) dispatch.ReturnKind);
		writer.Write (dispatch.IsStatic);
	}

	static void WriteUcoConstructor (this System.IO.BinaryWriter writer, UcoConstructorData constructor)
	{
		writer.Write (constructor.WrapperName);
		writer.WriteTypeRef (constructor.TargetType);
		writer.Write (constructor.JniSignature);
		writer.Write (constructor.HasMatchingManagedCtor);
		writer.Write (constructor.ManagedParameterTypes.Count);
		foreach (var parameterType in constructor.ManagedParameterTypes) {
			writer.WriteTypeRef (parameterType);
		}
	}

	static void WriteNativeRegistration (this System.IO.BinaryWriter writer, NativeRegistrationData registration)
	{
		writer.Write (registration.JniMethodName);
		writer.Write (registration.JniSignature);
		writer.Write (registration.WrapperMethodName);
		writer.Write (registration.WrapperTarget.TypeNamespace);
		writer.Write (registration.WrapperTarget.TypeName);
		writer.Write (registration.WrapperTarget.MethodName);
	}
}
