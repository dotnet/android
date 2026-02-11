using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Minimal ISignatureTypeProvider implementation for decoding method
/// signatures via System.Reflection.Metadata.
/// Returns fully qualified type name strings.
/// </summary>
sealed class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
{
	public string GetPrimitiveType (PrimitiveTypeCode typeCode) => typeCode switch {
		PrimitiveTypeCode.Void => "System.Void",
		PrimitiveTypeCode.Boolean => "System.Boolean",
		PrimitiveTypeCode.Char => "System.Char",
		PrimitiveTypeCode.SByte => "System.SByte",
		PrimitiveTypeCode.Byte => "System.Byte",
		PrimitiveTypeCode.Int16 => "System.Int16",
		PrimitiveTypeCode.UInt16 => "System.UInt16",
		PrimitiveTypeCode.Int32 => "System.Int32",
		PrimitiveTypeCode.UInt32 => "System.UInt32",
		PrimitiveTypeCode.Int64 => "System.Int64",
		PrimitiveTypeCode.UInt64 => "System.UInt64",
		PrimitiveTypeCode.Single => "System.Single",
		PrimitiveTypeCode.Double => "System.Double",
		PrimitiveTypeCode.String => "System.String",
		PrimitiveTypeCode.Object => "System.Object",
		PrimitiveTypeCode.IntPtr => "System.IntPtr",
		PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
		PrimitiveTypeCode.TypedReference => "System.TypedReference",
		_ => typeCode.ToString (),
	};

	public string GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
	{
		var typeDef = reader.GetTypeDefinition (handle);
		var ns = reader.GetString (typeDef.Namespace);
		var name = reader.GetString (typeDef.Name);
		if (typeDef.IsNested) {
			var parent = GetTypeFromDefinition (reader, typeDef.GetDeclaringType (), rawTypeKind);
			return parent + "+" + name;
		}
		return ns.Length > 0 ? ns + "." + name : name;
	}

	public string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
	{
		var typeRef = reader.GetTypeReference (handle);
		var ns = reader.GetString (typeRef.Namespace);
		var name = reader.GetString (typeRef.Name);
		return ns.Length > 0 ? ns + "." + name : name;
	}

	public string GetTypeFromSpecification (MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
	{
		var typeSpec = reader.GetTypeSpecification (handle);
		return typeSpec.DecodeSignature (this, genericContext);
	}

	public string GetSZArrayType (string elementType) => elementType + "[]";
	public string GetArrayType (string elementType, ArrayShape shape) => elementType + "[" + new string (',', shape.Rank - 1) + "]";
	public string GetByReferenceType (string elementType) => elementType + "&";
	public string GetPointerType (string elementType) => elementType + "*";
	public string GetPinnedType (string elementType) => elementType;
	public string GetModifiedType (string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

	public string GetGenericInstantiation (string genericType, ImmutableArray<string> typeArguments)
	{
		return genericType + "<" + string.Join (",", typeArguments) + ">";
	}

	public string GetGenericTypeParameter (object? genericContext, int index) => "!" + index;
	public string GetGenericMethodParameter (object? genericContext, int index) => "!!" + index;

	public string GetFunctionPointerType (MethodSignature<string> signature) => "delegate*";
}
