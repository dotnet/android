#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Assembly-level manifest attributes collected from all scanned assemblies.
/// Aggregated across assemblies — used to generate top-level manifest elements
/// like <![CDATA[<uses-permission>]]>, <![CDATA[<uses-feature>]]>, etc.
/// </summary>
public sealed class AssemblyManifestInfo
{
	public List<PermissionInfo> Permissions { get; } = [];
	public List<PermissionGroupInfo> PermissionGroups { get; } = [];
	public List<PermissionTreeInfo> PermissionTrees { get; } = [];
	public List<UsesPermissionInfo> UsesPermissions { get; } = [];
	public List<UsesFeatureInfo> UsesFeatures { get; } = [];
	public List<UsesLibraryInfo> UsesLibraries { get; } = [];
	public List<UsesConfigurationInfo> UsesConfigurations { get; } = [];
	public List<MetaDataInfo> MetaData { get; } = [];
	public List<PropertyInfo> Properties { get; } = [];

	/// <summary>
	/// Assembly-level [Application] attribute properties (merged from all assemblies).
	/// Null if no assembly-level [Application] attribute was found.
	/// </summary>
	public Dictionary<string, object?>? ApplicationProperties { get; set; }
}
