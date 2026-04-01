using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public class TrimmableTypeMapGenerator
{
	readonly ITrimmableTypeMapLogger logger;

	public TrimmableTypeMapGenerator (ITrimmableTypeMapLogger logger)
	{
		this.logger = logger ?? throw new ArgumentNullException (nameof (logger));
	}

	/// <summary>
	/// Runs the full generation pipeline: scan assemblies, generate typemap
	/// assemblies, generate JCW Java sources, and optionally generate a merged manifest.
	/// No file IO is performed — all results are returned in memory.
	/// </summary>
	public TrimmableTypeMapResult Execute (
		IReadOnlyList<(string Name, PEReader Reader)> assemblies,
		Version systemRuntimeVersion,
		HashSet<string> frameworkAssemblyNames,
		ManifestConfig? manifestConfig = null,
		XDocument? manifestTemplate = null)
	{
		_ = assemblies ?? throw new ArgumentNullException (nameof (assemblies));
		_ = systemRuntimeVersion ?? throw new ArgumentNullException (nameof (systemRuntimeVersion));
		_ = frameworkAssemblyNames ?? throw new ArgumentNullException (nameof (frameworkAssemblyNames));

		var (allPeers, assemblyManifestInfo) = ScanAssemblies (assemblies);
		if (allPeers.Count == 0) {
			logger.LogNoJavaPeerTypesFound ();
			return new TrimmableTypeMapResult ([], [], allPeers);
		}

		RootManifestReferencedTypes (allPeers, manifestTemplate);

		var generatedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion);
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		logger.LogGeneratingJcwFilesInfo (jcwPeers.Count, allPeers.Count);
		var generatedJavaSources = GenerateJcwJavaSources (jcwPeers);

		// Collect Application/Instrumentation types that need deferred registerNatives
		var appRegTypes = allPeers
			.Where (p => p.CannotRegisterInStaticConstructor && !p.IsAbstract)
			.Select (p => JniSignatureHelper.JniNameToJavaName (p.JavaName))
			.ToList ();
		if (appRegTypes.Count > 0) {
			logger.LogDeferredRegistrationTypesInfo (appRegTypes.Count);
		}

		var manifest = manifestConfig is not null
			? GenerateManifest (allPeers, assemblyManifestInfo, manifestConfig, manifestTemplate)
			: null;

		return new TrimmableTypeMapResult (generatedAssemblies, generatedJavaSources, allPeers, manifest, appRegTypes);
	}

	GeneratedManifest GenerateManifest (List<JavaPeerInfo> allPeers, AssemblyManifestInfo assemblyManifestInfo,
		ManifestConfig config, XDocument? manifestTemplate)
	{
		string minSdk = config.SupportedOSPlatformVersion ?? throw new InvalidOperationException ("SupportedOSPlatformVersion must be provided by MSBuild.");
		if (Version.TryParse (minSdk, out var sopv)) {
			minSdk = sopv.Major.ToString (System.Globalization.CultureInfo.InvariantCulture);
		}

		string targetSdk = config.AndroidApiLevel ?? throw new InvalidOperationException ("AndroidApiLevel must be provided by MSBuild.");
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
			RuntimeProviderJavaName = config.RuntimeProviderJavaName ?? throw new InvalidOperationException ("RuntimeProviderJavaName must be provided by MSBuild."),
			Debug = config.Debug,
			NeedsInternet = config.NeedsInternet,
			EmbedAssemblies = config.EmbedAssemblies,
			ForceDebuggable = forceDebuggable,
			ForceExtractNativeLibs = forceDebuggable,
			ManifestPlaceholders = config.ManifestPlaceholders,
			ApplicationJavaClass = config.ApplicationJavaClass,
		};

		var (doc, providerNames) = generator.Generate (manifestTemplate, allPeers, assemblyManifestInfo);
		return new GeneratedManifest (doc, providerNames.Count > 0 ? providerNames.ToArray () : []);
	}

	(List<JavaPeerInfo> peers, AssemblyManifestInfo manifestInfo) ScanAssemblies (IReadOnlyList<(string Name, PEReader Reader)> assemblies)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblies);
		var manifestInfo = scanner.ScanAssemblyManifestInfo ();
		logger.LogJavaPeerScanInfo (assemblies.Count, peers.Count);
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
			logger.LogGeneratedTypeMapAssemblyInfo (assemblyName, peers.Count);
		}
		var rootStream = new MemoryStream ();
		var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
		rootGenerator.Generate (perAssemblyNames, rootStream);
		rootStream.Position = 0;
		generatedAssemblies.Add (new GeneratedAssembly ("_Microsoft.Android.TypeMaps", rootStream));
		logger.LogGeneratedRootTypeMapInfo (perAssemblyNames.Count);
		logger.LogGeneratedTypeMapAssembliesInfo (generatedAssemblies.Count);
		return generatedAssemblies;
	}

	List<GeneratedJavaSource> GenerateJcwJavaSources (List<JavaPeerInfo> allPeers)
	{
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var sources = jcwGenerator.GenerateContent (allPeers);
		logger.LogGeneratedJcwFilesInfo (sources.Count);
		return sources.ToList ();
	}

	internal void RootManifestReferencedTypes (List<JavaPeerInfo> allPeers, XDocument? doc)
	{
		if (doc?.Root is not { } root) {
			return;
		}

		XNamespace androidNs = "http://schemas.android.com/apk/res/android";
		XName attName = androidNs + "name";
		var packageName = (string?) root.Attribute ("package") ?? "";

		var componentNames = new HashSet<string> (StringComparer.Ordinal);
		foreach (var element in root.Descendants ()) {
			switch (element.Name.LocalName) {
			case "activity":
			case "service":
			case "receiver":
			case "provider":
				var name = (string?) element.Attribute (attName);
				if (name is not null) {
					componentNames.Add (ResolveManifestClassName (name, packageName));
				}
				break;
			}
		}

		if (componentNames.Count == 0) {
			return;
		}

		// Build lookup by dot-name, keeping '$' for nested types (manifests use '$' too).
		var peersByDotName = new Dictionary<string, List<JavaPeerInfo>> (StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			var dotName = peer.JavaName.Replace ('/', '.');
			if (!peersByDotName.TryGetValue (dotName, out var list)) {
				list = [];
				peersByDotName [dotName] = list;
			}
			list.Add (peer);
		}

		foreach (var name in componentNames) {
			if (peersByDotName.TryGetValue (name, out var peers)) {
				foreach (var peer in peers) {
					if (!peer.IsUnconditional) {
						peer.IsUnconditional = true;
						logger.LogRootingManifestReferencedTypeInfo (name, peer.ManagedTypeName);
					}
				}
			} else {
				logger.LogManifestReferencedTypeNotFoundWarning (name);
			}
		}
	}

	/// <summary>
	/// Resolves an android:name value to a fully-qualified class name.
	/// Names starting with '.' are relative to the package. Names with no '.' at all
	/// are also treated as relative (Android tooling convention).
	/// </summary>
	static string ResolveManifestClassName (string name, string packageName)
	{
		if (name.StartsWith (".", StringComparison.Ordinal)) {
			return packageName + name;
		}

		if (name.IndexOf ('.') < 0 && !packageName.IsNullOrEmpty ()) {
			return packageName + "." + name;
		}

		return name;
	}
}
