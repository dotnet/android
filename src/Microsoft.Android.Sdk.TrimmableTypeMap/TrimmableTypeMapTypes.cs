using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public record TrimmableTypeMapResult (
	IReadOnlyList<GeneratedAssembly> GeneratedAssemblies,
	IReadOnlyList<GeneratedJavaSource> GeneratedJavaSources,
	IReadOnlyList<JavaPeerInfo> AllPeers);

public record GeneratedAssembly (string Name, MemoryStream Content);

public record GeneratedJavaSource (string RelativePath, string Content);
