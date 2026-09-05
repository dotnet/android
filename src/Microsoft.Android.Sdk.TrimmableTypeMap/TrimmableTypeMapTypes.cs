using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public record TrimmableTypeMapResult (
	IReadOnlyList<GeneratedAssembly> GeneratedAssemblies,
	IReadOnlyList<GeneratedJavaSource> GeneratedJavaSources,
	IReadOnlyList<JavaPeerInfo> AllPeers,
	GeneratedManifest? Manifest = null,
	IReadOnlyList<string>? ApplicationRegistrationTypes = null)
{
	/// <summary>
	/// Java class names (dot-separated) of Application/Instrumentation types
	/// that need deferred <c>Runtime.registerNatives()</c> calls in
	/// <c>ApplicationRegistration.registerApplications()</c>.
	/// </summary>
	public IReadOnlyList<string> ApplicationRegistrationTypes { get; init; } =
		ApplicationRegistrationTypes ?? [];
}

/// <summary>
/// A generated typemap assembly. <paramref name="Content"/> is a read-only, seekable stream
/// positioned at the start of the serialised PE image; callers own it and should dispose it.
/// </summary>
/// <remarks>
/// <paramref name="Content"/> is deliberately typed as <see cref="Stream"/> rather than
/// <see cref="MemoryStream"/>: the emitter hands back a view over the buffers the PE serialiser
/// already produced, so the image is never copied into a second contiguous buffer. Consumers
/// should treat it as a forward-reading stream — hash it, rewind, and copy it — rather than
/// reaching for <see cref="MemoryStream"/> members such as <c>ToArray</c> or <c>GetBuffer</c>.
/// This assembly is build-time SDK infrastructure rather than a library third parties compile
/// against, and the in-tree consumer (<c>GenerateTrimmableTypeMap</c>) only ever streams the
/// content to disk, so the narrowed member surface is an intentional trade for the removed copy.
/// </remarks>
public record GeneratedAssembly (string Name, Stream Content);

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
	string? ApplicationJavaClass = null,
	IReadOnlyList<string>? LibraryManifests = null);
