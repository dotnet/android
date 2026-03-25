#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public sealed record PermissionInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

public sealed record PermissionGroupInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

public sealed record PermissionTreeInfo
{
	public required string Name { get; init; }
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}

public sealed record UsesPermissionInfo
{
	public required string Name { get; init; }
	public int? MaxSdkVersion { get; init; }
}

/// <summary>
/// Describes a [UsesFeature] attribute.
/// </summary>
public sealed record UsesFeatureInfo
{
	/// <summary>
	/// Feature name (e.g., "android.hardware.camera"). Null for GL ES version features.
	/// </summary>
	public string? Name { get; init; }

	/// <summary>
	/// OpenGL ES version (e.g., 0x00020000 for 2.0). Zero for named features.
	/// </summary>
	public int GLESVersion { get; init; }

	public bool Required { get; init; } = true;
}

public sealed record UsesLibraryInfo
{
	public required string Name { get; init; }
	public bool Required { get; init; } = true;
}

public sealed record UsesConfigurationInfo
{
	public bool ReqFiveWayNav { get; init; }
	public bool ReqHardKeyboard { get; init; }
	public string? ReqKeyboardType { get; init; }
	public string? ReqNavigation { get; init; }
	public string? ReqTouchScreen { get; init; }
}

public sealed record PropertyInfo
{
	public required string Name { get; init; }
	public string? Value { get; init; }
	public string? Resource { get; init; }
}
