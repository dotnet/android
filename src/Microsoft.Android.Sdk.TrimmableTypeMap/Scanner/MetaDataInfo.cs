#nullable enable

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Describes a [MetaData] attribute on a component type.
/// </summary>
public sealed record MetaDataInfo
{
	/// <summary>
	/// The metadata name (first constructor argument).
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// The Value property, if set.
	/// </summary>
	public string? Value { get; init; }

	/// <summary>
	/// The Resource property, if set.
	/// </summary>
	public string? Resource { get; init; }
}
