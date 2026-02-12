using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// High-level API: builds the model from peers, then emits the PE assembly.
/// Composes <see cref="ModelBuilder"/> + <see cref="TypeMapAssemblyEmitter"/>.
/// </summary>
sealed class TypeMapAssemblyGenerator
{
	readonly Version _systemRuntimeVersion;

	/// <param name="systemRuntimeVersion">Version for System.Runtime assembly references.</param>
	public TypeMapAssemblyGenerator (Version systemRuntimeVersion)
	{
		_systemRuntimeVersion = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
	}

	/// <summary>
	/// Generates a TypeMap PE assembly from the given Java peer info records.
	/// </summary>
	/// <param name="peers">Scanned Java peer types.</param>
	/// <param name="outputPath">Path where the output .dll will be written.</param>
	/// <param name="assemblyName">Optional explicit assembly name. Derived from outputPath if null.</param>
	public void Generate (IReadOnlyList<JavaPeerInfo> peers, string outputPath, string? assemblyName = null)
	{
		var model = ModelBuilder.Build (peers, outputPath, assemblyName);
		var emitter = new TypeMapAssemblyEmitter (_systemRuntimeVersion);
		emitter.Emit (model, outputPath);
	}
}
