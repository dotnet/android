using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal sealed class AssemblyManifestInfo
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

	public Dictionary<string, object?>? ApplicationProperties { get; set; }
}
