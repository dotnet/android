using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Extracts component attribute data ([Activity], [Service], etc.) and sub-attributes
/// ([IntentFilter], [MetaData], [Layout], [Property], [GrantUriPermission])
/// from SRM CustomAttribute blobs into <see cref="ComponentData"/>.
/// </summary>
static class ComponentAttributeExtractor
{
	// Component attribute type names (without "Attribute" suffix for matching)
	static readonly HashSet<string> ComponentAttributeNames = new (StringComparer.Ordinal) {
		"ActivityAttribute",
		"ServiceAttribute",
		"BroadcastReceiverAttribute",
		"ContentProviderAttribute",
		"ApplicationAttribute",
		"InstrumentationAttribute",
	};

	static readonly HashSet<string> SubAttributeNames = new (StringComparer.Ordinal) {
		"IntentFilterAttribute",
		"MetaDataAttribute",
		"PropertyAttribute",
		"LayoutAttribute",
		"GrantUriPermissionAttribute",
	};

	/// <summary>
	/// Extracts all component and sub-attribute data from a type's custom attributes.
	/// </summary>
	public static ComponentData Extract (MetadataReader reader, TypeDefinitionHandle typeHandle)
	{
		var typeDef = reader.GetTypeDefinition (typeHandle);
		var provider = new CustomAttributeTypeProvider (reader);
		var result = new ComponentData ();

		foreach (var caHandle in typeDef.GetCustomAttributes ()) {
			var ca = reader.GetCustomAttribute (caHandle);
			var attrName = GetAttributeName (reader, ca);
			if (attrName == null)
				continue;

			if (ComponentAttributeNames.Contains (attrName)) {
				result.ComponentKind = GetComponentKind (attrName);
				result.ComponentAttribute = DecodeAttribute (reader, ca, provider, attrName);
			} else if (SubAttributeNames.Contains (attrName)) {
				var info = DecodeAttribute (reader, ca, provider, attrName);
				switch (attrName) {
				case "IntentFilterAttribute":
					result.IntentFilters.Add (info);
					break;
				case "MetaDataAttribute":
					result.MetaDataEntries.Add (info);
					break;
				case "PropertyAttribute":
					result.PropertyAttributes.Add (info);
					break;
				case "LayoutAttribute":
					result.LayoutAttribute = info;
					break;
				case "GrantUriPermissionAttribute":
					result.GrantUriPermissions.Add (info);
					break;
				}
			}
		}

		return result;
	}

	static ComponentAttributeInfo DecodeAttribute (MetadataReader reader, CustomAttribute ca, CustomAttributeTypeProvider provider, string attrName)
	{
		var decoded = ca.DecodeValue (provider);
		var properties = new Dictionary<string, object> (StringComparer.Ordinal);
		var ctorArgs = new List<object> ();

		foreach (var fixedArg in decoded.FixedArguments) {
			ctorArgs.Add (NormalizeValue (fixedArg.Value));
		}

		foreach (var named in decoded.NamedArguments) {
			if (named.Name != null)
				properties [named.Name] = NormalizeValue (named.Value);
		}

		var fullAttrName = GetFullAttributeTypeName (attrName);

		return new ComponentAttributeInfo {
			AttributeType = fullAttrName,
			Properties = properties,
			ConstructorArguments = ctorArgs,
		};
	}

	/// <summary>
	/// Normalizes SRM decoded values (boxed primitives, ImmutableArray) to consistent types.
	/// </summary>
	static object NormalizeValue (object? value)
	{
		if (value == null)
			return "";

		// ImmutableArray<CustomAttributeTypedArgument<string>> for array-typed args
		if (value is System.Collections.Immutable.ImmutableArray<CustomAttributeTypedArgument<string>> typedArray) {
			var result = new string [typedArray.Length];
			for (int i = 0; i < typedArray.Length; i++) {
				result [i] = typedArray [i].Value?.ToString () ?? "";
			}
			return result;
		}

		return value;
	}

	static ManifestComponentKind GetComponentKind (string attrName)
	{
		return attrName switch {
			"ActivityAttribute" => ManifestComponentKind.Activity,
			"ServiceAttribute" => ManifestComponentKind.Service,
			"BroadcastReceiverAttribute" => ManifestComponentKind.BroadcastReceiver,
			"ContentProviderAttribute" => ManifestComponentKind.ContentProvider,
			"ApplicationAttribute" => ManifestComponentKind.Application,
			"InstrumentationAttribute" => ManifestComponentKind.Instrumentation,
			_ => ManifestComponentKind.None,
		};
	}

	static string GetFullAttributeTypeName (string shortName)
	{
		return shortName switch {
			"ActivityAttribute" => "Android.App.ActivityAttribute",
			"ServiceAttribute" => "Android.App.ServiceAttribute",
			"InstrumentationAttribute" => "Android.App.InstrumentationAttribute",
			"ApplicationAttribute" => "Android.App.ApplicationAttribute",
			"BroadcastReceiverAttribute" => "Android.Content.BroadcastReceiverAttribute",
			"ContentProviderAttribute" => "Android.Content.ContentProviderAttribute",
			"IntentFilterAttribute" => "Android.App.IntentFilterAttribute",
			"MetaDataAttribute" => "Android.App.MetaDataAttribute",
			"PropertyAttribute" => "Android.App.PropertyAttribute",
			"LayoutAttribute" => "Android.App.LayoutAttribute",
			"GrantUriPermissionAttribute" => "Android.Content.GrantUriPermissionAttribute",
			_ => shortName,
		};
	}

	static string? GetAttributeName (MetadataReader reader, CustomAttribute ca)
	{
		if (ca.Constructor.Kind == HandleKind.MemberReference) {
			var memberRef = reader.GetMemberReference ((MemberReferenceHandle)ca.Constructor);
			if (memberRef.Parent.Kind == HandleKind.TypeReference) {
				var typeRef = reader.GetTypeReference ((TypeReferenceHandle)memberRef.Parent);
				return reader.GetString (typeRef.Name);
			}
		} else if (ca.Constructor.Kind == HandleKind.MethodDefinition) {
			var methodDef = reader.GetMethodDefinition ((MethodDefinitionHandle)ca.Constructor);
			var declaringType = reader.GetTypeDefinition (methodDef.GetDeclaringType ());
			return reader.GetString (declaringType.Name);
		}
		return null;
	}
}

/// <summary>
/// Result of extracting component attributes from a type.
/// </summary>
sealed class ComponentData
{
	public ManifestComponentKind ComponentKind { get; set; }
	public ComponentAttributeInfo? ComponentAttribute { get; set; }
	public List<ComponentAttributeInfo> IntentFilters { get; } = new ();
	public List<ComponentAttributeInfo> MetaDataEntries { get; } = new ();
	public List<ComponentAttributeInfo> PropertyAttributes { get; } = new ();
	public ComponentAttributeInfo? LayoutAttribute { get; set; }
	public List<ComponentAttributeInfo> GrantUriPermissions { get; } = new ();
}
