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

	/// <summary>
	/// Runs the full generation pipeline: scan assemblies, generate typemap
	/// assemblies, generate JCW Java sources, optionally generate manifest and acw-map.
	/// </summary>
	public TrimmableTypeMapResult Execute (
		IReadOnlyList<(string Name, PEReader Reader)> assemblies,
		Version systemRuntimeVersion,
		HashSet<string> frameworkAssemblyNames,
		ManifestConfig? manifestConfig = null,
		string? manifestTemplatePath = null,
		string? mergedManifestOutputPath = null,
		string? acwMapOutputPath = null)
	{
		_ = assemblies ?? throw new ArgumentNullException (nameof (assemblies));
		_ = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
		_ = frameworkAssemblyNames ?? throw new ArgumentNullException (nameof (frameworkAssemblyNames));

		var (allPeers, assemblyManifestInfo) = ScanAssemblies (assemblies);
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

		string[]? additionalProviderSources = null;

		// Generate merged AndroidManifest.xml if requested
		if (!mergedManifestOutputPath.IsNullOrEmpty () && manifestConfig is not null) {
			var providerSources = GenerateManifest (allPeers, assemblyManifestInfo, manifestConfig, manifestTemplatePath, mergedManifestOutputPath);
			if (providerSources.Count > 0) {
				additionalProviderSources = providerSources.ToArray ();
			}
		}

		// Write merged acw-map.txt if requested
		if (!acwMapOutputPath.IsNullOrEmpty ()) {
			Directory.CreateDirectory (Path.GetDirectoryName (acwMapOutputPath));
			using (var writer = new StreamWriter (acwMapOutputPath)) {
				AcwMapWriter.Write (writer, allPeers);
			}
			log ($"Wrote merged acw-map.txt with {allPeers.Count} types to {acwMapOutputPath}.");
		}

		return new TrimmableTypeMapResult (generatedAssemblies, generatedJavaSources, allPeers, additionalProviderSources);
	}

	IList<string> GenerateManifest (List<JavaPeerInfo> allPeers, AssemblyManifestInfo assemblyManifestInfo,
		ManifestConfig config, string? manifestTemplatePath, string mergedManifestOutputPath)
	{
		string minSdk = "21";
		if (!config.SupportedOSPlatformVersion.IsNullOrEmpty () && Version.TryParse (config.SupportedOSPlatformVersion, out var sopv)) {
			minSdk = sopv.Major.ToString (System.Globalization.CultureInfo.InvariantCulture);
		}

		string targetSdk = config.AndroidApiLevel ?? "36";
		if (Version.TryParse (targetSdk, out var apiVersion)) {
			targetSdk = apiVersion.Major.ToString (System.Globalization.CultureInfo.InvariantCulture);
		}

		bool forceDebuggable = !config.CheckedBuild.IsNullOrEmpty ();

		var generator = new ManifestGenerator {
			PackageName = config.PackageName,
			ApplicationLabel = config.ApplicationLabel ?? config.PackageName,
			VersionCode = config.VersionCode ?? "",
			VersionName = config.VersionName ?? "",
			MinSdkVersion = minSdk,
			TargetSdkVersion = targetSdk,
			AndroidRuntime = config.AndroidRuntime ?? "coreclr",
			Debug = config.Debug,
			NeedsInternet = config.NeedsInternet,
			EmbedAssemblies = config.EmbedAssemblies,
			ForceDebuggable = forceDebuggable,
			ForceExtractNativeLibs = forceDebuggable,
			ManifestPlaceholders = config.ManifestPlaceholders,
			ApplicationJavaClass = config.ApplicationJavaClass,
		};

		return generator.Generate (manifestTemplatePath, allPeers, assemblyManifestInfo, mergedManifestOutputPath);
	}

	(List<JavaPeerInfo> peers, AssemblyManifestInfo manifestInfo) ScanAssemblies (IReadOnlyList<(string Name, PEReader Reader)> assemblies)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblies);
		var manifestInfo = scanner.ScanAssemblyManifestInfo ();
		log ($"Scanned {assemblies.Count} assemblies, found {peers.Count} Java peer types.");
		return (peers, manifestInfo);
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
