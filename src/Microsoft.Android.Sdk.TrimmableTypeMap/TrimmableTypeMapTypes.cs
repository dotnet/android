using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public record TrimmableTypeMapResult (
	IReadOnlyList<GeneratedAssembly> GeneratedAssemblies,
	IReadOnlyList<GeneratedJavaSource> GeneratedJavaSources,
	IReadOnlyList<JavaPeerInfo> AllPeers,
	string[]? AdditionalProviderSources = null);

public record GeneratedAssembly (string Name, MemoryStream Content);

public record GeneratedJavaSource (string RelativePath, string Content);

/// <summary>
/// Configuration values for manifest generation. Passed from MSBuild properties.
/// </summary>
public record ManifestConfig (
	string PackageName,
	string? ApplicationLabel = null,
	string? VersionCode = null,
	string? VersionName = null,
	string? AndroidApiLevel = null,
	string? SupportedOSPlatformVersion = null,
	string? AndroidRuntime = null,
	bool Debug = false,
	bool NeedsInternet = false,
	bool EmbedAssemblies = false,
	string? ManifestPlaceholders = null,
	string? CheckedBuild = null,
	string? ApplicationJavaClass = null);
