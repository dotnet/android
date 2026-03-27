using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Core logic for generating trimmable TypeMap assemblies, JCW Java sources, and acw-map files.
/// Extracted from the MSBuild task so it can be tested directly without MSBuild ceremony.
/// </summary>
public class TrimmableTypeMapGenerator
{
	readonly Action<string> log;

	public TrimmableTypeMapGenerator (Action<string> log)
	{
		if (log is null) {
			throw new ArgumentNullException (nameof (log));
		}
		this.log = log;
	}

	/// <summary>
	/// Runs the full generation pipeline: scan assemblies, generate typemap
	/// assemblies, generate JCW Java sources, and write acw-map files.
	/// </summary>
	public TrimmableTypeMapResult Execute (
		IReadOnlyList<string> assemblyPaths,
		string outputDirectory,
		string javaSourceOutputDirectory,
		Version systemRuntimeVersion,
		HashSet<string> frameworkAssemblyNames)
	{
		if (assemblyPaths is null) {
			throw new ArgumentNullException (nameof (assemblyPaths));
		}
		if (outputDirectory is null) {
			throw new ArgumentNullException (nameof (outputDirectory));
		}
		if (javaSourceOutputDirectory is null) {
			throw new ArgumentNullException (nameof (javaSourceOutputDirectory));
		}
		if (systemRuntimeVersion is null) {
			throw new ArgumentNullException (nameof (systemRuntimeVersion));
		}
		if (frameworkAssemblyNames is null) {
			throw new ArgumentNullException (nameof (frameworkAssemblyNames));
		}

		Directory.CreateDirectory (outputDirectory);
		Directory.CreateDirectory (javaSourceOutputDirectory);

		var allPeers = ScanAssemblies (assemblyPaths);

		if (allPeers.Count == 0) {
			log ("No Java peer types found, skipping typemap generation.");
			return new TrimmableTypeMapResult ([], [], allPeers);
		}

		var generatedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion, assemblyPaths, outputDirectory);

		// Generate JCW .java files for user assemblies + framework Implementor types.
		// Framework binding types already have compiled JCWs in the SDK but their constructors
		// use the legacy TypeManager.Activate() JNI native which isn't available in the
		// trimmable runtime. Implementor types (View_OnClickListenerImplementor, etc.) are
		// in the mono.* Java package so we use the mono/ prefix to identify them.
		// We generate fresh JCWs that use Runtime.registerNatives() for activation.
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		log ($"Generating JCW files for {jcwPeers.Count} types (filtered from {allPeers.Count} total).");
		var generatedJavaFiles = GenerateJcwJavaSources (jcwPeers, javaSourceOutputDirectory);

		return new TrimmableTypeMapResult (generatedAssemblies, generatedJavaFiles, allPeers);
	}

	// Future optimization: the scanner currently scans all assemblies on every run.
	// For incremental builds, we could:
	// 1. Add a Scan(allPaths, changedPaths) overload that only produces JavaPeerInfo
	//    for changed assemblies while still indexing all assemblies for cross-assembly
	//    resolution (base types, interfaces, activation ctors).
	// 2. Cache scan results per assembly to skip PE I/O entirely for unchanged assemblies.
	// Both require profiling to determine if they meaningfully improve build times.
	List<JavaPeerInfo> ScanAssemblies (IReadOnlyList<string> assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);
		log ($"Scanned {assemblyPaths.Count} assemblies, found {peers.Count} Java peer types.");
		return peers;
	}

	List<string> GenerateTypeMapAssemblies (List<JavaPeerInfo> allPeers, Version systemRuntimeVersion,
		IReadOnlyList<string> assemblyPaths, string outputDir)
	{
		// Build a map from assembly name → source path for timestamp comparison
		var sourcePathByName = new Dictionary<string, string> (StringComparer.Ordinal);
		foreach (var path in assemblyPaths) {
			var name = Path.GetFileNameWithoutExtension (path);
			sourcePathByName [name] = path;
		}

		var peersByAssembly = allPeers
			.GroupBy (p => p.AssemblyName, StringComparer.Ordinal)
			.OrderBy (g => g.Key, StringComparer.Ordinal);

		var generatedAssemblies = new List<string> ();
		var perAssemblyNames = new List<string> ();
		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);
		bool anyRegenerated = false;

		foreach (var group in peersByAssembly) {
			string assemblyName = $"_{group.Key}.TypeMap";
			string outputPath = Path.Combine (outputDir, assemblyName + ".dll");
			perAssemblyNames.Add (assemblyName);

			if (IsUpToDate (outputPath, group.Key, sourcePathByName)) {
				log ($"  {assemblyName}: up to date, skipping");
				generatedAssemblies.Add (outputPath);
				continue;
			}

			var peers = group.ToList ();
			generator.Generate (peers, outputPath, assemblyName);
			generatedAssemblies.Add (outputPath);
			anyRegenerated = true;

			log ($"  {assemblyName}: {peers.Count} types");
		}

		// Root assembly references all per-assembly typemaps — regenerate if any changed
		string rootOutputPath = Path.Combine (outputDir, "_Microsoft.Android.TypeMaps.dll");
		if (anyRegenerated || !File.Exists (rootOutputPath)) {
			var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
			rootGenerator.Generate (perAssemblyNames, rootOutputPath);
			log ($"  Root: {perAssemblyNames.Count} per-assembly refs");
		} else {
			log ("  Root: up to date, skipping");
		}
		generatedAssemblies.Add (rootOutputPath);

		log ($"Generated {generatedAssemblies.Count} typemap assemblies.");
		return generatedAssemblies;
	}

	internal static bool IsUpToDate (string outputPath, string assemblyName, Dictionary<string, string> sourcePathByName)
	{
		if (!File.Exists (outputPath)) {
			return false;
		}
		if (!sourcePathByName.TryGetValue (assemblyName, out var sourcePath)) {
			return false;
		}
		return File.GetLastWriteTimeUtc (outputPath) >= File.GetLastWriteTimeUtc (sourcePath);
	}

	List<string> GenerateJcwJavaSources (List<JavaPeerInfo> allPeers, string javaSourceOutputDirectory)
	{
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var files = jcwGenerator.Generate (allPeers, javaSourceOutputDirectory);
		log ($"Generated {files.Count} JCW Java source files.");
		return files.ToList ();
	}

	public static Version ParseTargetFrameworkVersion (string tfv)
	{
		if (tfv.Length > 0 && (tfv [0] == 'v' || tfv [0] == 'V')) {
			tfv = tfv.Substring (1);
		}
		if (Version.TryParse (tfv, out var version)) {
			return version;
		}
		throw new ArgumentException ($"Cannot parse TargetFrameworkVersion '{tfv}' as a Version.");
	}
}
