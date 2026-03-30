#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public enum ComponentKind
{
	Activity,
	Service,
	BroadcastReceiver,
	ContentProvider,
	Application,
	Instrumentation,
}

public class ComponentInfo
{
	public bool HasPublicDefaultConstructor { get; set; }
	public ComponentKind Kind { get; set; }
	public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?> ();
	public IReadOnlyList<IntentFilterInfo> IntentFilters { get; set; } = [];
	public IReadOnlyList<MetaDataInfo> MetaData { get; set; } = [];
}

public class IntentFilterInfo
{
	public IReadOnlyList<string> Actions { get; set; } = [];
	public IReadOnlyList<string> Categories { get; set; } = [];
	public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?> ();
}

public class MetaDataInfo
{
	public string Name { get; set; } = "";
	public string? Value { get; set; }
	public string? Resource { get; set; }
}

public class PermissionInfo
{
	public string Name { get; set; } = "";
	public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?> ();
}

public class PermissionGroupInfo
{
	public string Name { get; set; } = "";
	public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?> ();
}

public class PermissionTreeInfo
{
	public string Name { get; set; } = "";
	public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?> ();
}

public class UsesPermissionInfo
{
	public string Name { get; set; } = "";
	public int? MaxSdkVersion { get; set; }
}

public class UsesFeatureInfo
{
	public string? Name { get; set; }
	public bool Required { get; set; }
	public int GLESVersion { get; set; }
}

public class UsesLibraryInfo
{
	public string Name { get; set; } = "";
	public bool Required { get; set; }
}

public class UsesConfigurationInfo
{
	public bool ReqFiveWayNav { get; set; }
	public bool ReqHardKeyboard { get; set; }
	public string? ReqKeyboardType { get; set; }
	public string? ReqNavigation { get; set; }
	public string? ReqTouchScreen { get; set; }
}

public class PropertyInfo
{
	public string Name { get; set; } = "";
	public string? Value { get; set; }
	public string? Resource { get; set; }
}

public class AssemblyManifestInfo
{
	public List<PermissionInfo> Permissions { get; set; } = new List<PermissionInfo> ();
	public List<PermissionGroupInfo> PermissionGroups { get; set; } = new List<PermissionGroupInfo> ();
	public List<PermissionTreeInfo> PermissionTrees { get; set; } = new List<PermissionTreeInfo> ();
	public List<UsesPermissionInfo> UsesPermissions { get; set; } = new List<UsesPermissionInfo> ();
	public List<UsesFeatureInfo> UsesFeatures { get; set; } = new List<UsesFeatureInfo> ();
	public List<UsesLibraryInfo> UsesLibraries { get; set; } = new List<UsesLibraryInfo> ();
	public List<UsesConfigurationInfo> UsesConfigurations { get; set; } = new List<UsesConfigurationInfo> ();
	public List<MetaDataInfo> MetaData { get; set; } = new List<MetaDataInfo> ();
	public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo> ();
	public Dictionary<string, object?>? ApplicationProperties { get; set; }
}
