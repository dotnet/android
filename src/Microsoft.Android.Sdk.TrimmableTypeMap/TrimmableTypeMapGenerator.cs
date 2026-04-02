using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public class TrimmableTypeMapGenerator
{
	readonly Action<string> log;

	public TrimmableTypeMapGenerator (Action<string> log)
	{
		this.log = log ?? throw new ArgumentNullException (nameof (log));
	}

	public TrimmableTypeMapResult Execute (
		IReadOnlyList<(string Name, PEReader Reader)> assemblies,
		Version systemRuntimeVersion,
		HashSet<string> frameworkAssemblyNames)
	{
		_ = assemblies ?? throw new ArgumentNullException (nameof (assemblies));
		_ = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
		_ = frameworkAssemblyNames ?? throw new ArgumentNullException (nameof (frameworkAssemblyNames));

		var allPeers = ScanAssemblies (assemblies);
		if (allPeers.Count == 0) {
			log ("No Java peer types found, skipping typemap generation.");
			return new TrimmableTypeMapResult ([], [], allPeers);
		}

		var generatedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion);
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		log ($"Generating JCW files for {jcwPeers.Count} types (filtered from {allPeers.Count} total).");
		var generatedJavaSources = GenerateJcwJavaSources (jcwPeers);
		return new TrimmableTypeMapResult (generatedAssemblies, generatedJavaSources, allPeers);
	}

	List<JavaPeerInfo> ScanAssemblies (IReadOnlyList<(string Name, PEReader Reader)> assemblies)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblies);
		log ($"Scanned {assemblies.Count} assemblies, found {peers.Count} Java peer types.");
		return peers;
	}

	List<GeneratedAssembly> GenerateTypeMapAssemblies (List<JavaPeerInfo> allPeers, Version systemRuntimeVersion)
	{
		var peersByAssembly = allPeers.GroupBy (p => p.AssemblyName, StringComparer.Ordinal).OrderBy (g => g.Key, StringComparer.Ordinal);
		var generatedAssemblies = new List<GeneratedAssembly> ();
		var perAssemblyNames = new List<string> ();
		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);
		foreach (var group in peersByAssembly) {
			string assemblyName = $"_{group.Key}.TypeMap";
			perAssemblyNames.Add (assemblyName);
			var peers = group.ToList ();
			var stream = new MemoryStream ();
			generator.Generate (peers, stream, assemblyName);
			stream.Position = 0;
			generatedAssemblies.Add (new GeneratedAssembly (assemblyName, stream));
			log ($"  {assemblyName}: {peers.Count} types");
		}
		var rootStream = new MemoryStream ();
		var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
		rootGenerator.Generate (perAssemblyNames, rootStream);
		rootStream.Position = 0;
		generatedAssemblies.Add (new GeneratedAssembly ("_Microsoft.Android.TypeMaps", rootStream));
		log ($"  Root: {perAssemblyNames.Count} per-assembly refs");
		log ($"Generated {generatedAssemblies.Count} typemap assemblies.");
		return generatedAssemblies;
	}

	List<GeneratedJavaSource> GenerateJcwJavaSources (List<JavaPeerInfo> allPeers)
	{
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var sources = jcwGenerator.GenerateContent (allPeers);
		log ($"Generated {sources.Count} JCW Java source files.");
		return sources.ToList ();
	}
}
