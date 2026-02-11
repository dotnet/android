using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Minimal ICustomAttributeTypeProvider implementation for decoding
/// custom attribute values via System.Reflection.Metadata.
/// </summary>
sealed class CustomAttributeTypeProvider : ICustomAttributeTypeProvider<string>
{
	readonly MetadataReader reader;

	public CustomAttributeTypeProvider (MetadataReader reader)
	{
		this.reader = reader;
	}

	public string GetPrimitiveType (PrimitiveTypeCode typeCode) => typeCode.ToString ();

	public string GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
	{
		var typeDef = reader.GetTypeDefinition (handle);
		var ns = reader.GetString (typeDef.Namespace);
		var name = reader.GetString (typeDef.Name);
		return ns.Length > 0 ? ns + "." + name : name;
	}

	public string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
	{
		var typeRef = reader.GetTypeReference (handle);
		var ns = reader.GetString (typeRef.Namespace);
		var name = reader.GetString (typeRef.Name);
		return ns.Length > 0 ? ns + "." + name : name;
	}

	public string GetTypeFromSerializedName (string name) => name;

	public PrimitiveTypeCode GetUnderlyingEnumType (string type) => PrimitiveTypeCode.Int32;

	public string GetSystemType () => "System.Type";

	public string GetSZArrayType (string elementType) => elementType + "[]";

	public bool IsSystemType (string type) => type == "System.Type" || type == "Type";
}
