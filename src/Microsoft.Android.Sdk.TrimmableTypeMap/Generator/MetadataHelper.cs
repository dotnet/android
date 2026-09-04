using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Sink = Microsoft.Android.Sdk.TrimmableTypeMap.FingerprintWriter.Sink;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Fingerprints computed from a <see cref="TypeMapAssemblyData"/> model in a single walk.
/// </summary>
/// <param name="Content">
/// Content fingerprint that seeds the deterministic MVID of the emitted assembly.
/// </param>
/// <param name="Incremental">
/// Incremental-build fingerprint, or <see langword="null"/> when it was not requested.
/// </param>
readonly record struct ModelFingerprints (byte [] Content, byte []? Incremental);

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
	/// Computes the content fingerprint — and optionally the incremental-build fingerprint — for
	/// <paramref name="data"/> in a single walk over the model.
	/// </summary>
	/// <remarks>
	/// The content fingerprint covers only the model data that changes the emitted assembly's
	/// contents. The incremental fingerprint is an incremental-build contract, so it additionally
	/// covers the generator binary identity, the emitter configuration, and every model field the
	/// emitter consumes. Both are serialised from the same walk: fields shared by the two
	/// fingerprints are UTF-8 encoded once and appended to both hashes.
	/// </remarks>
	public static ModelFingerprints ComputeFingerprints (
		TypeMapAssemblyData data,
		Version systemRuntimeVersion,
		bool useSharedTypemapUniverse,
		bool includeIncremental)
	{
		using var writer = new FingerprintWriter (includeIncremental);
		var incremental = Sink.Incremental;
		var both = includeIncremental ? Sink.Both : Sink.Content;

		if (includeIncremental) {
			writer.WriteRaw (incremental, GeneratorModuleVersionId.ToByteArray ());
			writer.WriteString (incremental, systemRuntimeVersion.ToString ());
			writer.WriteBoolean (incremental, useSharedTypemapUniverse);
			writer.WriteString (incremental, data.AssemblyName);
			writer.WriteString (incremental, data.ModuleName);
			writer.WriteInt32 (incremental, data.Entries.Count);
		}

		foreach (var entry in data.Entries) {
			writer.WriteString (both, entry.MapKey);
			writer.WriteString (both, entry.ProxyTypeReference);
			writer.WriteString (Sink.Content, entry.TargetTypeReference ?? "");
			if (includeIncremental) {
				writer.WriteOptionalString (incremental, entry.TargetTypeReference);
			}
		}

		if (includeIncremental) {
			writer.WriteInt32 (incremental, data.ProxyTypes.Count);
		}
		foreach (var proxy in data.ProxyTypes) {
			writer.WriteString (both, proxy.TypeName);
			if (includeIncremental) {
				writer.WriteString (incremental, proxy.JniName);
				writer.WriteString (incremental, proxy.Namespace);
			}
			WriteTypeRef (writer, both, proxy.TargetType);
			if (includeIncremental) {
				WriteOptionalTypeRef (writer, incremental, proxy.InvokerType);
			}
			writer.WriteByte (Sink.Content, (byte) (proxy.ActivationCtor?.Style ?? 0));
			if (includeIncremental) {
				writer.WriteBoolean (incremental, proxy.InvokerActivationCtorStyle.HasValue);
				if (proxy.InvokerActivationCtorStyle.HasValue) {
					writer.WriteByte (incremental, (byte) proxy.InvokerActivationCtorStyle.Value);
				}
				writer.WriteBoolean (incremental, proxy.ActivationCtor is not null);
			}
			if (proxy.ActivationCtor is not null) {
				WriteTypeRef (writer, both, proxy.ActivationCtor.DeclaringType);
				if (includeIncremental) {
					writer.WriteBoolean (incremental, proxy.ActivationCtor.IsOnLeafType);
					writer.WriteByte (incremental, (byte) proxy.ActivationCtor.Style);
				}
			}
			writer.WriteByte (Sink.Content, (byte) (proxy.InvokerActivationCtorStyle ?? 0));
			if (includeIncremental) {
				writer.WriteBoolean (incremental, proxy.IsGenericDefinition);
				writer.WriteBoolean (incremental, proxy.CannotRegisterInStaticConstructor);
				writer.WriteBoolean (incremental, proxy.IsAcw);
			}
			writer.WriteInt32 (both, proxy.UcoMethods.Count);
			foreach (var method in proxy.UcoMethods) {
				WriteUcoMethod (writer, both, method);
			}
			writer.WriteInt32 (both, proxy.UcoConstructors.Count);
			foreach (var constructor in proxy.UcoConstructors) {
				WriteUcoConstructor (writer, both, constructor);
			}
			writer.WriteInt32 (both, proxy.NativeRegistrations.Count);
			foreach (var registration in proxy.NativeRegistrations) {
				WriteNativeRegistration (writer, both, registration);
			}
		}

		if (includeIncremental) {
			writer.WriteInt32 (incremental, data.Associations.Count);
		}
		foreach (var assoc in data.Associations) {
			writer.WriteString (both, assoc.SourceTypeReference);
			writer.WriteString (both, assoc.AliasProxyTypeReference);
		}

		if (includeIncremental) {
			writer.WriteInt32 (incremental, data.AliasHolders.Count);
			foreach (var holder in data.AliasHolders) {
				writer.WriteString (incremental, holder.TypeName);
				writer.WriteString (incremental, holder.Namespace);
				writer.WriteInt32 (incremental, holder.AliasKeys.Count);
				foreach (var aliasKey in holder.AliasKeys) {
					writer.WriteString (incremental, aliasKey);
				}
			}
			writer.WriteInt32 (incremental, data.IgnoresAccessChecksTo.Count);
			foreach (var assemblyName in data.IgnoresAccessChecksTo) {
				writer.WriteString (incremental, assemblyName);
			}
		}

		return new ModelFingerprints (
			writer.GetContentFingerprint (),
			includeIncremental ? writer.GetIncrementalFingerprint () : null);
	}

	/// <summary>
	/// Computes a fingerprint of every input that affects the root typemap assembly.
	/// </summary>
	public static byte [] ComputeRootIncrementalFingerprint (
		IReadOnlyList<string> perAssemblyTypeMapNames,
		Version systemRuntimeVersion,
		bool useSharedTypemapUniverse)
	{
		// This method needs only one hash. The content sink is used as the writer's always-present
		// sink; the returned value is still solely the incremental-build fingerprint for the root.
		using var writer = new FingerprintWriter (includeIncremental: false);
		writer.WriteRaw (Sink.Content, GeneratorModuleVersionId.ToByteArray ());
		writer.WriteString (Sink.Content, systemRuntimeVersion.ToString ());
		writer.WriteBoolean (Sink.Content, useSharedTypemapUniverse);
		writer.WriteInt32 (Sink.Content, perAssemblyTypeMapNames.Count);
		foreach (var assemblyName in perAssemblyTypeMapNames) {
			writer.WriteString (Sink.Content, assemblyName);
		}
		return writer.GetContentFingerprint ();
	}

	static void WriteTypeRef (FingerprintWriter writer, Sink sink, TypeRefData type)
	{
		writer.WriteString (sink, type.ManagedTypeName);
		writer.WriteString (sink, type.AssemblyName);
		writer.WriteByte (sink, type.IsValueType ? (byte) 1 : (byte) 0);
		writer.WriteByte (sink, type.IsEnum ? (byte) 1 : (byte) 0);
		writer.WriteInt32 (sink, type.GenericArguments.Count);
		foreach (var argument in type.GenericArguments) {
			WriteTypeRef (writer, sink, argument);
		}
	}

	static void WriteOptionalTypeRef (FingerprintWriter writer, Sink sink, TypeRefData? type)
	{
		writer.WriteBoolean (sink, type is not null);
		if (type is not null) {
			WriteTypeRef (writer, sink, type);
		}
	}

	static void WriteUcoMethod (FingerprintWriter writer, Sink sink, UcoMethodData method)
	{
		writer.WriteString (sink, method.WrapperName);
		writer.WriteString (sink, method.CallbackMethodName);
		WriteTypeRef (writer, sink, method.CallbackType);
		writer.WriteString (sink, method.JniSignature);
		WriteOptionalStrings (writer, sink, method.CallbackParameterTypeNames);
		writer.WriteOptionalString (sink, method.CallbackReturnTypeName);
		WriteExportMethodDispatch (writer, sink, method.ExportMethodDispatch);
	}

	static void WriteOptionalStrings (FingerprintWriter writer, Sink sink, IReadOnlyList<string>? values)
	{
		writer.WriteBoolean (sink, values is not null);
		if (values is null) {
			return;
		}
		writer.WriteInt32 (sink, values.Count);
		foreach (var value in values) {
			writer.WriteString (sink, value);
		}
	}

	static void WriteExportMethodDispatch (FingerprintWriter writer, Sink sink, ExportMethodDispatchData? dispatch)
	{
		writer.WriteBoolean (sink, dispatch is not null);
		if (dispatch is null) {
			return;
		}

		writer.WriteString (sink, dispatch.ManagedMethodName);
		writer.WriteInt32 (sink, dispatch.ParameterTypes.Count);
		foreach (var parameterType in dispatch.ParameterTypes) {
			WriteTypeRef (writer, sink, parameterType);
		}
		writer.WriteInt32 (sink, dispatch.ParameterKinds.Count);
		foreach (var parameterKind in dispatch.ParameterKinds) {
			writer.WriteInt32 (sink, (int) parameterKind);
		}
		WriteTypeRef (writer, sink, dispatch.ReturnType);
		writer.WriteInt32 (sink, (int) dispatch.ReturnKind);
		writer.WriteBoolean (sink, dispatch.IsStatic);
	}

	static void WriteUcoConstructor (FingerprintWriter writer, Sink sink, UcoConstructorData constructor)
	{
		writer.WriteString (sink, constructor.WrapperName);
		WriteTypeRef (writer, sink, constructor.TargetType);
		writer.WriteString (sink, constructor.JniSignature);
		writer.WriteBoolean (sink, constructor.HasMatchingManagedCtor);
		writer.WriteInt32 (sink, constructor.ManagedParameterTypes.Count);
		foreach (var parameterType in constructor.ManagedParameterTypes) {
			WriteTypeRef (writer, sink, parameterType);
		}
	}

	static void WriteNativeRegistration (FingerprintWriter writer, Sink sink, NativeRegistrationData registration)
	{
		writer.WriteString (sink, registration.JniMethodName);
		writer.WriteString (sink, registration.JniSignature);
		writer.WriteString (sink, registration.WrapperMethodName);
		writer.WriteString (sink, registration.WrapperTarget.TypeNamespace);
		writer.WriteString (sink, registration.WrapperTarget.TypeName);
		writer.WriteString (sink, registration.WrapperTarget.MethodName);
	}
}
