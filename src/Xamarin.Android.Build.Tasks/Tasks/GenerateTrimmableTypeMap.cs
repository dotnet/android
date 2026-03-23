#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates trimmable TypeMap assemblies and JCW Java source files from resolved assemblies.
/// Runs before the trimmer to produce per-assembly typemap .dll files and a root
/// _Microsoft.Android.TypeMaps.dll, plus .java files for ACW types with registerNatives.
/// </summary>
public class GenerateTrimmableTypeMap : AndroidTask
{
	public override string TaskPrefix => "GTT";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public string JavaSourceOutputDirectory { get; set; } = "";

	[Required]
	public string AcwMapDirectory { get; set; } = "";

	/// <summary>
	/// The .NET target framework version (e.g., "v11.0"). Used to set the System.Runtime
	/// assembly reference version in generated typemap assemblies.
	/// </summary>
	[Required]
	public string TargetFrameworkVersion { get; set; } = "";

	/// <summary>
	/// User's AndroidManifest.xml template. May be null if no template exists.
	/// </summary>
	public string? ManifestTemplate { get; set; }

	/// <summary>
	/// Output path for the merged AndroidManifest.xml.
	/// </summary>
	public string? MergedAndroidManifestOutput { get; set; }

	/// <summary>
	/// Android package name (e.g., "com.example.myapp").
	/// </summary>
	public string? PackageName { get; set; }

	/// <summary>
	/// Application label for the manifest.
	/// </summary>
	public string? ApplicationLabel { get; set; }

	public string? VersionCode { get; set; }

	public string? VersionName { get; set; }

	/// <summary>
	/// Target Android API level (e.g., "36").
	/// </summary>
	public string? AndroidApiLevel { get; set; }

	/// <summary>
	/// Supported OS platform version (e.g., "21.0").
	/// </summary>
	public string? SupportedOSPlatformVersion { get; set; }

	/// <summary>
	/// Android runtime type ("mono", "coreclr", "nativeaot").
	/// </summary>
	public string? AndroidRuntime { get; set; }

	public bool Debug { get; set; }

	public bool NeedsInternet { get; set; }

	public bool EmbedAssemblies { get; set; }

	/// <summary>
	/// Manifest placeholder values (e.g., "applicationId=com.example.app;key=value").
	/// </summary>
	public string? ManifestPlaceholders { get; set; }

	/// <summary>
	/// When set, forces android:debuggable="true" and android:extractNativeLibs="true".
	/// </summary>
	public string? CheckedBuild { get; set; }

	/// <summary>
	/// Optional custom Application Java class name.
	/// </summary>
	public string? ApplicationJavaClass { get; set; }

	[Output]
	public ITaskItem []? GeneratedAssemblies { get; set; }

	[Output]
	public ITaskItem []? GeneratedJavaFiles { get; set; }

	/// <summary>
	/// Content provider names for ApplicationRegistration.java.
	/// </summary>
	[Output]
	public string []? AdditionalProviderSources { get; set; }

	public override bool RunTask ()
	{
		var systemRuntimeVersion = ParseTargetFrameworkVersion (TargetFrameworkVersion);
		// Don't filter by HasMonoAndroidReference — ReferencePath items from the compiler
		// don't carry this metadata. The scanner handles non-Java assemblies gracefully.
		var assemblyPaths = ResolvedAssemblies.Select (i => i.ItemSpec).Distinct ().ToList ();

		// Framework/runtime-pack assemblies (Mono.Android, Java.Interop, etc.) already have JCW .java
		// files in the SDK. Only generate JCWs for user assemblies. Detect framework assemblies via
		// FrameworkReferenceName (not NuGetPackageId — user libraries from NuGet need JCWs too).
		var frameworkAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (var item in ResolvedAssemblies) {
			if (!item.GetMetadata ("FrameworkReferenceName").IsNullOrEmpty ()) {
				frameworkAssemblyNames.Add (Path.GetFileNameWithoutExtension (item.ItemSpec));
			}
		}

		Directory.CreateDirectory (OutputDirectory);
		Directory.CreateDirectory (JavaSourceOutputDirectory);
		Directory.CreateDirectory (AcwMapDirectory);

		var (allPeers, assemblyManifestInfo) = ScanAssemblies (assemblyPaths);
		if (allPeers.Count == 0) {
			Log.LogDebugMessage ("No Java peer types found, skipping typemap generation.");
			return !Log.HasLoggedErrors;
		}

		GeneratedAssemblies = GenerateTypeMapAssemblies (allPeers, systemRuntimeVersion, assemblyPaths);

		// Generate JCW .java files for user assemblies + framework Implementor types.
		// Framework binding types already have compiled JCWs in the SDK but their constructors
		// use the legacy TypeManager.Activate() JNI native which isn't available in the
		// trimmable runtime. Implementor types (View_OnClickListenerImplementor, etc.) are
		// in the mono.* Java package so we use the mono/ prefix to identify them.
		// We generate fresh JCWs that use Runtime.registerNatives() for activation.
		var jcwPeers = allPeers.Where (p =>
			!frameworkAssemblyNames.Contains (p.AssemblyName)
			|| p.JavaName.StartsWith ("mono/", StringComparison.Ordinal)).ToList ();
		Log.LogDebugMessage ($"Generating JCW files for {jcwPeers.Count} types (filtered from {allPeers.Count} total).");
		GeneratedJavaFiles = GenerateJcwJavaSources (jcwPeers);

		// Generate manifest if output path is configured
		if (!MergedAndroidManifestOutput.IsNullOrEmpty () && !PackageName.IsNullOrEmpty ()) {
			GenerateManifest (allPeers, assemblyManifestInfo);
		}

		return !Log.HasLoggedErrors;
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
		Log.LogDebugMessage ($"Scanned {assemblyPaths.Count} assemblies, found {peers.Count} Java peer types.");
		return (peers, manifestInfo);
	}

	ITaskItem [] GenerateTypeMapAssemblies (List<JavaPeerInfo> allPeers, Version systemRuntimeVersion,
		IReadOnlyList<string> assemblyPaths)
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

		var generatedAssemblies = new List<ITaskItem> ();
		var perAssemblyNames = new List<string> ();
		var generator = new TypeMapAssemblyGenerator (systemRuntimeVersion);
		bool anyRegenerated = false;

		foreach (var group in peersByAssembly) {
			string assemblyName = $"_{group.Key}.TypeMap";
			string outputPath = Path.Combine (OutputDirectory, assemblyName + ".dll");
			perAssemblyNames.Add (assemblyName);

			if (IsUpToDate (outputPath, group.Key, sourcePathByName)) {
				Log.LogDebugMessage ($"  {assemblyName}: up to date, skipping");
				generatedAssemblies.Add (new TaskItem (outputPath));
				continue;
			}

			generator.Generate (group.ToList (), outputPath, assemblyName);
			generatedAssemblies.Add (new TaskItem (outputPath));
			anyRegenerated = true;

			Log.LogDebugMessage ($"  {assemblyName}: {group.Count ()} types");
		}

		// Root assembly references all per-assembly typemaps — regenerate if any changed
		string rootOutputPath = Path.Combine (OutputDirectory, "_Microsoft.Android.TypeMaps.dll");
		if (anyRegenerated || !File.Exists (rootOutputPath)) {
			var rootGenerator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
			rootGenerator.Generate (perAssemblyNames, rootOutputPath);
			Log.LogDebugMessage ($"  Root: {perAssemblyNames.Count} per-assembly refs");
		} else {
			Log.LogDebugMessage ($"  Root: up to date, skipping");
		}
		generatedAssemblies.Add (new TaskItem (rootOutputPath));

		Log.LogDebugMessage ($"Generated {generatedAssemblies.Count} typemap assemblies.");
		return generatedAssemblies.ToArray ();
	}

	static bool IsUpToDate (string outputPath, string assemblyName, Dictionary<string, string> sourcePathByName)
	{
		if (!File.Exists (outputPath)) {
			return false;
		}
		if (!sourcePathByName.TryGetValue (assemblyName, out var sourcePath)) {
			return false;
		}
		return File.GetLastWriteTimeUtc (outputPath) >= File.GetLastWriteTimeUtc (sourcePath);
	}

	ITaskItem [] GenerateJcwJavaSources (List<JavaPeerInfo> allPeers)
	{
		var jcwGenerator = new JcwJavaSourceGenerator ();
		var files = jcwGenerator.Generate (allPeers, JavaSourceOutputDirectory);
		Log.LogDebugMessage ($"Generated {files.Count} JCW Java source files.");

		var items = new ITaskItem [files.Count];
		for (int i = 0; i < files.Count; i++) {
			items [i] = new TaskItem (files [i]);
		}
		return items;
	}

	void GenerateManifest (List<JavaPeerInfo> allPeers, AssemblyManifestInfo assemblyManifestInfo)
	{
		if (PackageName is null || MergedAndroidManifestOutput is null) {
			return;
		}

		// Validate components
		ValidateComponents (allPeers, assemblyManifestInfo);
		if (Log.HasLoggedErrors) {
			return;
		}

		string minSdk = "21";
		if (!SupportedOSPlatformVersion.IsNullOrEmpty () && Version.TryParse (SupportedOSPlatformVersion, out var sopv)) {
			minSdk = sopv.Major.ToString ();
		}

		string targetSdk = AndroidApiLevel ?? "36";
		if (Version.TryParse (targetSdk, out var apiVersion)) {
			targetSdk = apiVersion.Major.ToString ();
		}

		bool forceDebuggable = !CheckedBuild.IsNullOrEmpty ();

		var generator = new ManifestGenerator {
			PackageName = PackageName,
			ApplicationLabel = ApplicationLabel ?? PackageName,
			VersionCode = VersionCode ?? "",
			VersionName = VersionName ?? "",
			MinSdkVersion = minSdk,
			TargetSdkVersion = targetSdk,
			AndroidRuntime = AndroidRuntime ?? "coreclr",
			Debug = Debug,
			NeedsInternet = NeedsInternet,
			EmbedAssemblies = EmbedAssemblies,
			ForceDebuggable = forceDebuggable,
			ForceExtractNativeLibs = forceDebuggable,
			ManifestPlaceholders = ManifestPlaceholders,
			ApplicationJavaClass = ApplicationJavaClass,
		};

		var providerNames = generator.Generate (ManifestTemplate, allPeers, assemblyManifestInfo, MergedAndroidManifestOutput);
		AdditionalProviderSources = providerNames.ToArray ();
	}

	static Version ParseTargetFrameworkVersion (string tfv)
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
				Log.LogCodedError ("XA4213", Properties.Resources.XA4213, peer.ManagedTypeName);
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
		// These match the legacy ManifestDocument behavior (InvalidOperationException with same messages).
		// No XA error code — legacy doesn't have one either.
		if (applicationTypes.Count > 1) {
			Log.LogError ("There can be only one type with an [Application] attribute; found: " +
				string.Join (", ", applicationTypes));
		} else if (applicationTypes.Count > 0 && hasAssemblyLevelApplication) {
			Log.LogError ("Application cannot have both a type with an [Application] attribute and an [assembly:Application] attribute.");
		}
	}

	/// <summary>
	/// Filters resolved assemblies to only those that reference Mono.Android or Java.Interop
	/// (i.e., assemblies that could contain [Register] types). Skips BCL assemblies.
	/// </summary>
	static IReadOnlyList<string> GetJavaInteropAssemblyPaths (ITaskItem [] items)
	{
		var paths = new List<string> (items.Length);
		foreach (var item in items) {
			if (MonoAndroidHelper.IsMonoAndroidAssembly (item)) {
				paths.Add (item.ItemSpec);
			}
		}
		return paths;
	}
}
