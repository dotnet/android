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
	/// <param name="maxArrayRank">Max rank for per-rank array <c>TypeMap</c> entries. 0 disables.</param>
	public void Generate (IReadOnlyList<JavaPeerInfo> peers, Stream stream, string assemblyName, bool useSharedTypemapUniverse = false, int maxArrayRank = 0)
	{
		var model = ModelBuilder.Build (peers, assemblyName + ".dll", assemblyName, maxArrayRank);
		var emitter = new TypeMapAssemblyEmitter (_systemRuntimeVersion);
		emitter.Emit (model, stream, useSharedTypemapUniverse);
	}
}
