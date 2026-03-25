#nullable enable

using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Describes an [IntentFilter] attribute on a component type.
/// </summary>
public sealed record IntentFilterInfo
{
	/// <summary>
	/// Action names from the first constructor argument (string[]).
	/// </summary>
	public IReadOnlyList<string> Actions { get; init; } = [];

	/// <summary>
	/// Category names.
	/// </summary>
	public IReadOnlyList<string> Categories { get; init; } = [];

	/// <summary>
	/// Named properties (DataScheme, DataHost, DataPath, Label, Icon, Priority, etc.).
	/// </summary>
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?> ();
}
