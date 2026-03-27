#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Core logic for generating trimmable TypeMap assemblies, JCW Java sources, and manifest.
/// Extracted from the MSBuild task so it can be tested directly without MSBuild ceremony.
/// </summary>
internal class TrimmableTypeMapGenerator
{
	readonly TaskLoggingHelper log;

	public TrimmableTypeMapGenerator (TaskLoggingHelper log)
	{
		this.log = log;
	}

	/// <summary>
	/// Runs the full generation pipeline: scan assemblies, generate typemap
	/// assemblies, generate JCW Java sources, and optionally generate the manifest.
	/// </summary>
	public TrimmableTypeMapResult Execute (
		IReadOnlyList<string> assemblyPaths,
		string outputDirectory,
		string javaSourceOutputDirectory,
		Version systemRuntimeVersion,
		HashSet<string> frameworkAssemblyNames,
		ManifestConfig? manifestConfig,
		string? manifestTemplatePath,
		string? mergedManifestOutputPath,
		string? acwMapOutputPath = null)
	{
		Directory.CreateDirectory (outputDirectory);
		Directory.CreateDirectory (javaSourceOutputDirectory);

		var (allPeers, assemblyManifestInfo) = ScanAssemblies (assemblyPaths);

		if (allPeers.Count == 0) {
			log.LogMessage (MessageImportance.Low, "No Java peer types found, skipping typemap generation.");
			return new TrimmableTypeMapResult ([], [], null);
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
		log.LogMessage (MessageImportance.Low, "Generating JCW files for {0} types (filtered from {1} total).", jcwPeers.Count, allPeers.Count);
		var generatedJavaFiles = GenerateJcwJavaSources (jcwPeers, javaSourceOutputDirectory);

		// Generate manifest if output path is configured
		string[]? additionalProviderSources = null;
		if (!mergedManifestOutputPath.IsNullOrEmpty () && manifestConfig is not null && !manifestConfig.PackageName.IsNullOrEmpty ()) {
			additionalProviderSources = GenerateManifest (allPeers, assemblyManifestInfo, manifestConfig, manifestTemplatePath, mergedManifestOutputPath);
		}

		// Write acw-map.txt so _ConvertCustomView and _UpdateAndroidResgen can resolve custom view names.
		if (!acwMapOutputPath.IsNullOrEmpty ()) {
			using var writer = new StreamWriter (acwMapOutputPath, append: false);
			AcwMapWriter.Write (writer, allPeers);
			log.LogMessage (MessageImportance.Low, "Written acw-map.txt with {0} entries to {1}.", allPeers.Count, acwMapOutputPath);
		}

		return new TrimmableTypeMapResult (generatedAssemblies, generatedJavaFiles, additionalProviderSources);
	}

	// Future optimization: the scanner currently scans all assemblies on every run.
	// For incremental builds, we could:
	// 1. Add a Scan(allPaths, changedPaths) overload that only produces JavaPeerInfo
	//    for changed assemblies while still indexing all assemblies for cross-assembly
	//    resolution (base types, interfaces, activation ctors).
	// 2. Cache scan results per assembly to skip PE I/O entirely for unchanged assemblies.
	// Both require profiling to determine if they meaningfully improve build times.
	(List<JavaPeerInfo> peers, AssemblyManifestInfo manifestInfo) ScanAssemblies (IReadOnlyList<string> assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);
		var manifestInfo = scanner.ScanAssemblyManifestInfo ();
		log.LogMessage (MessageImportance.Low, "Scanned {0} assemblies, found {1} Java peer types.", assemblyPaths.Count, peers.Count);
		return (peers, manifestInfo);
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
				log.LogMessage (MessageImportance.Low, "  {0}: up to date, skipping", assemblyName);
				generatedAssemblies.Add (outputPath);
				continue;
			}

			var peers = group.ToList ();
			generator.Generate (peers, outputPath, assemblyName);
			generatedAssemblies.Add (outputPath);
			anyRegenerated = true;

			log.LogMessage (MessageImportance.Low, "  {0}: {1} types", assemblyName, peers.Count);
		}

		// Root assembly references all per-assembly typemaps — regenerate if any changed
		string rootOutputPath = Path.Combine (outputDir, "_Microsoft.Android.TypeMaps.dll");
		if (anyRegenerated || !File.Exists (rootOutputPath)) {
			var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
			rootGenerator.Generate (perAssemblyNames, rootOutputPath);
			log.LogMessage (MessageImportance.Low, "  Root: {0} per-assembly refs", perAssemblyNames.Count);
		} else {
			log.LogMessage (MessageImportance.Low, "  Root: up to date, skipping");
		}
		generatedAssemblies.Add (rootOutputPath);

		log.LogMessage (MessageImportance.Low, "Generated {0} typemap assemblies.", generatedAssemblies.Count);
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
		log.LogMessage (MessageImportance.Low, "Generated {0} JCW Java source files.", files.Count);
		return files.ToList ();
	}

	string[]? GenerateManifest (List<JavaPeerInfo> allPeers, AssemblyManifestInfo assemblyManifestInfo,
		ManifestConfig config, string? manifestTemplatePath, string mergedManifestOutputPath)
	{
		// Validate components
		ValidateComponents (allPeers, assemblyManifestInfo);
		if (log.HasLoggedErrors) {
			return null;
		}

		string minSdk = "21";
		if (!config.SupportedOSPlatformVersion.IsNullOrEmpty () && Version.TryParse (config.SupportedOSPlatformVersion, out var sopv)) {
			minSdk = sopv.Major.ToString (CultureInfo.InvariantCulture);
		}

		string targetSdk = config.AndroidApiLevel ?? "36";
		if (Version.TryParse (targetSdk, out var apiVersion)) {
			targetSdk = apiVersion.Major.ToString (CultureInfo.InvariantCulture);
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

		var providerNames = generator.Generate (manifestTemplatePath, allPeers, assemblyManifestInfo, mergedManifestOutputPath);
		return providerNames.ToArray ();
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

	void ValidateComponents (List<JavaPeerInfo> allPeers, AssemblyManifestInfo assemblyManifestInfo)
	{
		// XA4213: component types must have a public parameterless constructor
		foreach (var peer in allPeers) {
			if (peer.ComponentAttribute is null || peer.IsAbstract) {
				continue;
			}
			if (!peer.ComponentAttribute.HasPublicDefaultConstructor) {
				log.LogError (null, "XA4213", null, null, 0, 0, 0, 0, Properties.Resources.XA4213, peer.ManagedTypeName);
			}
		}

		// Validate only one Application type
		var applicationTypes = new List<string> ();
		foreach (var peer in allPeers) {
			if (peer.ComponentAttribute?.Kind == ComponentKind.Application && !peer.IsAbstract) {
				applicationTypes.Add (peer.ManagedTypeName);
			}
		}

		bool hasAssemblyLevelApplication = assemblyManifestInfo.ApplicationProperties is not null;
		if (applicationTypes.Count > 1) {
			log.LogError (null, "XA4212", null, null, 0, 0, 0, 0, Properties.Resources.XA4212, string.Join (", ", applicationTypes));
		} else if (applicationTypes.Count > 0 && hasAssemblyLevelApplication) {
			log.LogError (null, "XA4217", null, null, 0, 0, 0, 0, Properties.Resources.XA4217);
		}
	}
}
