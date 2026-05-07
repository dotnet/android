#nullable enable

namespace Microsoft.Android.Runtime;

// Shared TypeMap group anchors for per-rank array entries. Per-assembly typemap DLLs
// reference these so all rank-N entries across all assemblies merge into a single
// dictionary at runtime via TypeMapping.GetOrCreateExternalTypeMapping<__ArrayMapRankN>().
//
// To support a higher MaxArrayRank, add additional types here and bump
// TrimmableTypeMapGenerator.MaxSupportedArrayRank.

internal sealed class __ArrayMapRank1 { }
internal sealed class __ArrayMapRank2 { }
internal sealed class __ArrayMapRank3 { }
internal sealed class __ArrayMapRank4 { }
internal sealed class __ArrayMapRank5 { }
internal sealed class __ArrayMapRank6 { }
internal sealed class __ArrayMapRank7 { }
internal sealed class __ArrayMapRank8 { }
