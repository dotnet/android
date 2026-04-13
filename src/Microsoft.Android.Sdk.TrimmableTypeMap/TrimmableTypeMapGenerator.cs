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

	static readonly HashSet<string> RequiredFrameworkDeferredRegistrationTypes = new (StringComparer.Ordinal) {
		"android/app/Application",
		"android/app/Instrumentation",
	};

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
		bool useSharedTypemapUniverse = false,
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

		var preparedManifest = PrepareManifestForRooting (manifestTemplate, manifestConfig);
		RootManifestReferencedTypes (allPeers, preparedManifest);
		PropagateDeferredRegistrationToBaseClasses (allPeers);
		PropagateCannotRegisterToDescendants (allPeers);

		var generatedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion, useSharedTypemapUniverse);
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		logger.LogGeneratingJcwFilesInfo (jcwPeers.Count, allPeers.Count);
		var generatedJavaSources = GenerateJcwJavaSources (jcwPeers);

		var appRegTypes = CollectApplicationRegistrationTypes (allPeers);
		if (appRegTypes.Count > 0) {
			logger.LogDeferredRegistrationTypesInfo (appRegTypes.Count);
		}

		var manifest = manifestConfig is not null
			? GenerateManifest (allPeers, assemblyManifestInfo, manifestConfig, preparedManifest)
			: null;

		return new TrimmableTypeMapResult (generatedAssemblies, generatedJavaSources, allPeers, manifest, appRegTypes);
	}

	internal static List<string> CollectApplicationRegistrationTypes (List<JavaPeerInfo> allPeers)
	{
		var appRegTypes = new List<string> ();
		var seen = new HashSet<string> (StringComparer.Ordinal);

		foreach (var peer in allPeers) {
			if (!peer.CannotRegisterInStaticConstructor) {
				continue;
			}

			// ApplicationRegistration.java is compiled against the app's target Android API
			// surface. Legacy framework descendants such as android.test.* may not exist there,
			// so keep only the two framework roots plus app/runtime types that participate in
			// the deferred-registration flow.
			if (peer.DoNotGenerateAcw && !RequiredFrameworkDeferredRegistrationTypes.Contains (peer.JavaName)) {
				continue;
			}

			var javaName = JniSignatureHelper.JniNameToJavaName (peer.JavaName);
			if (seen.Add (javaName)) {
				appRegTypes.Add (javaName);
			}
		}

		return appRegTypes;
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

	List<GeneratedAssembly> GenerateTypeMapAssemblies (List<JavaPeerInfo> allPeers, Version systemRuntimeVersion, bool useSharedTypemapUniverse)
	{
		List<(string AssemblyName, List<JavaPeerInfo> Peers)> peersByAssembly;

		if (useSharedTypemapUniverse) {
			// In Release builds all per-assembly typemaps are merged into a single
			// shared universe dictionary.  Cross-assembly aliases (e.g. Java.Lang.Object
			// in Mono.Android and JavaObject in Java.Interop both mapping to
			// java/lang/Object) must be moved into the owner assembly's group so the
			// ModelBuilder can handle them as an alias group and the runtime doesn't
			// crash on duplicate keys.
			peersByAssembly = MergeCrossAssemblyAliases (allPeers);
		} else {
			// In Debug builds each typemap DLL has its own per-assembly universe, so
			// cross-assembly duplicates don't collide — simple GroupBy is sufficient.
			peersByAssembly = allPeers
				.GroupBy (p => p.AssemblyName, StringComparer.Ordinal)
				.OrderBy (g => g.Key, StringComparer.Ordinal)
				.Select (g => (g.Key, g.ToList ()))
				.ToList ();
		}

		var generatedAssemblies = new List<GeneratedAssembly> ();
		var perAssemblyNames = new List<string> ();
		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);
		foreach (var (assemblyName, peers) in peersByAssembly) {
			string typeMapAssemblyName = $"_{assemblyName}.TypeMap";
			perAssemblyNames.Add (typeMapAssemblyName);
			var stream = new MemoryStream ();
			generator.Generate (peers, stream, typeMapAssemblyName, useSharedTypemapUniverse);
			stream.Position = 0;
			generatedAssemblies.Add (new GeneratedAssembly (typeMapAssemblyName, stream));
			logger.LogGeneratedTypeMapAssemblyInfo (typeMapAssemblyName, peers.Count);
		}
		var rootStream = new MemoryStream ();
		var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
		rootGenerator.Generate (perAssemblyNames, useSharedTypemapUniverse, rootStream);
		rootStream.Position = 0;
		generatedAssemblies.Add (new GeneratedAssembly ("_Microsoft.Android.TypeMaps", rootStream));
		logger.LogGeneratedRootTypeMapInfo (perAssemblyNames.Count);
		logger.LogGeneratedTypeMapAssembliesInfo (generatedAssemblies.Count);
		return generatedAssemblies;
	}

	/// <summary>
	/// Groups peers by assembly, merging cross-assembly aliases into a single group.
	/// When the same JNI name appears in multiple assemblies (e.g. <c>Java.Lang.Object</c>
	/// in <c>Mono.Android</c> and <c>JavaObject</c> in <c>Java.Interop</c> both mapping
	/// to <c>java/lang/Object</c>), peers from later assemblies are moved into the owner
	/// assembly's group so the <see cref="ModelBuilder"/> can handle them as an alias group.
	/// </summary>
	/// <remarks>
	/// Ownership is determined by <c>[Register]</c> over <c>[JniTypeSignature]</c> — the
	/// canonical MCW binding type takes precedence. Among peers with the same attribute
	/// kind, the first assembly in sorted order wins.
	/// </remarks>
	internal static List<(string AssemblyName, List<JavaPeerInfo> Peers)> MergeCrossAssemblyAliases (List<JavaPeerInfo> allPeers)
	{
		var groups = new SortedDictionary<string, List<JavaPeerInfo>> (StringComparer.Ordinal);

		// Group by assembly (sorted order)
		foreach (var peer in allPeers) {
			if (!groups.TryGetValue (peer.AssemblyName, out var list)) {
				list = [];
				groups [peer.AssemblyName] = list;
			}
			list.Add (peer);
		}

		// Build JNI name → owner assembly map.
		// [Register] types take precedence over [JniTypeSignature] types.
		// Among peers of the same kind, the first assembly (sorted order) wins.
		var jniNameOwner = new Dictionary<string, (string AssemblyName, bool IsFromJniTypeSignature)> (StringComparer.Ordinal);
		foreach (var kvp in groups) {
			string assemblyName = kvp.Key;
			foreach (var peer in kvp.Value) {
				if (!jniNameOwner.TryGetValue (peer.JavaName, out var current)) {
					jniNameOwner [peer.JavaName] = (assemblyName, peer.IsFromJniTypeSignature);
				} else if (current.IsFromJniTypeSignature && !peer.IsFromJniTypeSignature) {
					// [Register] type takes ownership from [JniTypeSignature] type
					jniNameOwner [peer.JavaName] = (assemblyName, false);
				}
			}
		}

		// Move colliding peers to the owner assembly
		var movedPeers = new List<(JavaPeerInfo Peer, string TargetAssembly)> ();
		foreach (var kvp in groups) {
			string assemblyName = kvp.Key;
			foreach (var peer in kvp.Value) {
				var owner = jniNameOwner [peer.JavaName];
				if (!string.Equals (owner.AssemblyName, assemblyName, StringComparison.Ordinal)) {
					movedPeers.Add ((peer, owner.AssemblyName));
				}
			}
		}

		foreach (var moved in movedPeers) {
			groups [moved.Peer.AssemblyName].Remove (moved.Peer);
			groups [moved.TargetAssembly].Add (moved.Peer);
		}

		// Return non-empty groups
		var result = new List<(string, List<JavaPeerInfo>)> ();
		foreach (var kvp in groups) {
			if (kvp.Value.Count > 0) {
				result.Add ((kvp.Key, kvp.Value));
			}
		}
		return result;
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

		var componentEntries = new List<(string Name, bool DeferredRegistration, XElement Element)> ();
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
					var resolvedName = ManifestNameResolver.Resolve (name, packageName);
					componentEntries.Add ((resolvedName, element.Name.LocalName is "application" or "instrumentation", element));
				}
				break;
			}
		}

		if (componentEntries.Count == 0) {
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

		foreach (var (name, deferredRegistration, element) in componentEntries) {
			if (peersByDotName.TryGetValue (name, out var peers)) {
				string actualJavaName = JniSignatureHelper.JniNameToJavaName (peers [0].JavaName);
				if (!string.Equals ((string?) element.Attribute (attName), actualJavaName, StringComparison.Ordinal)) {
					element.SetAttributeValue (attName, actualJavaName);
				}

				foreach (var peer in peers) {
					if (deferredRegistration) {
						peer.CannotRegisterInStaticConstructor = true;
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

	/// <summary>
	/// Propagates <see cref="JavaPeerInfo.CannotRegisterInStaticConstructor"/> up the base class chain.
	/// When a type like NUnitInstrumentation has deferred registration, its base class
	/// TestInstrumentation_1 must also defer — otherwise the base class <c>&lt;clinit&gt;</c> will call
	/// <c>registerNatives</c> before the managed runtime is ready.
	/// </summary>
	static void PropagateDeferredRegistrationToBaseClasses (List<JavaPeerInfo> allPeers)
	{
		var peersByJniName = new Dictionary<string, JavaPeerInfo> (StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			if (!peersByJniName.ContainsKey (peer.JavaName)) {
				peersByJniName [peer.JavaName] = peer;
			}
		}

		foreach (var peer in allPeers) {
			if (!peer.CannotRegisterInStaticConstructor) {
				continue;
			}

			var current = peer;
			while (current.BaseJavaName is { } baseJniName && peersByJniName.TryGetValue (baseJniName, out var basePeer)) {
				if (basePeer.DoNotGenerateAcw) {
					break;
				}

				basePeer.CannotRegisterInStaticConstructor = true;
				current = basePeer;
			}
		}
	}

	/// <summary>
	/// Propagates <see cref="JavaPeerInfo.CannotRegisterInStaticConstructor"/> DOWN
	/// from Application/Instrumentation types to all their descendants. Any subclass of
	/// an Instrumentation/Application type can be loaded by Android before the native
	/// library is ready, so it must also use the lazy __md_registerNatives pattern.
	/// </summary>
	internal static void PropagateCannotRegisterToDescendants (List<JavaPeerInfo> allPeers)
	{
		// Build a set of JavaNames that have CannotRegisterInStaticConstructor
		var cannotRegister = new HashSet<string> (StringComparer.Ordinal);
		foreach (var peer in allPeers) {
			if (peer.CannotRegisterInStaticConstructor) {
				cannotRegister.Add (peer.JavaName);
			}
		}

		// Also include the framework base types
		cannotRegister.Add ("android/app/Application");
		cannotRegister.Add ("android/app/Instrumentation");

		// Propagate to descendants: if your base is in the set, you're in the set too
		bool changed = true;
		while (changed) {
			changed = false;
			foreach (var peer in allPeers) {
				if (peer.CannotRegisterInStaticConstructor || peer.BaseJavaName is null) {
					continue;
				}
				if (cannotRegister.Contains (peer.BaseJavaName)) {
					peer.CannotRegisterInStaticConstructor = true;
					cannotRegister.Add (peer.JavaName);
					changed = true;
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
		var simpleName = JniSignatureHelper.GetJavaSimpleName (jniName);
		var packageName = JniSignatureHelper.GetJavaPackageName (jniName);
		var manifestName = packageName.IsNullOrEmpty () ? simpleName : packageName + "." + simpleName;
		AddPeerByDotName (peersByDotName, manifestName, peer);

		var javaSourceName = JniSignatureHelper.JniNameToJavaName (jniName);
		if (javaSourceName != manifestName) {
			AddPeerByDotName (peersByDotName, javaSourceName, peer);
		}
	}

}
