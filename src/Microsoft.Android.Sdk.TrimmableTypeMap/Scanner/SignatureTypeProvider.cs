using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Minimal ISignatureTypeProvider implementation for decoding method
/// signatures via System.Reflection.Metadata.
/// Returns fully qualified type name strings.
/// </summary>
sealed class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
{
	public static readonly SignatureTypeProvider Instance = new ();

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
		=> MetadataTypeNameResolver.GetTypeFromDefinition (reader, handle, rawTypeKind);

	public string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
		=> MetadataTypeNameResolver.GetTypeFromReference (reader, handle, rawTypeKind);

	public string GetTypeFromSpecification (MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
	{
		var typeSpec = reader.GetTypeSpecification (handle);
		return typeSpec.DecodeSignature (this, genericContext);
	}

	public string GetSZArrayType (string elementType) => $"{elementType}[]";
	public string GetArrayType (string elementType, ArrayShape shape) => $"{elementType}[{new string (',', shape.Rank - 1)}]";
	public string GetByReferenceType (string elementType) => $"{elementType}&";
	public string GetPointerType (string elementType) => $"{elementType}*";
	public string GetPinnedType (string elementType) => elementType;
	public string GetModifiedType (string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

	public string GetGenericInstantiation (string genericType, ImmutableArray<string> typeArguments)
	{
		return $"{genericType}<{string.Join (",", typeArguments)}>";
	}

	public string GetGenericTypeParameter (object? genericContext, int index) => $"!{index}";
	public string GetGenericMethodParameter (object? genericContext, int index) => $"!!{index}";

	public string GetFunctionPointerType (MethodSignature<string> signature) => "delegate*";
}

sealed class TypeRefSignatureTypeProvider : ISignatureTypeProvider<TypeRefData, AssemblyIndex>
{
	static readonly TypeRefData VoidType = CreatePrimitiveType ("System.Void");
	static readonly TypeRefData BooleanType = CreatePrimitiveType ("System.Boolean");
	static readonly TypeRefData CharType = CreatePrimitiveType ("System.Char");
	static readonly TypeRefData SByteType = CreatePrimitiveType ("System.SByte");
	static readonly TypeRefData ByteType = CreatePrimitiveType ("System.Byte");
	static readonly TypeRefData Int16Type = CreatePrimitiveType ("System.Int16");
	static readonly TypeRefData UInt16Type = CreatePrimitiveType ("System.UInt16");
	static readonly TypeRefData Int32Type = CreatePrimitiveType ("System.Int32");
	static readonly TypeRefData UInt32Type = CreatePrimitiveType ("System.UInt32");
	static readonly TypeRefData Int64Type = CreatePrimitiveType ("System.Int64");
	static readonly TypeRefData UInt64Type = CreatePrimitiveType ("System.UInt64");
	static readonly TypeRefData SingleType = CreatePrimitiveType ("System.Single");
	static readonly TypeRefData DoubleType = CreatePrimitiveType ("System.Double");
	static readonly TypeRefData StringType = CreatePrimitiveType ("System.String");
	static readonly TypeRefData ObjectType = CreatePrimitiveType ("System.Object");
	static readonly TypeRefData IntPtrType = CreatePrimitiveType ("System.IntPtr");
	static readonly TypeRefData UIntPtrType = CreatePrimitiveType ("System.UIntPtr");
	static readonly TypeRefData TypedReferenceType = CreatePrimitiveType ("System.TypedReference");
	readonly AssemblyIndex index;

	internal TypeRefSignatureTypeProvider (AssemblyIndex index)
	{
		this.index = index;
	}

	public TypeRefData GetPrimitiveType (PrimitiveTypeCode typeCode) => typeCode switch {
		PrimitiveTypeCode.Void => VoidType,
		PrimitiveTypeCode.Boolean => BooleanType,
		PrimitiveTypeCode.Char => CharType,
		PrimitiveTypeCode.SByte => SByteType,
		PrimitiveTypeCode.Byte => ByteType,
		PrimitiveTypeCode.Int16 => Int16Type,
		PrimitiveTypeCode.UInt16 => UInt16Type,
		PrimitiveTypeCode.Int32 => Int32Type,
		PrimitiveTypeCode.UInt32 => UInt32Type,
		PrimitiveTypeCode.Int64 => Int64Type,
		PrimitiveTypeCode.UInt64 => UInt64Type,
		PrimitiveTypeCode.Single => SingleType,
		PrimitiveTypeCode.Double => DoubleType,
		PrimitiveTypeCode.String => StringType,
		PrimitiveTypeCode.Object => ObjectType,
		PrimitiveTypeCode.IntPtr => IntPtrType,
		PrimitiveTypeCode.UIntPtr => UIntPtrType,
		PrimitiveTypeCode.TypedReference => TypedReferenceType,
		_ => CreatePrimitiveType (typeCode.ToString ()),
	};

	static TypeRefData CreatePrimitiveType (string managedTypeName) => new () {
		ManagedTypeName = managedTypeName,
		AssemblyName = "System.Runtime",
	};

	// Each provider is owned by the AssemblyIndex for the MetadataReader being decoded.
	public TypeRefData GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
		=> index.GetTypeRef (handle, rawTypeKind);

	public TypeRefData GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
		=> index.GetTypeRef (handle, rawTypeKind);

	public TypeRefData GetTypeFromSpecification (MetadataReader reader, AssemblyIndex genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
	{
		var typeSpec = reader.GetTypeSpecification (handle);
		return typeSpec.DecodeSignature (this, genericContext);
	}

	public TypeRefData GetSZArrayType (TypeRefData elementType) => elementType with {
		ManagedTypeName = $"{elementType.ManagedTypeName}[]",
	};

	public TypeRefData GetArrayType (TypeRefData elementType, ArrayShape shape) => elementType with {
		ManagedTypeName = $"{elementType.ManagedTypeName}[{new string (',', shape.Rank - 1)}]",
	};

	public TypeRefData GetByReferenceType (TypeRefData elementType) => elementType with {
		ManagedTypeName = $"{elementType.ManagedTypeName}&",
	};

	public TypeRefData GetPointerType (TypeRefData elementType) => elementType with {
		ManagedTypeName = $"{elementType.ManagedTypeName}*",
	};

	public TypeRefData GetPinnedType (TypeRefData elementType) => elementType;
	public TypeRefData GetModifiedType (TypeRefData modifier, TypeRefData unmodifiedType, bool isRequired) => unmodifiedType;

	public TypeRefData GetGenericInstantiation (TypeRefData genericType, ImmutableArray<TypeRefData> typeArguments)
	{
		return genericType with { GenericArguments = typeArguments.ToArray () };
	}

	public TypeRefData GetGenericTypeParameter (AssemblyIndex genericContext, int index) => new () {
		ManagedTypeName = $"!{index}",
		AssemblyName = genericContext.AssemblyName,
	};

	public TypeRefData GetGenericMethodParameter (AssemblyIndex genericContext, int index) => new () {
		ManagedTypeName = $"!!{index}",
		AssemblyName = genericContext.AssemblyName,
	};

	public TypeRefData GetFunctionPointerType (MethodSignature<TypeRefData> signature) => new () {
		ManagedTypeName = "delegate*",
		AssemblyName = "System.Runtime",
	};
}
