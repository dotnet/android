#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Configuration for manifest generation, passed from the MSBuild task.
/// </summary>
public record ManifestConfig (
	string PackageName,
	string? ApplicationLabel,
	string? VersionCode,
	string? VersionName,
	string? AndroidApiLevel,
	string? SupportedOSPlatformVersion,
	string? AndroidRuntime,
	bool Debug,
	bool NeedsInternet,
	bool EmbedAssemblies,
	string? ManifestPlaceholders,
	string? CheckedBuild,
	string? ApplicationJavaClass);

/// <summary>
/// Result of the trimmable type map generation.
/// </summary>
public record TrimmableTypeMapResult (
	IReadOnlyList<string> GeneratedAssemblies,
	IReadOnlyList<string> GeneratedJavaFiles,
	string[]? AdditionalProviderSources);
