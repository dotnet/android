using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

sealed class JavaAnnotationParser
{
	sealed record AnnotationTypeInfo (string JavaName, IReadOnlyDictionary<string, string> PropertyNames);

	static readonly IReadOnlyList<JavaAnnotationInfo> noAnnotations = [];

	readonly IReadOnlyDictionary<string, AssemblyIndex> assemblies;
	readonly Func<string, string?> resolveTypeName;
	readonly Dictionary<(AssemblyIndex Index, EntityHandle Type), AnnotationTypeInfo?> annotationTypes = new ();

	public JavaAnnotationParser (IReadOnlyDictionary<string, AssemblyIndex> assemblies, Func<string, string?> resolveTypeName)
	{
		this.assemblies = assemblies;
		this.resolveTypeName = resolveTypeName;
	}

	public IReadOnlyList<JavaAnnotationInfo> Parse (CustomAttributeHandleCollection attributes, AssemblyIndex index)
	{
		List<JavaAnnotationInfo>? annotations = null;
		foreach (var attributeHandle in attributes) {
			var attribute = index.Reader.GetCustomAttribute (attributeHandle);
			var annotationType = GetAnnotationType (attribute, index);
			if (annotationType is null) {
				continue;
			}

			annotations ??= [];
			annotations.Add (new JavaAnnotationInfo {
				Name = annotationType.JavaName,
				Properties = GetProperties (attribute, index, annotationType),
			});
		}
		return annotations ?? noAnnotations;
	}

	static string? GetJavaName (TypeDefinition attributeType, AssemblyIndex index)
	{
		foreach (var markerHandle in attributeType.GetCustomAttributes ()) {
			var marker = index.Reader.GetCustomAttribute (markerHandle);
			if (!AssemblyIndex.IsCustomAttributeMatch (marker, index.Reader, "Android.Runtime", "AnnotationAttribute")) {
				continue;
			}

			var value = index.DecodeAttribute (marker);
			return value.FixedArguments.Length > 0 ? value.FixedArguments [0].Value as string : null;
		}
		return null;
	}

	IReadOnlyList<KeyValuePair<string, string>> GetProperties (
		CustomAttribute attribute,
		AssemblyIndex index,
		AnnotationTypeInfo annotationType)
	{
		var properties = new List<KeyValuePair<string, string>> ();
		foreach (var property in index.DecodeAttribute (attribute).NamedArguments) {
			if (property.Kind != CustomAttributeNamedArgumentKind.Property || property.Name is null) {
				continue;
			}
			var propertyName = annotationType.PropertyNames.TryGetValue (property.Name, out var javaName)
				? javaName
				: property.Name;
			properties.Add (new KeyValuePair<string, string> (
				propertyName,
				ManagedValueToJavaSource (property.Type, property.Value)
			));
		}
		return properties;
	}

	static IReadOnlyDictionary<string, string> GetJavaPropertyNames (TypeDefinition attributeType, AssemblyIndex index)
	{
		var names = new Dictionary<string, string> (StringComparer.Ordinal);
		foreach (var propertyHandle in attributeType.GetProperties ()) {
			var property = index.Reader.GetPropertyDefinition (propertyHandle);
			var managedName = index.Reader.GetString (property.Name);
			foreach (var attributeHandle in property.GetCustomAttributes ()) {
				var attribute = index.Reader.GetCustomAttribute (attributeHandle);
				if (!AssemblyIndex.IsCustomAttributeMatch (attribute, index.Reader, "Android.Runtime", "RegisterAttribute")) {
					continue;
				}
				var value = index.DecodeAttribute (attribute);
				if (value.FixedArguments.Length > 0 && value.FixedArguments [0].Value is string javaName) {
					names [managedName] = javaName;
				}
				break;
			}
		}
		return names;
	}

	AnnotationTypeInfo? GetAnnotationType (CustomAttribute attribute, AssemblyIndex index)
	{
		EntityHandle typeHandle = default;
		if (attribute.Constructor.Kind == HandleKind.MethodDefinition) {
			typeHandle = index.Reader.GetMethodDefinition ((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType ();
		} else if (attribute.Constructor.Kind == HandleKind.MemberReference) {
			typeHandle = index.Reader.GetMemberReference ((MemberReferenceHandle)attribute.Constructor).Parent;
		}

		var key = (index, typeHandle);
		if (typeHandle.IsNil || annotationTypes.TryGetValue (key, out var cached) && cached is null) {
			return null;
		}
		if (cached is not null) {
			return cached;
		}

		TypeDefinition attributeType;
		AssemblyIndex attributeIndex;
		if (typeHandle.Kind == HandleKind.TypeDefinition) {
			attributeType = index.Reader.GetTypeDefinition ((TypeDefinitionHandle)typeHandle);
			attributeIndex = index;
		} else if (typeHandle.Kind == HandleKind.TypeReference) {
			var typeReference = MetadataTypeNameResolver.GetTypeRefFromReference (
				index.Reader,
				(TypeReferenceHandle)typeHandle,
				index.AssemblyName,
				rawTypeKind: 0
			);
			if (!assemblies.TryGetValue (typeReference.AssemblyName, out attributeIndex) ||
			    !attributeIndex.TypesByFullName.TryGetValue (typeReference.ManagedTypeName, out var resolvedHandle)) {
				annotationTypes [key] = null;
				return null;
			}
			attributeType = attributeIndex.Reader.GetTypeDefinition (resolvedHandle);
		} else {
			annotationTypes [key] = null;
			return null;
		}

		var javaName = GetJavaName (attributeType, attributeIndex);
		var result = javaName.IsNullOrEmpty ()
			? null
			: new AnnotationTypeInfo (javaName, GetJavaPropertyNames (attributeType, attributeIndex));
		annotationTypes [key] = result;
		return result;
	}

	string ManagedValueToJavaSource (string managedType, object? value)
	{
		if (value is null) {
			return "null";
		}
		if (managedType == "String" || managedType == "System.String") {
			return ToJavaStringLiteral (value.ToString () ?? "");
		}
		if (managedType == "System.Type" && value is string typeName) {
			var javaName = resolveTypeName (typeName);
			if (javaName is not null) {
				return JniSignatureHelper.JniNameToJavaName (javaName) + ".class";
			}
			throw new InvalidOperationException ($"Java annotation type value '{typeName}' does not resolve to a Java peer.");
		}
		if (value is bool boolean) {
			return boolean ? "true" : "false";
		}
		if (value is IFormattable formattable) {
			return formattable.ToString (null, CultureInfo.InvariantCulture) ?? "";
		}
		return value.ToString () ?? "";
	}

	static string ToJavaStringLiteral (string value)
	{
		var builder = new StringBuilder (value.Length + 2);
		builder.Append ('"');
		foreach (char c in value) {
			switch (c) {
				case '"':
					builder.Append ("\\\"");
					break;
				case '\\':
					builder.Append ("\\\\");
					break;
				case '\b':
					builder.Append ("\\b");
					break;
				case '\t':
					builder.Append ("\\t");
					break;
				case '\n':
					builder.Append ("\\n");
					break;
				case '\f':
					builder.Append ("\\f");
					break;
				case '\r':
					builder.Append ("\\r");
					break;
				default:
					if (char.IsControl (c)) {
						builder.Append ("\\u");
						builder.Append (((int)c).ToString ("x4", CultureInfo.InvariantCulture));
					} else {
						builder.Append (c);
					}
					break;
			}
		}
		builder.Append ('"');
		return builder.ToString ();
	}
}
