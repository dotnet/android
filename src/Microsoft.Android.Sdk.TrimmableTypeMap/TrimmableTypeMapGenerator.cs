using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public class TrimmableTypeMapGenerator
{
	readonly Action<string> log;
	readonly Action<string>? warn;

	public TrimmableTypeMapGenerator (Action<string> log, Action<string>? warn = null)
	{
		this.log = log ?? throw new ArgumentNullException (nameof (log));
		this.warn = warn;
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
			log ("No Java peer types found, skipping typemap generation.");
			return new TrimmableTypeMapResult ([], [], allPeers);
		}

		RootManifestReferencedTypes (allPeers, manifestTemplate);

		var generatedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion);
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		log ($"Generating JCW files for {jcwPeers.Count} types (filtered from {allPeers.Count} total).");
		var generatedJavaSources = GenerateJcwJavaSources (jcwPeers);

		// Collect Application/Instrumentation types that need deferred registerNatives
		var appRegTypes = allPeers
			.Where (p => p.CannotRegisterInStaticConstructor && !p.IsAbstract)
			.Select (p => JniSignatureHelper.JniNameToJavaName (p.JavaName))
			.ToList ();
		if (appRegTypes.Count > 0) {
			log ($"Found {appRegTypes.Count} Application/Instrumentation types for deferred registration.");
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
			case "application":
			case "activity":
			case "instrumentation":
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

		// Build lookup by both Java and compat dot-names. Keep '$' for nested types,
		// because manifests commonly use '$', but also include the Java source form.
		var peersByDotName = new Dictionary<string, List<JavaPeerInfo>> (StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			var dotName = GetManifestLookupName (peer.JavaName);
			AddPeerByDotName (peersByDotName, dotName, peer);
			AddJavaSourceLookupName (peersByDotName, dotName, peer);

			var compatDotName = GetManifestLookupName (peer.CompatJniName);
			if (compatDotName != dotName) {
				AddPeerByDotName (peersByDotName, compatDotName, peer);
				AddJavaSourceLookupName (peersByDotName, compatDotName, peer);
			}
		}

		foreach (var name in componentNames) {
			if (peersByDotName.TryGetValue (name, out var peers)) {
				foreach (var peer in peers) {
					if (!peer.IsUnconditional) {
						peer.IsUnconditional = true;
						log ($"Rooting manifest-referenced type '{name}' ({peer.ManagedTypeName}) as unconditional.");
					}
				}
			} else {
				warn?.Invoke (name);
			}
		}
	}

	static void AddPeerByDotName (Dictionary<string, List<JavaPeerInfo>> peersByDotName, string dotName, JavaPeerInfo peer)
	{
		if (!peersByDotName.TryGetValue (dotName, out var list)) {
			list = [];
			peersByDotName [dotName] = list;
		}

		list.Add (peer);
	}

	static void AddJavaSourceLookupName (Dictionary<string, List<JavaPeerInfo>> peersByDotName, string dotName, JavaPeerInfo peer)
	{
		var javaSourceName = dotName.Replace ('$', '.');
		if (javaSourceName != dotName) {
			AddPeerByDotName (peersByDotName, javaSourceName, peer);
		}
	}

	static string GetManifestLookupName (string jniName)
	{
		return jniName.Replace ('/', '.');
	}

	/// <summary>
	/// Resolves an android:name value to a fully-qualified class name.
	/// Names starting with '.' are relative to the package. Names with no '.' at all
	/// are also treated as relative (Android tooling convention).
	/// </summary>
	static string ResolveManifestClassName (string name, string packageName)
	{
		return name switch {
			_ when name.StartsWith (".", StringComparison.Ordinal) => packageName + name,
			_ when name.IndexOf ('.') < 0 && !packageName.IsNullOrEmpty () => packageName + "." + name,
			_ => name,
		};
	}
}
