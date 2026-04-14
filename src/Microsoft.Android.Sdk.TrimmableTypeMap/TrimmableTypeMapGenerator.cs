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

		RootManifestReferencedTypes (allPeers, PrepareManifestForRooting (manifestTemplate, manifestConfig));

		var generatedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion);
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		logger.LogGeneratingJcwFilesInfo (jcwPeers.Count, allPeers.Count);
		var generatedJavaSources = GenerateJcwJavaSources (jcwPeers);

		// Collect Application/Instrumentation types that need deferred registerNatives
		var appRegTypes = jcwPeers
			.Where (p => p.CannotRegisterInStaticConstructor && !p.DoNotGenerateAcw)
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
		var deferredRegistrationNames = new HashSet<string> (StringComparer.Ordinal);
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
					var resolvedName = ResolveManifestClassName (name, packageName);
					componentNames.Add (resolvedName);

					if (element.Name.LocalName is "application" or "instrumentation") {
						deferredRegistrationNames.Add (resolvedName);
					}
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
			AddJniLookupNames (peersByDotName, peer.JavaName, peer);
			if (peer.CompatJniName != peer.JavaName) {
				AddJniLookupNames (peersByDotName, peer.CompatJniName, peer);
			}
		}

		foreach (var name in componentNames) {
			if (peersByDotName.TryGetValue (name, out var peers)) {
				foreach (var peer in peers) {
					if (deferredRegistrationNames.Contains (name)) {
						MarkCannotRegisterInStaticConstructor (peer, peersByDotName);
					}

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

	static void MarkCannotRegisterInStaticConstructor (JavaPeerInfo peer, Dictionary<string, List<JavaPeerInfo>> peersByDotName)
	{
		var pending = new Queue<JavaPeerInfo> ();
		var visited = new HashSet<string> (StringComparer.Ordinal);

		pending.Enqueue (peer);
		while (pending.Count > 0) {
			var current = pending.Dequeue ();
			if (!visited.Add (current.JavaName)) {
				continue;
			}

			current.CannotRegisterInStaticConstructor = true;

			if (current.BaseJavaName is null) {
				continue;
			}

			if (!peersByDotName.TryGetValue (current.BaseJavaName, out var basePeers)) {
				continue;
			}

			foreach (var basePeer in basePeers) {
				if (!basePeer.DoNotGenerateAcw) {
					pending.Enqueue (basePeer);
				}
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

	static XDocument? PrepareManifestForRooting (XDocument? manifestTemplate, ManifestConfig? manifestConfig)
	{
		if (manifestTemplate is null && manifestConfig is null) {
			return null;
		}

		var doc = manifestTemplate is not null
			? new XDocument (manifestTemplate)
			: new XDocument (
				new XElement (
					"manifest",
					new XAttribute (XNamespace.Xmlns + "android", ManifestConstants.AndroidNs.NamespaceName)));

		if (doc.Root is not { } root) {
			return doc;
		}

		if (manifestConfig is null) {
			return doc;
		}

		if (((string?) root.Attribute ("package")).IsNullOrEmpty () && !manifestConfig.PackageName.IsNullOrEmpty ()) {
			root.SetAttributeValue ("package", manifestConfig.PackageName);
		}

		ManifestGenerator.ApplyPlaceholders (doc, manifestConfig.ManifestPlaceholders);

		if (!manifestConfig.ApplicationJavaClass.IsNullOrEmpty ()) {
			var app = root.Element ("application");
			if (app is null) {
				app = new XElement ("application");
				root.Add (app);
			}

			if (app.Attribute (ManifestConstants.AttName) is null) {
				app.SetAttributeValue (ManifestConstants.AttName, manifestConfig.ApplicationJavaClass);
			}
		}

		return doc;
	}

	static void AddJniLookupNames (Dictionary<string, List<JavaPeerInfo>> peersByDotName, string jniName, JavaPeerInfo peer)
	{
		AddPeerByDotName (peersByDotName, jniName, peer);

		var simpleName = JniSignatureHelper.GetJavaSimpleName (jniName);
		var packageName = JniSignatureHelper.GetJavaPackageName (jniName);
		var manifestName = packageName.IsNullOrEmpty () ? simpleName : packageName + "." + simpleName;
		AddPeerByDotName (peersByDotName, manifestName, peer);

		var javaSourceName = JniSignatureHelper.JniNameToJavaName (jniName);
		if (javaSourceName != manifestName) {
			AddPeerByDotName (peersByDotName, javaSourceName, peer);
		}
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
