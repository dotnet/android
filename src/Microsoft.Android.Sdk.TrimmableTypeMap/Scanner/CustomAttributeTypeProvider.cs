using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

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

	public string GetTypeFromDefinition (MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind)
	{
		var typeDef = metadataReader.GetTypeDefinition (handle);
		var name = metadataReader.GetString (typeDef.Name);
		if (typeDef.IsNested) {
			var parent = GetTypeFromDefinition (metadataReader, typeDef.GetDeclaringType (), rawTypeKind);
			return parent + "+" + name;
		}
		var ns = metadataReader.GetString (typeDef.Namespace);
		return ns.Length > 0 ? ns + "." + name : name;
	}

	public string GetTypeFromReference (MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind)
	{
		var typeRef = metadataReader.GetTypeReference (handle);
		var name = metadataReader.GetString (typeRef.Name);
		if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference) {
			var parent = GetTypeFromReference (metadataReader, (TypeReferenceHandle)typeRef.ResolutionScope, rawTypeKind);
			return parent + "+" + name;
		}
		var ns = metadataReader.GetString (typeRef.Namespace);
		return ns.Length > 0 ? ns + "." + name : name;
	}

	public string GetTypeFromSerializedName (string name) => name;

	public PrimitiveTypeCode GetUnderlyingEnumType (string type)
	{
		// Find the enum type in this assembly's metadata and read its value__ field type.
		foreach (var typeHandle in reader.TypeDefinitions) {
			var typeDef = reader.GetTypeDefinition (typeHandle);
			var name = reader.GetString (typeDef.Name);
			var ns = reader.GetString (typeDef.Namespace);
			var fullName = ns.Length > 0 ? ns + "." + name : name;

			if (fullName != type)
				continue;

			// For enums, the first instance field is the underlying value__ field
			foreach (var fieldHandle in typeDef.GetFields ()) {
				var field = reader.GetFieldDefinition (fieldHandle);
				if ((field.Attributes & System.Reflection.FieldAttributes.Static) != 0)
					continue;

				var sig = field.DecodeSignature (SignatureTypeProvider.Instance, genericContext: null);
				return sig switch {
					"System.Byte" => PrimitiveTypeCode.Byte,
					"System.SByte" => PrimitiveTypeCode.SByte,
					"System.Int16" => PrimitiveTypeCode.Int16,
					"System.UInt16" => PrimitiveTypeCode.UInt16,
					"System.Int32" => PrimitiveTypeCode.Int32,
					"System.UInt32" => PrimitiveTypeCode.UInt32,
					"System.Int64" => PrimitiveTypeCode.Int64,
					"System.UInt64" => PrimitiveTypeCode.UInt64,
					_ => PrimitiveTypeCode.Int32,
				};
			}
		}

		// Default to Int32 for enums defined in other assemblies
		return PrimitiveTypeCode.Int32;
	}

	public string GetSystemType () => "System.Type";

	public string GetSZArrayType (string elementType) => elementType + "[]";

	public bool IsSystemType (string type) => type == "System.Type" || type == "Type";
}
