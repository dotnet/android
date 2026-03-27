using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Result of the trimmable type map generation.
/// </summary>
public record TrimmableTypeMapResult (
	IReadOnlyList<string> GeneratedAssemblies,
	IReadOnlyList<string> GeneratedJavaFiles,
	IReadOnlyList<JavaPeerInfo> AllPeers);
