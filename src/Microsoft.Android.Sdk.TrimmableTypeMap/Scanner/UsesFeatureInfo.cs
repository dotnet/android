#nullable enable

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Describes a [UsesFeature] attribute.
/// </summary>
internal sealed record UsesFeatureInfo
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
