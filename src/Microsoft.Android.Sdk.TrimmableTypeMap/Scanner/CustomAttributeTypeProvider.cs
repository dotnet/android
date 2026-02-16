using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Minimal ICustomAttributeTypeProvider implementation for decoding
/// custom attribute values via System.Reflection.Metadata.
/// </summary>
sealed class CustomAttributeTypeProvider (MetadataReader reader) : ICustomAttributeTypeProvider<string>
{
	Dictionary<string, PrimitiveTypeCode>? enumTypeCache;

	public string GetPrimitiveType (PrimitiveTypeCode typeCode) => typeCode.ToString ();

	public string GetTypeFromDefinition (MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind)
		=> MetadataTypeNameResolver.GetTypeFromDefinition (metadataReader, handle, rawTypeKind);

	public string GetTypeFromReference (MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind)
		=> MetadataTypeNameResolver.GetTypeFromReference (metadataReader, handle, rawTypeKind);

	public string GetTypeFromSerializedName (string name) => name;

	public PrimitiveTypeCode GetUnderlyingEnumType (string type)
	{
		if (enumTypeCache == null) {
			enumTypeCache = BuildEnumTypeCache ();
		}

		if (enumTypeCache.TryGetValue (type, out var code)) {
			return code;
		}

		// Default to Int32 for enums defined in other assemblies
		return PrimitiveTypeCode.Int32;
	}

	Dictionary<string, PrimitiveTypeCode> BuildEnumTypeCache ()
	{
		var cache = new Dictionary<string, PrimitiveTypeCode> ();

		foreach (var typeHandle in reader.TypeDefinitions) {
			var typeDef = reader.GetTypeDefinition (typeHandle);

			// Only process enum types
			if (!IsEnum (typeDef))
				continue;

			var fullName = GetTypeFromDefinition (reader, typeHandle, rawTypeKind: 0);
			var code = GetEnumUnderlyingTypeCode (typeDef);
			cache [fullName] = code;
		}

		return cache;
	}

	bool IsEnum (TypeDefinition typeDef)
	{
		var baseType = typeDef.BaseType;
		if (baseType.IsNil)
			return false;

		string? baseFullName = baseType.Kind switch {
			HandleKind.TypeReference => GetTypeFromReference (reader, (TypeReferenceHandle)baseType, rawTypeKind: 0),
			HandleKind.TypeDefinition => GetTypeFromDefinition (reader, (TypeDefinitionHandle)baseType, rawTypeKind: 0),
			_ => null,
		};

		return baseFullName == "System.Enum";
	}

	PrimitiveTypeCode GetEnumUnderlyingTypeCode (TypeDefinition typeDef)
	{
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

		return PrimitiveTypeCode.Int32;
	}

	public string GetSystemType () => "System.Type";

	public string GetSZArrayType (string elementType) => $"{elementType}[]";

	public bool IsSystemType (string type) => type == "System.Type" || type == "Type";
}
