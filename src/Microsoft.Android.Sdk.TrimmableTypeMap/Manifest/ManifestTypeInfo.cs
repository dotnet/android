using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Default mutable implementation of <see cref="IManifestTypeInfo"/>.
/// Used by both Cecil and SRM adapters to populate manifest type data.
/// </summary>
sealed class ManifestTypeInfo : IManifestTypeInfo
{
	public string FullName { get; set; } = "";
	public string Namespace { get; set; } = "";
	public string JavaName { get; set; } = "";
	public string CompatJavaName { get; set; } = "";
	public bool IsAbstract { get; set; }
	public bool HasPublicParameterlessConstructor { get; set; }
	public ManifestComponentKind ComponentKind { get; set; }
	public ComponentAttributeInfo? ComponentAttribute { get; set; }
	public IReadOnlyList<ComponentAttributeInfo> IntentFilters { get; set; } = Array.Empty<ComponentAttributeInfo> ();
	public IReadOnlyList<ComponentAttributeInfo> MetaDataEntries { get; set; } = Array.Empty<ComponentAttributeInfo> ();
	public IReadOnlyList<ComponentAttributeInfo> PropertyAttributes { get; set; } = Array.Empty<ComponentAttributeInfo> ();
	public ComponentAttributeInfo? LayoutAttribute { get; set; }
	public IReadOnlyList<ComponentAttributeInfo> GrantUriPermissions { get; set; } = Array.Empty<ComponentAttributeInfo> ();
}
