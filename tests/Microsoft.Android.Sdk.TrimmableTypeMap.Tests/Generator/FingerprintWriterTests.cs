using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Sink = Microsoft.Android.Sdk.TrimmableTypeMap.FingerprintWriter.Sink;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// The fingerprints are computed by streaming model fields straight into SHA-256 instead of
/// buffering a <see cref="BinaryWriter"/> serialisation in a <see cref="MemoryStream"/>. These
/// tests pin that rewrite to the previous byte stream: the content fingerprint seeds the emitted
/// assembly's deterministic MVID and the incremental fingerprint is an incremental-build contract,
/// so neither may drift.
/// </summary>
public class FingerprintWriterTests : FixtureTestBase
{
	[Theory]
	[InlineData ("")]
	[InlineData ("a")]
	[InlineData ("Java.Lang.Object")]
	[InlineData ("caf\u00e9 \u00fcber \u65e5\u672c\u8a9e")]
	public void WriteString_MatchesBinaryWriter (string value)
	{
		AssertSameBytes (writer => writer.Write (value), writer => writer.WriteString (Sink.Content, value));
	}

	[Fact]
	public void WriteString_LongerThanInternalBuffers_MatchesBinaryWriter ()
	{
		// Exercises both the scratch-buffer growth and the direct-append path for values that
		// cannot fit into the sink buffer.
		foreach (int length in new [] { 100, 511, 512, 513, 8191, 8192, 8193, 40000 }) {
			var value = new string ('x', length);
			AssertSameBytes (writer => writer.Write (value), writer => writer.WriteString (Sink.Content, value));
		}
	}

	[Fact]
	public void WritePrimitives_MatchBinaryWriter ()
	{
		AssertSameBytes (writer => writer.Write (true), writer => writer.WriteBoolean (Sink.Content, true));
		AssertSameBytes (writer => writer.Write (false), writer => writer.WriteBoolean (Sink.Content, false));
		AssertSameBytes (writer => writer.Write ((byte) 0xAB), writer => writer.WriteByte (Sink.Content, 0xAB));
		AssertSameBytes (writer => writer.Write (int.MinValue), writer => writer.WriteInt32 (Sink.Content, int.MinValue));
		AssertSameBytes (writer => writer.Write (0x12345678), writer => writer.WriteInt32 (Sink.Content, 0x12345678));
		AssertSameBytes (writer => writer.Write (false), writer => writer.WriteOptionalString (Sink.Content, null));
		AssertSameBytes (
			writer => { writer.Write (true); writer.Write ("value"); },
			writer => writer.WriteOptionalString (Sink.Content, "value"));
		AssertSameBytes (
			writer => writer.Write (GeneratorModuleVersionId.ToByteArray ()),
			writer => writer.WriteRaw (Sink.Content, GeneratorModuleVersionId.ToByteArray ()));
	}

	[Fact]
	public void SinksAreIndependent ()
	{
		using var writer = new FingerprintWriter (includeIncremental: true);
		writer.WriteString (Sink.Both, "shared");
		writer.WriteString (Sink.Incremental, "incremental-only");
		writer.WriteInt32 (Sink.Content, 7);

		Assert.Equal (Sha256 (BinaryWriterBytes (w => { w.Write ("shared"); w.Write (7); })), writer.GetContentFingerprint ());
		Assert.Equal (Sha256 (BinaryWriterBytes (w => { w.Write ("shared"); w.Write ("incremental-only"); })), writer.GetIncrementalFingerprint ());
	}

	[Fact]
	public void GetIncrementalFingerprint_WhenNotRequested_Throws ()
	{
		using var writer = new FingerprintWriter (includeIncremental: false);
		Assert.Throws<InvalidOperationException> (() => writer.GetIncrementalFingerprint ());
	}

	[Fact]
	public void WritingToIncrementalSink_WhenNotRequested_Throws ()
	{
		// Silently dropping the write would yield a fingerprint that looks valid but covers
		// less than it claims to, so the writer must fail fast instead.
		foreach (var sink in new [] { Sink.Incremental, Sink.Both }) {
			using var writer = new FingerprintWriter (includeIncremental: false);
			Assert.Throws<InvalidOperationException> (() => writer.WriteString (sink, "value"));
			Assert.Throws<InvalidOperationException> (() => writer.WriteBoolean (sink, true));
			Assert.Throws<InvalidOperationException> (() => writer.WriteInt32 (sink, 1));
			Assert.Throws<InvalidOperationException> (() => writer.WriteRaw (sink, [1, 2, 3]));
			Assert.Equal (Sha256 ([]), writer.GetContentFingerprint ());
		}
	}

	[Fact]
	public void IncrementalSink_FlushesBufferedDataSpanningMultipleFlushes ()
	{
		// Exercises the incremental sink's buffer-flush path across the 8 KB boundary, both for
		// values that fit in the buffer and for one larger than it.
		using var writer = new FingerprintWriter (includeIncremental: true);
		var chunk = new string ('y', 3000);
		var oversized = new string ('z', 20000);
		writer.WriteString (Sink.Incremental, chunk);
		writer.WriteString (Sink.Incremental, chunk);
		writer.WriteString (Sink.Incremental, chunk);
		writer.WriteString (Sink.Incremental, oversized);
		writer.WriteString (Sink.Incremental, chunk);

		var expected = Sha256 (BinaryWriterBytes (w => {
			w.Write (chunk);
			w.Write (chunk);
			w.Write (chunk);
			w.Write (oversized);
			w.Write (chunk);
		}));
		Assert.Equal (expected, writer.GetIncrementalFingerprint ());
	}

	[Theory]
	[InlineData (true)]
	[InlineData (false)]
	public void ComputeFingerprints_MatchesLegacyBufferedSerialization (bool useSharedTypemapUniverse)
	{
		var systemRuntimeVersion = new Version (11, 0, 0, 0);
		var model = new TypeMapAssemblyGenerator (systemRuntimeVersion)
			.CreateModel (ScanFixtures (), "_TestFixtures.TypeMap");

		// The fixtures must exercise every part of the walk that the two fingerprints share.
		Assert.NotEmpty (model.Entries);
		Assert.NotEmpty (model.ProxyTypes);

		var fingerprints = MetadataHelper.ComputeFingerprints (model, systemRuntimeVersion, useSharedTypemapUniverse, includeIncremental: true);

		Assert.Equal (LegacyContentFingerprint (model), fingerprints.Content);
		Assert.Equal (LegacyIncrementalFingerprint (model, systemRuntimeVersion, useSharedTypemapUniverse), fingerprints.Incremental);
	}

	[Fact]
	public void ComputeFingerprints_WithoutIncremental_ProducesSameContentFingerprint ()
	{
		var systemRuntimeVersion = new Version (11, 0, 0, 0);
		var model = new TypeMapAssemblyGenerator (systemRuntimeVersion)
			.CreateModel (ScanFixtures (), "_TestFixtures.TypeMap");

		var withIncremental = MetadataHelper.ComputeFingerprints (model, systemRuntimeVersion, useSharedTypemapUniverse: true, includeIncremental: true);
		var contentOnly = MetadataHelper.ComputeFingerprints (model, systemRuntimeVersion, useSharedTypemapUniverse: true, includeIncremental: false);

		Assert.Equal (withIncremental.Content, contentOnly.Content);
		Assert.Null (contentOnly.Incremental);
		Assert.NotNull (withIncremental.Incremental);
	}

	[Fact]
	public void ComputeFingerprints_ContentIgnoresEmitterConfiguration ()
	{
		var systemRuntimeVersion = new Version (11, 0, 0, 0);
		var model = new TypeMapAssemblyGenerator (systemRuntimeVersion)
			.CreateModel (ScanFixtures (), "_TestFixtures.TypeMap");

		var shared = MetadataHelper.ComputeFingerprints (model, systemRuntimeVersion, useSharedTypemapUniverse: true, includeIncremental: true);
		var perAssembly = MetadataHelper.ComputeFingerprints (model, systemRuntimeVersion, useSharedTypemapUniverse: false, includeIncremental: true);

		// The content fingerprint only covers the model, the incremental one also covers config.
		Assert.Equal (shared.Content, perAssembly.Content);
		Assert.NotEqual (shared.Incremental, perAssembly.Incremental);
	}

	[Fact]
	public void ComputeRootIncrementalFingerprint_MatchesLegacyBufferedSerialization ()
	{
		var systemRuntimeVersion = new Version (11, 0, 0, 0);
		string [] names = ["_A.TypeMap", "_B.TypeMap"];

		var expected = Sha256 (BinaryWriterBytes (writer => {
			writer.Write (GeneratorModuleVersionId.ToByteArray ());
			writer.Write (systemRuntimeVersion.ToString ());
			writer.Write (true);
			writer.Write (names.Length);
			foreach (var name in names) {
				writer.Write (name);
			}
		}));

		Assert.Equal (expected, MetadataHelper.ComputeRootIncrementalFingerprint (names, systemRuntimeVersion, useSharedTypemapUniverse: true));
	}

	static Guid GeneratorModuleVersionId => typeof (TypeMapAssemblyGenerator).Module.ModuleVersionId;

	static void AssertSameBytes (Action<BinaryWriter> expected, Action<FingerprintWriter> actual)
	{
		Assert.Equal (Sha256 (BinaryWriterBytes (expected)), WriterBytes (actual));
	}

	static byte [] WriterBytes (Action<FingerprintWriter> write)
	{
		using var writer = new FingerprintWriter (includeIncremental: false);
		write (writer);
		return writer.GetContentFingerprint ();
	}

	static byte [] BinaryWriterBytes (Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream ();
		using var writer = new BinaryWriter (stream, Encoding.UTF8);
		write (writer);
		writer.Flush ();
		return stream.ToArray ();
	}

	static byte [] Sha256 (byte [] bytes)
	{
		using var sha = SHA256.Create ();
		return sha.ComputeHash (bytes);
	}

	// The implementations below are the buffered BinaryWriter serialisation used before the
	// streaming rewrite. They are intentionally verbatim copies so the tests fail if the new
	// walk changes the byte stream in any way.

	static byte [] LegacyContentFingerprint (TypeMapAssemblyData data) => Sha256 (BinaryWriterBytes (writer => {
		foreach (var entry in data.Entries) {
			writer.Write (entry.MapKey);
			writer.Write (entry.ProxyTypeReference);
			writer.Write (entry.TargetTypeReference ?? "");
		}
		foreach (var proxy in data.ProxyTypes) {
			writer.Write (proxy.TypeName);
			LegacyWriteTypeRef (writer, proxy.TargetType);
			writer.Write ((byte) (proxy.ActivationCtor?.Style ?? 0));
			if (proxy.ActivationCtor is not null) {
				LegacyWriteTypeRef (writer, proxy.ActivationCtor.DeclaringType);
			}
			writer.Write ((byte) (proxy.InvokerActivationCtorStyle ?? 0));
			writer.Write (proxy.UcoMethods.Count);
			foreach (var method in proxy.UcoMethods) {
				LegacyWriteUcoMethod (writer, method);
			}
			writer.Write (proxy.UcoConstructors.Count);
			foreach (var constructor in proxy.UcoConstructors) {
				LegacyWriteUcoConstructor (writer, constructor);
			}
			writer.Write (proxy.NativeRegistrations.Count);
			foreach (var registration in proxy.NativeRegistrations) {
				LegacyWriteNativeRegistration (writer, registration);
			}
		}
		foreach (var assoc in data.Associations) {
			writer.Write (assoc.SourceTypeReference);
			writer.Write (assoc.AliasProxyTypeReference);
		}
	}));

	static byte [] LegacyIncrementalFingerprint (TypeMapAssemblyData data, Version systemRuntimeVersion, bool useSharedTypemapUniverse) =>
		Sha256 (BinaryWriterBytes (writer => {
			writer.Write (GeneratorModuleVersionId.ToByteArray ());
			writer.Write (systemRuntimeVersion.ToString ());
			writer.Write (useSharedTypemapUniverse);
			writer.Write (data.AssemblyName);
			writer.Write (data.ModuleName);
			writer.Write (data.Entries.Count);
			foreach (var entry in data.Entries) {
				writer.Write (entry.MapKey);
				writer.Write (entry.ProxyTypeReference);
				LegacyWriteOptionalString (writer, entry.TargetTypeReference);
			}
			writer.Write (data.ProxyTypes.Count);
			foreach (var proxy in data.ProxyTypes) {
				writer.Write (proxy.TypeName);
				writer.Write (proxy.JniName);
				writer.Write (proxy.Namespace);
				LegacyWriteTypeRef (writer, proxy.TargetType);
				LegacyWriteOptionalTypeRef (writer, proxy.InvokerType);
				writer.Write (proxy.InvokerActivationCtorStyle.HasValue);
				if (proxy.InvokerActivationCtorStyle.HasValue) {
					writer.Write ((byte) proxy.InvokerActivationCtorStyle.Value);
				}
				LegacyWriteOptionalActivationCtor (writer, proxy.ActivationCtor);
				writer.Write (proxy.IsGenericDefinition);
				writer.Write (proxy.CannotRegisterInStaticConstructor);
				writer.Write (proxy.IsAcw);
				writer.Write (proxy.UcoMethods.Count);
				foreach (var method in proxy.UcoMethods) {
					LegacyWriteUcoMethod (writer, method);
				}
				writer.Write (proxy.UcoConstructors.Count);
				foreach (var constructor in proxy.UcoConstructors) {
					LegacyWriteUcoConstructor (writer, constructor);
				}
				writer.Write (proxy.NativeRegistrations.Count);
				foreach (var registration in proxy.NativeRegistrations) {
					LegacyWriteNativeRegistration (writer, registration);
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
		}));

	static void LegacyWriteTypeRef (BinaryWriter writer, TypeRefData type)
	{
		writer.Write (type.ManagedTypeName);
		writer.Write (type.AssemblyName);
		writer.Write (type.IsValueType ? (byte) 1 : (byte) 0);
		writer.Write (type.IsEnum ? (byte) 1 : (byte) 0);
		writer.Write (type.GenericArguments.Count);
		foreach (var argument in type.GenericArguments) {
			LegacyWriteTypeRef (writer, argument);
		}
	}

	static void LegacyWriteOptionalTypeRef (BinaryWriter writer, TypeRefData? type)
	{
		writer.Write (type is not null);
		if (type is not null) {
			LegacyWriteTypeRef (writer, type);
		}
	}

	static void LegacyWriteOptionalString (BinaryWriter writer, string? value)
	{
		writer.Write (value is not null);
		if (value is not null) {
			writer.Write (value);
		}
	}

	static void LegacyWriteOptionalActivationCtor (BinaryWriter writer, ActivationCtorData? constructor)
	{
		writer.Write (constructor is not null);
		if (constructor is not null) {
			LegacyWriteTypeRef (writer, constructor.DeclaringType);
			writer.Write (constructor.IsOnLeafType);
			writer.Write ((byte) constructor.Style);
		}
	}

	static void LegacyWriteUcoMethod (BinaryWriter writer, UcoMethodData method)
	{
		writer.Write (method.WrapperName);
		writer.Write (method.CallbackMethodName);
		LegacyWriteTypeRef (writer, method.CallbackType);
		writer.Write (method.JniSignature);
		LegacyWriteOptionalStrings (writer, method.CallbackParameterTypeNames);
		LegacyWriteOptionalString (writer, method.CallbackReturnTypeName);
		LegacyWriteExportMethodDispatch (writer, method.ExportMethodDispatch);
	}

	static void LegacyWriteOptionalStrings (BinaryWriter writer, IReadOnlyList<string>? values)
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

	static void LegacyWriteExportMethodDispatch (BinaryWriter writer, ExportMethodDispatchData? dispatch)
	{
		writer.Write (dispatch is not null);
		if (dispatch is null) {
			return;
		}
		writer.Write (dispatch.ManagedMethodName);
		writer.Write (dispatch.ParameterTypes.Count);
		foreach (var parameterType in dispatch.ParameterTypes) {
			LegacyWriteTypeRef (writer, parameterType);
		}
		writer.Write (dispatch.ParameterKinds.Count);
		foreach (var parameterKind in dispatch.ParameterKinds) {
			writer.Write ((int) parameterKind);
		}
		LegacyWriteTypeRef (writer, dispatch.ReturnType);
		writer.Write ((int) dispatch.ReturnKind);
		writer.Write (dispatch.IsStatic);
	}

	static void LegacyWriteUcoConstructor (BinaryWriter writer, UcoConstructorData constructor)
	{
		writer.Write (constructor.WrapperName);
		LegacyWriteTypeRef (writer, constructor.TargetType);
		writer.Write (constructor.JniSignature);
		writer.Write (constructor.HasMatchingManagedCtor);
		writer.Write (constructor.ManagedParameterTypes.Count);
		foreach (var parameterType in constructor.ManagedParameterTypes) {
			LegacyWriteTypeRef (writer, parameterType);
		}
		writer.Write (constructor.ParameterKinds.Count);
		foreach (var parameterKind in constructor.ParameterKinds) {
			writer.Write ((int) parameterKind);
		}
	}

	static void LegacyWriteNativeRegistration (BinaryWriter writer, NativeRegistrationData registration)
	{
		writer.Write (registration.JniMethodName);
		writer.Write (registration.JniSignature);
		writer.Write (registration.WrapperMethodName);
		writer.Write (registration.WrapperTarget.TypeNamespace);
		writer.Write (registration.WrapperTarget.TypeName);
		writer.Write (registration.WrapperTarget.MethodName);
	}
}
