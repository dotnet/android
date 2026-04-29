using System.Reflection.Metadata;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Shared logic for resolving fully qualified type names from metadata handles.
/// Used by both <see cref="SignatureTypeProvider"/> and <see cref="CustomAttributeTypeProvider"/>.
/// </summary>
static class MetadataTypeNameResolver
{
	internal static string JoinNamespaceAndName (string ns, string name)
	{
		return ns.Length > 0 ? $"{ns}.{name}" : name;
	}

	internal static string JoinNestedTypeName (string parentName, string name)
	{
		return $"{parentName}+{name}";
	}

	public static string GetFullName (TypeDefinition typeDef, MetadataReader reader)
	{
		var name = reader.GetString (typeDef.Name);
		var ns = reader.GetString (typeDef.Namespace);
		if (typeDef.IsNested) {
			var declaringType = reader.GetTypeDefinition (typeDef.GetDeclaringType ());
			var parentName = GetFullName (declaringType, reader);
			return JoinNestedTypeName (parentName, name);
		}
		return JoinNamespaceAndName (ns, name);
	}

	public static string GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
	{
		return GetFullName (reader.GetTypeDefinition (handle), reader);
	}

	public static TypeRefData GetTypeRefFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, string assemblyName, byte rawTypeKind)
	{
		return new TypeRefData {
			ManagedTypeName = GetTypeFromDefinition (reader, handle, rawTypeKind),
			AssemblyName = assemblyName,
		};
	}

	public static string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
	{
		var typeRef = reader.GetTypeReference (handle);
		var name = reader.GetString (typeRef.Name);
		if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference) {
			var parent = GetTypeFromReference (reader, (TypeReferenceHandle)typeRef.ResolutionScope, rawTypeKind);
			return JoinNestedTypeName (parent, name);
		}
		var ns = reader.GetString (typeRef.Namespace);
		return JoinNamespaceAndName (ns, name);
	}

	public static TypeRefData GetTypeRefFromReference (MetadataReader reader, TypeReferenceHandle handle, string fallbackAssemblyName, byte rawTypeKind)
	{
		var typeRef = reader.GetTypeReference (handle);
		var managedTypeName = GetTypeFromReference (reader, handle, rawTypeKind);
		var assemblyName = GetAssemblyNameFromResolutionScope (reader, typeRef.ResolutionScope, fallbackAssemblyName);

		return new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = assemblyName,
		};
	}

	static string GetAssemblyNameFromResolutionScope (MetadataReader reader, EntityHandle scope, string fallbackAssemblyName)
	{
		switch (scope.Kind) {
			case HandleKind.AssemblyReference:
				return reader.GetString (reader.GetAssemblyReference ((AssemblyReferenceHandle) scope).Name);
			case HandleKind.TypeReference:
				return GetAssemblyNameFromResolutionScope (reader, reader.GetTypeReference ((TypeReferenceHandle) scope).ResolutionScope, fallbackAssemblyName);
			default:
				return fallbackAssemblyName;
		}
	}
}
