using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

static class MetadataHelper
{
	static readonly Guid GeneratorModuleVersionId = typeof (TypeMapAssemblyGenerator).Module.ModuleVersionId;

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
		using var stream = new MemoryStream ();
		using var writer = new BinaryWriter (stream, Encoding.UTF8);
		foreach (var entry in data.Entries) {
			writer.Write (entry.MapKey);
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
		return sha.ComputeHash (stream.GetBuffer (), 0, checked ((int) stream.Length));
	}

	/// <summary>
	/// Computes a fingerprint of every input that affects a generated per-assembly typemap.
	/// Unlike <see cref="ComputeContentFingerprint"/>, this is an incremental-build contract,
	/// so it includes the generator binary identity and all model fields consumed by the emitter.
	/// </summary>
	public static byte [] ComputeIncrementalFingerprint (TypeMapAssemblyData data, Version systemRuntimeVersion, bool useSharedTypemapUniverse)
	{
		using var sha = SHA256.Create ();
		using var stream = new MemoryStream ();
		using var writer = new BinaryWriter (stream, Encoding.UTF8);
		writer.Write (GeneratorModuleVersionId.ToByteArray ());
		writer.Write (systemRuntimeVersion.ToString ());
		writer.Write (useSharedTypemapUniverse);
		writer.Write (data.AssemblyName);
		writer.Write (data.ModuleName);
		writer.Write (data.Entries.Count);
		foreach (var entry in data.Entries) {
			writer.Write (entry.MapKey);
			writer.Write (entry.ProxyTypeReference);
			writer.WriteOptionalString (entry.TargetTypeReference);
		}
		writer.Write (data.ProxyTypes.Count);
		foreach (var proxy in data.ProxyTypes) {
			writer.Write (proxy.TypeName);
			writer.Write (proxy.JniName);
			writer.Write (proxy.Namespace);
			writer.WriteTypeRef (proxy.TargetType);
			writer.WriteOptionalTypeRef (proxy.InvokerType);
			writer.Write (proxy.InvokerActivationCtorStyle.HasValue);
			if (proxy.InvokerActivationCtorStyle.HasValue) {
				writer.Write ((byte) proxy.InvokerActivationCtorStyle.Value);
			}
			writer.WriteOptionalActivationCtor (proxy.ActivationCtor);
			writer.Write (proxy.IsGenericDefinition);
			writer.Write (proxy.CannotRegisterInStaticConstructor);
			writer.Write (proxy.IsAcw);
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
		writer.Write (data.Associations.Count);
		foreach (var assoc in data.Associations) {
			writer.Write (assoc.SourceTypeReference);
			writer.Write (assoc.AliasProxyTypeReference);
		}
		writer.Write (data.AliasHolders.Count);
		foreach (var holder in data.AliasHolders) {
			writer.Write (holder.TypeName);
			writer.Write (holder.Namespace);
			writer.Write (holder.AliasKeys.Count);
			foreach (var aliasKey in holder.AliasKeys) {
				writer.Write (aliasKey);
			}
		}
		writer.Write (data.IgnoresAccessChecksTo.Count);
		foreach (var assemblyName in data.IgnoresAccessChecksTo) {
			writer.Write (assemblyName);
		}
		writer.Flush ();
		return sha.ComputeHash (stream.GetBuffer (), 0, checked ((int) stream.Length));
	}

	/// <summary>
	/// Computes a fingerprint of every input that affects the root typemap assembly.
	/// </summary>
	public static byte [] ComputeRootIncrementalFingerprint (
		IReadOnlyList<string> perAssemblyTypeMapNames,
		Version systemRuntimeVersion,
		bool useSharedTypemapUniverse,
		IReadOnlyList<string>? sharedFrameworkTypeMapNames = null)
	{
		using var sha = SHA256.Create ();
		using var stream = new MemoryStream ();
		using var writer = new BinaryWriter (stream, Encoding.UTF8);
		writer.Write (GeneratorModuleVersionId.ToByteArray ());
		writer.Write (systemRuntimeVersion.ToString ());
		writer.Write (useSharedTypemapUniverse);
		writer.Write (perAssemblyTypeMapNames.Count);
		foreach (var assemblyName in perAssemblyTypeMapNames) {
			writer.Write (assemblyName);
		}
		writer.Write (sharedFrameworkTypeMapNames?.Count ?? 0);
		if (sharedFrameworkTypeMapNames is not null) {
			foreach (var assemblyName in sharedFrameworkTypeMapNames) {
				writer.Write (assemblyName);
			}
		}
		writer.Flush ();
		return sha.ComputeHash (stream.GetBuffer (), 0, checked ((int) stream.Length));
	}

	static void WriteTypeRef (this BinaryWriter writer, TypeRefData type)
	{
		writer.Write (type.ManagedTypeName);
		writer.Write (type.AssemblyName);
		writer.Write (type.IsValueType ? (byte) 1 : (byte) 0);
		writer.Write (type.IsEnum ? (byte) 1 : (byte) 0);
		writer.Write (type.GenericArguments.Count);
		foreach (var argument in type.GenericArguments) {
			writer.WriteTypeRef (argument);
		}
	}

	static void WriteOptionalTypeRef (this BinaryWriter writer, TypeRefData? type)
	{
		writer.Write (type is not null);
		if (type is not null) {
			writer.WriteTypeRef (type);
		}
	}

	static void WriteOptionalString (this BinaryWriter writer, string? value)
	{
		writer.Write (value is not null);
		if (value is not null) {
			writer.Write (value);
		}
	}

	static void WriteOptionalActivationCtor (this BinaryWriter writer, ActivationCtorData? constructor)
	{
		writer.Write (constructor is not null);
		if (constructor is not null) {
			writer.WriteTypeRef (constructor.DeclaringType);
			writer.Write (constructor.IsOnLeafType);
			writer.Write ((byte) constructor.Style);
		}
	}

	static void WriteUcoMethod (this BinaryWriter writer, UcoMethodData method)
	{
		writer.Write (method.WrapperName);
		writer.Write (method.CallbackMethodName);
		writer.WriteTypeRef (method.CallbackType);
		writer.Write (method.JniSignature);
		writer.WriteOptionalStrings (method.CallbackParameterTypeNames);
		writer.WriteOptionalString (method.CallbackReturnTypeName);
		writer.WriteExportMethodDispatch (method.ExportMethodDispatch);
	}

	static void WriteOptionalStrings (this BinaryWriter writer, IReadOnlyList<string>? values)
	{
		writer.Write (values is not null);
		if (values is null) {
			return;
		}
		writer.Write (values.Count);
		foreach (var value in values) {
			writer.Write (value);
		}
	}

	static void WriteExportMethodDispatch (this BinaryWriter writer, ExportMethodDispatchData? dispatch)
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

	static void WriteUcoConstructor (this BinaryWriter writer, UcoConstructorData constructor)
	{
		writer.Write (constructor.WrapperName);
		writer.WriteTypeRef (constructor.TargetType);
		writer.Write (constructor.JniSignature);
		writer.Write (constructor.HasMatchingManagedCtor);
		writer.Write (constructor.ManagedParameterTypes.Count);
		foreach (var parameterType in constructor.ManagedParameterTypes) {
			writer.WriteTypeRef (parameterType);
		}
		writer.Write (constructor.ParameterKinds.Count);
		foreach (var parameterKind in constructor.ParameterKinds) {
			writer.Write ((int) parameterKind);
		}
	}

	static void WriteNativeRegistration (this BinaryWriter writer, NativeRegistrationData registration)
	{
		writer.Write (registration.JniMethodName);
		writer.Write (registration.JniSignature);
		writer.Write (registration.WrapperMethodName);
		writer.Write (registration.WrapperTarget.TypeNamespace);
		writer.Write (registration.WrapperTarget.TypeName);
		writer.Write (registration.WrapperTarget.MethodName);
	}
}
