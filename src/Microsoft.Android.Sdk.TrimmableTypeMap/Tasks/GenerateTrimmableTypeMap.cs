using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Result of per-assembly trimmable typemap generation.
/// </summary>
sealed class TrimmableTypeMapResult
{
	/// <summary>
	/// Path to the generated TypeMap assembly.
	/// </summary>
	public string TypeMapAssemblyPath { get; set; } = "";

	/// <summary>
	/// Paths to generated JCW .java source files.
	/// </summary>
	public IReadOnlyList<string> GeneratedJavaSources { get; set; } = Array.Empty<string> ();
}

/// <summary>
/// Core logic for per-assembly trimmable typemap generation.
/// Scans a single assembly and generates:
///   - TypeMap assembly (.dll with TypeMapAttribute registrations)
///   - JCW Java source files (.java callable wrappers)
///   - Per-assembly acw-map fragment (acw-map.{AssemblyName}.txt)
///   - Component data (serialized component attribute info for manifest generation)
///
/// This is the logic class — MSBuild task wrapper lives in Xamarin.Android.Build.Tasks.
/// </summary>
static class TrimmableTypeMapGenerator
{
	/// <summary>
	/// Runs the full per-assembly pipeline: scan → generate TypeMap → generate JCW → write ACW map → serialize component data.
	/// </summary>
	public static TrimmableTypeMapResult Generate (
		string inputAssembly,
		IReadOnlyList<string>? referenceAssemblies,
		string typeMapOutputDirectory,
		string javaSourceOutputDirectory,
		string acwMapOutputPath,
		string componentDataOutputPath,
		Version systemRuntimeVersion)
	{
		var assemblyName = Path.GetFileNameWithoutExtension (inputAssembly);

		// Collect all assembly paths for scanning (input + references)
		var assemblyPaths = new List<string> { inputAssembly };
		if (referenceAssemblies != null) {
			foreach (var refAsm in referenceAssemblies) {
				assemblyPaths.Add (refAsm);
			}
		}

		// Scan all assemblies to build type hierarchy
		using var scanner = new JavaPeerScanner ();
		var allPeers = scanner.Scan (assemblyPaths);

		// Filter to peers from the input assembly only
		var assemblyPeers = new List<JavaPeerInfo> ();
		foreach (var peer in allPeers) {
			if (string.Equals (peer.AssemblyName, assemblyName, StringComparison.Ordinal)) {
				assemblyPeers.Add (peer);
			}
		}

		// Generate TypeMap assembly
		var typeMapOutputPath = Path.Combine (typeMapOutputDirectory, assemblyName + ".TypeMap.dll");
		EnsureDirectoryExists (typeMapOutputDirectory);

		var typeMapGenerator = new TypeMapAssemblyGenerator (systemRuntimeVersion);
		typeMapGenerator.Generate (assemblyPeers, typeMapOutputPath, assemblyName);

		// Generate JCW Java sources
		EnsureDirectoryExists (javaSourceOutputDirectory);
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var generatedJavaFiles = jcwGenerator.Generate (assemblyPeers, javaSourceOutputDirectory);

		// Generate per-assembly ACW map fragment
		var acwEntries = AcwMapWriter.CreateEntries (allPeers, assemblyName);
		EnsureDirectoryExists (Path.GetDirectoryName (acwMapOutputPath)!);

		using (var writer = new StreamWriter (acwMapOutputPath)) {
			foreach (var entry in acwEntries) {
				writer.Write (entry.PartialAssemblyQualifiedName);
				writer.Write (';');
				writer.WriteLine (entry.JavaKey);

				writer.Write (entry.ManagedKey);
				writer.Write (';');
				writer.WriteLine (entry.JavaKey);

				writer.Write (entry.CompatJniName);
				writer.Write (';');
				writer.WriteLine (entry.JavaKey);
			}
		}

		// Serialize component data for manifest generation
		EnsureDirectoryExists (Path.GetDirectoryName (componentDataOutputPath)!);
		ComponentDataSerializer.Serialize (assemblyPeers, componentDataOutputPath);

		return new TrimmableTypeMapResult {
			TypeMapAssemblyPath = typeMapOutputPath,
			GeneratedJavaSources = generatedJavaFiles,
		};
	}

	static void EnsureDirectoryExists (string? path)
	{
		if (!string.IsNullOrEmpty (path) && !Directory.Exists (path)) {
			Directory.CreateDirectory (path);
		}
	}
}

