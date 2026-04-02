using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public record TrimmableTypeMapResult (
	IReadOnlyList<GeneratedAssembly> GeneratedAssemblies,
	IReadOnlyList<GeneratedJavaSource> GeneratedJavaSources,
	IReadOnlyList<JavaPeerInfo> AllPeers,
	GeneratedManifest? Manifest = null,
	IReadOnlyList<string> ApplicationRegistrationTypes = null!)
{
	/// <summary>
	/// Java class names (dot-separated) of Application/Instrumentation types
	/// that need deferred <c>Runtime.registerNatives()</c> calls in
	/// <c>ApplicationRegistration.registerApplications()</c>.
	/// </summary>
	public IReadOnlyList<string> ApplicationRegistrationTypes { get; init; } =
		ApplicationRegistrationTypes ?? [];
}

public record GeneratedAssembly (string Name, MemoryStream Content);

public record GeneratedJavaSource (string RelativePath, string Content);

/// <summary>
/// The in-memory result of manifest generation: the merged document and
/// any additional content provider class names for ApplicationRegistration.java.
/// </summary>
public record GeneratedManifest (XDocument Document, string[] AdditionalProviderSources);

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
	string? RuntimeProviderJavaName = null,
	bool Debug = false,
	bool NeedsInternet = false,
	bool EmbedAssemblies = false,
	string? ManifestPlaceholders = null,
	string? CheckedBuild = null,
	string? ApplicationJavaClass = null);
