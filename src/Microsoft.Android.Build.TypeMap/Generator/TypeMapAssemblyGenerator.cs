using System;
using System.Collections.Generic;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// High-level API: builds the model from peers, then emits the PE assembly.
/// Composes <see cref="ModelBuilder"/> + <see cref="TypeMapAssemblyEmitter"/>.
/// </summary>
sealed class TypeMapAssemblyGenerator
{
	/// <summary>
	/// Generates a TypeMap PE assembly from the given Java peer info records.
	/// </summary>
	/// <param name="peers">Scanned Java peer types.</param>
	/// <param name="outputPath">Path where the output .dll will be written.</param>
	/// <param name="assemblyName">Optional explicit assembly name. Derived from outputPath if null.</param>
	public void Generate (IReadOnlyList<JavaPeerInfo> peers, string outputPath, string? assemblyName = null)
	{
		var builder = new ModelBuilder ();
		var model = builder.Build (peers, outputPath, assemblyName);
		var emitter = new TypeMapAssemblyEmitter ();
		emitter.Emit (model, outputPath);
	}
}
