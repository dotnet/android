using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// High-level API: builds the model from peers, then emits the PE assembly.
/// Composes <see cref="ModelBuilder"/> + <see cref="TypeMapAssemblyEmitter"/>.
/// </summary>
public sealed class TypeMapAssemblyGenerator
{
	readonly Version _systemRuntimeVersion;

	/// <param name="systemRuntimeVersion">Version for System.Runtime assembly references.</param>
	public TypeMapAssemblyGenerator (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Generates a TypeMap PE assembly from the given Java peer info records and writes it to <paramref name="stream"/>.
	/// </summary>
	/// <param name="peers">Scanned Java peer types.</param>
	/// <param name="stream">Stream to write the output PE assembly to.</param>
	/// <param name="assemblyName">Assembly name for the generated assembly.</param>
	/// <param name="useSharedTypemapUniverse">
	/// When true, uses <c>Java.Lang.Object</c> as the shared anchor type. When false, emits a per-assembly anchor.
	/// </param>
	public void Generate (IReadOnlyList<JavaPeerInfo> peers, Stream stream, string assemblyName, bool useSharedTypemapUniverse = false)
	{
		var model = CreateModel (peers, assemblyName);
		Generate (model, stream, useSharedTypemapUniverse);
	}

	internal TypeMapAssemblyData CreateModel (IReadOnlyList<JavaPeerInfo> peers, string assemblyName)
	{
		return ModelBuilder.Build (peers, assemblyName + ".dll", assemblyName);
	}

	/// <summary>
	/// Computes the content fingerprint — and, when <paramref name="includeIncremental"/> is
	/// <see langword="true"/>, the incremental-build fingerprint — in a single walk over the model.
	/// The content fingerprint should be passed back to
	/// <see cref="Generate(TypeMapAssemblyData, Stream, bool, byte[])"/> so the model is not walked twice.
	/// </summary>
	internal ModelFingerprints ComputeFingerprints (TypeMapAssemblyData model, bool useSharedTypemapUniverse, bool includeIncremental)
	{
		return MetadataHelper.ComputeFingerprints (model, _systemRuntimeVersion, useSharedTypemapUniverse, includeIncremental);
	}

	internal void Generate (TypeMapAssemblyData model, Stream stream, bool useSharedTypemapUniverse, byte []? contentFingerprint = null)
	{
		var emitter = new TypeMapAssemblyEmitter (_systemRuntimeVersion);
		emitter.Emit (model, stream, useSharedTypemapUniverse, contentFingerprint);
	}

	/// <summary>
	/// Generates the PE assembly and returns a read-only stream over the serialised image without
	/// copying it into a second buffer.
	/// </summary>
	internal Stream GenerateToStream (TypeMapAssemblyData model, bool useSharedTypemapUniverse, byte []? contentFingerprint = null)
	{
		var emitter = new TypeMapAssemblyEmitter (_systemRuntimeVersion);
		return emitter.EmitToStream (model, useSharedTypemapUniverse, contentFingerprint);
	}

	/// <summary>
	/// Emits an empty typemap assembly (containing no type map entries) with the given
	/// <paramref name="assemblyName"/>, writing it to <paramref name="stream"/>. Used to satisfy
	/// <c>[assembly: TypeMapAssemblyTarget&lt;T&gt;("name")]</c> references to per-assembly typemaps
	/// that the trimmer removed (their target Java binding was unused): the runtime can still
	/// <c>Assembly.Load</c> the stub, which contributes no mappings, instead of throwing
	/// <see cref="System.IO.FileNotFoundException"/>.
	/// </summary>
	/// <param name="stream">Stream to write the output PE assembly to.</param>
	/// <param name="assemblyName">Assembly name for the generated stub.</param>
	public void GenerateEmpty (Stream stream, string assemblyName)
	{
		var builder = new PEAssemblyBuilder (_systemRuntimeVersion);
		builder.EmitPreamble (assemblyName, assemblyName + ".dll");
		builder.WritePE (stream);
	}
}
