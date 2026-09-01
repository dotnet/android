using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

enum ValueTypeContainerKind
{
	List,
	Collection,
	Dictionary,
}

sealed record ValueTypeContainerRoot
{
	public required ValueTypeContainerKind Kind { get; init; }
	public required IReadOnlyList<TypeRefData> TypeArguments { get; init; }

	public string DisplayName => $"{Kind}<{string.Join (",", TypeArguments.Select (t => t.DisplayName))}>";
}

sealed class MethodScanTraversal
{
	public HashSet<string> ActiveDefinitions { get; } = new (StringComparer.Ordinal);
	public HashSet<string> ScannedInstances { get; } = new (StringComparer.Ordinal);
}

static class ValueTypeContainerScanner
{
	static readonly Dictionary<ushort, OperandType> OperandTypes = CreateOperandTypes ();

	public static List<ValueTypeContainerRoot> Scan (
		IReadOnlyList<AssemblyInput> assemblies,
		HashSet<string> frameworkAssemblyNames)
	{
		var roots = new SortedDictionary<string, ValueTypeContainerRoot> (StringComparer.Ordinal);
		var indexes = new List<AssemblyIndex> ();
		var indexesByAssembly = new Dictionary<string, AssemblyIndex> (StringComparer.OrdinalIgnoreCase);
		foreach (var assembly in assemblies) {
			if (frameworkAssemblyNames.Contains (assembly.Name)) {
				continue;
			}

			var index = AssemblyIndex.Create (assembly.Reader, assembly.Name, assembly.Path);
			indexes.Add (index);
			indexesByAssembly [assembly.Name] = index;
		}
		var traversal = new MethodScanTraversal ();
		try {
			foreach (var index in indexes) {
				ScanAssembly (index, indexesByAssembly, roots, traversal);
			}
		} finally {
			foreach (var index in indexes) {
				index.Dispose ();
			}
		}
		return roots.Values.ToList ();
	}

	static void ScanAssembly (
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		MethodScanTraversal traversal)
	{
		var reader = index.Reader;
		foreach (var typeHandle in reader.TypeDefinitions) {
			var type = reader.GetTypeDefinition (typeHandle);
			foreach (var fieldHandle in type.GetFields ()) {
				AddType (reader.GetFieldDefinition (fieldHandle).DecodeSignature (index.TypeRefSignatureProvider, index), roots);
			}
			foreach (var methodHandle in type.GetMethods ()) {
				AddMethodDefinition (methodHandle, index, indexesByAssembly, roots, [], [], traversal);
			}
			foreach (var propertyHandle in type.GetProperties ()) {
				var signature = reader.GetPropertyDefinition (propertyHandle).DecodeSignature (index.TypeRefSignatureProvider, index);
				AddMethodSignature (signature, roots, [], []);
			}
		}

		int memberReferenceCount = reader.GetTableRowCount (TableIndex.MemberRef);
		for (int row = 1; row <= memberReferenceCount; row++) {
			AddMemberReference (
				reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (row)),
				index,
				indexesByAssembly,
				roots,
				[],
				[],
				[],
				traversal);
		}

		int methodSpecificationCount = reader.GetTableRowCount (TableIndex.MethodSpec);
		for (int row = 1; row <= methodSpecificationCount; row++) {
			AddMethodSpecification (
				reader.GetMethodSpecification (MetadataTokens.MethodSpecificationHandle (row)),
				index,
				indexesByAssembly,
				roots,
				[],
				[],
				traversal);
		}
	}

	static void AddMethodDefinition (
		MethodDefinitionHandle methodHandle,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments,
		MethodScanTraversal traversal)
	{
		var instanceKey = GetMethodInstanceKey (index, methodHandle, typeArguments, methodArguments);
		if (!traversal.ScannedInstances.Add (instanceKey)) {
			return;
		}
		var method = index.Reader.GetMethodDefinition (methodHandle);
		AddMethodSignature (
			method.DecodeSignature (index.TypeRefSignatureProvider, index),
			roots,
			typeArguments,
			methodArguments);
		var definitionKey = $"{index.AssemblyName}:{MetadataTokens.GetRowNumber (methodHandle)}";
		if (!traversal.ActiveDefinitions.Add (definitionKey)) {
			AddMethodBody (
				method,
				index,
				indexesByAssembly,
				roots,
				typeArguments,
				methodArguments,
				traversal,
				followMethodCalls: false);
			return;
		}
		try {
			AddMethodBody (
				method,
				index,
				indexesByAssembly,
				roots,
				typeArguments,
				methodArguments,
				traversal,
				followMethodCalls: true);
		} finally {
			traversal.ActiveDefinitions.Remove (definitionKey);
		}
	}

	static void AddMethodBody (
		MethodDefinition method,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments,
		MethodScanTraversal traversal,
		bool followMethodCalls)
	{
		if (method.RelativeVirtualAddress == 0 ||
				(method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL) {
			return;
		}

		var body = index.GetMethodBody (method.RelativeVirtualAddress);
		if (!body.LocalSignature.IsNil) {
			var locals = index.Reader.GetStandaloneSignature (body.LocalSignature)
				.DecodeLocalSignature (index.TypeRefSignatureProvider, index);
			foreach (var local in locals) {
				AddType (SubstituteGenericParameters (local, typeArguments, methodArguments), roots);
			}
		}
		var il = body.GetILBytes ();
		if (il is not null) {
			AddIlUsages (il, index, indexesByAssembly, roots, typeArguments, methodArguments, traversal, followMethodCalls);
		}
	}

	static void AddMemberReference (
		MemberReference memberReference,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> callerTypeArguments,
		IReadOnlyList<TypeRefData> callerMethodArguments,
		IReadOnlyList<TypeRefData> referencedMethodArguments,
		MethodScanTraversal traversal)
	{
		IReadOnlyList<TypeRefData> referencedTypeArguments = callerTypeArguments;
		if (memberReference.Parent.Kind == HandleKind.TypeSpecification) {
			var parent = index.Reader.GetTypeSpecification ((TypeSpecificationHandle) memberReference.Parent)
				.DecodeSignature (index.TypeRefSignatureProvider, index);
			parent = SubstituteGenericParameters (parent, callerTypeArguments, callerMethodArguments);
			referencedTypeArguments = parent.GenericArguments;
		}

		if (memberReference.GetKind () == MemberReferenceKind.Field) {
			var fieldType = memberReference.DecodeFieldSignature (index.TypeRefSignatureProvider, index);
			AddType (SubstituteGenericParameters (fieldType, referencedTypeArguments, referencedMethodArguments), roots);
			return;
		}

		var signature = memberReference.DecodeMethodSignature (index.TypeRefSignatureProvider, index);
		AddMethodSignature (
			signature,
			roots,
			referencedTypeArguments,
			referencedMethodArguments);
		AddLocalMemberReferenceBodies (
			memberReference,
			signature,
			index,
			indexesByAssembly,
			roots,
			referencedTypeArguments,
			referencedMethodArguments,
			traversal);
	}

	static void AddMethodSpecification (
		MethodSpecification specification,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> callerTypeArguments,
		IReadOnlyList<TypeRefData> callerMethodArguments,
		MethodScanTraversal traversal)
	{
		var methodArguments = specification.DecodeSignature (index.TypeRefSignatureProvider, index)
			.Select (argument => SubstituteGenericParameters (argument, callerTypeArguments, callerMethodArguments))
			.ToArray ();
		foreach (var argument in methodArguments) {
			AddType (argument, roots);
		}

		if (specification.Method.Kind == HandleKind.MethodDefinition) {
			AddMethodDefinition (
				(MethodDefinitionHandle) specification.Method,
				index,
				indexesByAssembly,
				roots,
				callerTypeArguments,
				methodArguments,
				traversal);
		} else if (specification.Method.Kind == HandleKind.MemberReference) {
			AddMemberReference (
				index.Reader.GetMemberReference ((MemberReferenceHandle) specification.Method),
				index,
				indexesByAssembly,
				roots,
				callerTypeArguments,
				callerMethodArguments,
				methodArguments,
				traversal);
		}
	}

	static void AddLocalMemberReferenceBodies (
		MemberReference memberReference,
		MethodSignature<TypeRefData> memberSignature,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments,
		MethodScanTraversal traversal)
	{
		if (!TryGetDeclaringType (
				memberReference.Parent,
				index,
				indexesByAssembly,
				out var declaringIndex,
				out var declaringTypeHandle)) {
			return;
		}

		var methodName = index.Reader.GetString (memberReference.Name);
		var declaringType = declaringIndex.Reader.GetTypeDefinition (declaringTypeHandle);
		foreach (var methodHandle in declaringType.GetMethods ()) {
			var method = declaringIndex.Reader.GetMethodDefinition (methodHandle);
			if (declaringIndex.Reader.GetString (method.Name) == ".cctor") {
				AddMethodDefinition (
					methodHandle,
					declaringIndex,
					indexesByAssembly,
					roots,
					typeArguments,
					[],
					traversal);
			}
		}
		foreach (var methodHandle in declaringType.GetMethods ()) {
			var method = declaringIndex.Reader.GetMethodDefinition (methodHandle);
			if (declaringIndex.Reader.GetString (method.Name) != methodName ||
					method.GetGenericParameters ().Count != memberSignature.GenericParameterCount) {
				continue;
			}
			var signature = method.DecodeSignature (declaringIndex.TypeRefSignatureProvider, declaringIndex);
			if (!HaveSameClosedSignature (memberSignature, signature, typeArguments, methodArguments)) {
				continue;
			}
			AddMethodDefinition (
				methodHandle,
				declaringIndex,
				indexesByAssembly,
				roots,
				typeArguments,
				methodArguments,
				traversal);
		}
	}

	static bool HaveSameClosedSignature (
		MethodSignature<TypeRefData> memberSignature,
		MethodSignature<TypeRefData> definitionSignature,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments)
	{
		if (memberSignature.ParameterTypes.Length != definitionSignature.ParameterTypes.Length ||
				!SubstituteGenericParameters (memberSignature.ReturnType, typeArguments, methodArguments).Equals (
					SubstituteGenericParameters (definitionSignature.ReturnType, typeArguments, methodArguments))) {
			return false;
		}
		for (int i = 0; i < memberSignature.ParameterTypes.Length; i++) {
			if (!SubstituteGenericParameters (memberSignature.ParameterTypes [i], typeArguments, methodArguments).Equals (
					SubstituteGenericParameters (definitionSignature.ParameterTypes [i], typeArguments, methodArguments))) {
				return false;
			}
		}
		return true;
	}

	static bool TryGetDeclaringType (
		EntityHandle parentHandle,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		out AssemblyIndex declaringIndex,
		out TypeDefinitionHandle declaringTypeHandle)
	{
		if (parentHandle.Kind == HandleKind.TypeDefinition) {
			declaringIndex = index;
			declaringTypeHandle = (TypeDefinitionHandle) parentHandle;
			return true;
		}

		TypeRefData? parent = null;
		if (parentHandle.Kind == HandleKind.TypeReference) {
			parent = index.GetTypeRef ((TypeReferenceHandle) parentHandle, rawTypeKind: 0);
		} else if (parentHandle.Kind == HandleKind.TypeSpecification) {
			parent = index.Reader.GetTypeSpecification ((TypeSpecificationHandle) parentHandle)
				.DecodeSignature (index.TypeRefSignatureProvider, index);
		}
		if (parent is not null &&
				indexesByAssembly.TryGetValue (parent.AssemblyName, out declaringIndex) &&
				declaringIndex.TypesByFullName.TryGetValue (parent.ManagedTypeName, out declaringTypeHandle)) {
			return true;
		}
		declaringIndex = index;
		declaringTypeHandle = default;
		return false;
	}

	static void AddMethodSignature (
		MethodSignature<TypeRefData> signature,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments)
	{
		AddType (SubstituteGenericParameters (signature.ReturnType, typeArguments, methodArguments), roots);
		foreach (var parameter in signature.ParameterTypes) {
			AddType (SubstituteGenericParameters (parameter, typeArguments, methodArguments), roots);
		}
	}

	static void AddIlUsages (
		byte[] il,
		AssemblyIndex index,
		IReadOnlyDictionary<string, AssemblyIndex> indexesByAssembly,
		SortedDictionary<string, ValueTypeContainerRoot> roots,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments,
		MethodScanTraversal traversal,
		bool followMethodCalls)
	{
		for (int offset = 0; offset < il.Length;) {
			ushort value = il [offset++];
			if (value == 0xfe) {
				EnsureRemainingBytes (il, offset, 1);
				value = (ushort) (0xfe00 | il [offset++]);
			}
			if (!OperandTypes.TryGetValue (value, out var operandType)) {
				throw new BadImageFormatException ($"Unknown IL opcode 0x{value:x4}.");
			}

			int operandSize = GetOperandSize (operandType, il, offset);
			EnsureRemainingBytes (il, offset, operandSize);
			if (operandType == OperandType.InlineType || value == (ushort) OpCodes.Ldtoken.Value) {
				var handle = MetadataTokens.EntityHandle (BitConverter.ToInt32 (il, offset));
				if (handle.Kind == HandleKind.TypeSpecification) {
					var specification = index.Reader.GetTypeSpecification ((TypeSpecificationHandle) handle);
					var type = specification.DecodeSignature (index.TypeRefSignatureProvider, index);
					AddType (SubstituteGenericParameters (type, typeArguments, methodArguments), roots);
				}
			}
			if (followMethodCalls && operandType == OperandType.InlineMethod) {
				var handle = MetadataTokens.EntityHandle (BitConverter.ToInt32 (il, offset));
				if (handle.Kind == HandleKind.MethodDefinition) {
					AddMethodDefinition (
						(MethodDefinitionHandle) handle,
						index,
						indexesByAssembly,
						roots,
						typeArguments,
						[],
						traversal);
				} else if (handle.Kind == HandleKind.MemberReference) {
					AddMemberReference (
						index.Reader.GetMemberReference ((MemberReferenceHandle) handle),
						index,
						indexesByAssembly,
						roots,
						typeArguments,
						methodArguments,
						[],
						traversal);
				} else if (handle.Kind == HandleKind.MethodSpecification) {
					AddMethodSpecification (
						index.Reader.GetMethodSpecification ((MethodSpecificationHandle) handle),
						index,
						indexesByAssembly,
						roots,
						typeArguments,
						methodArguments,
						traversal);
				}
			}
			offset += operandSize;
		}
	}

	static TypeRefData SubstituteGenericParameters (
		TypeRefData type,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments)
	{
		if (TryGetArraySuffix (type.ManagedTypeName, out var elementTypeName, out var suffix)) {
			var elementType = SubstituteGenericParameters (
				type with { ManagedTypeName = elementTypeName },
				typeArguments,
				methodArguments);
			return elementType with { ManagedTypeName = elementType.ManagedTypeName + suffix };
		}
		if (TryGetGenericParameterIndex (type.ManagedTypeName, "!!", out var methodIndex) &&
				methodIndex < methodArguments.Count) {
			return methodArguments [methodIndex];
		}
		if (TryGetGenericParameterIndex (type.ManagedTypeName, "!", out var typeIndex) &&
				typeIndex < typeArguments.Count) {
			return typeArguments [typeIndex];
		}
		if (type.GenericArguments.Count == 0) {
			return type;
		}
		return type with {
			GenericArguments = type.GenericArguments
				.Select (argument => SubstituteGenericParameters (argument, typeArguments, methodArguments))
				.ToArray (),
		};
	}

	static bool TryGetGenericParameterIndex (string typeName, string prefix, out int index)
	{
		if (typeName.StartsWith (prefix, StringComparison.Ordinal) &&
				int.TryParse (typeName.Substring (prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out index)) {
			return true;
		}
		index = 0;
		return false;
	}

	static int GetOperandSize (OperandType operandType, byte[] il, int operandOffset)
	{
		switch (operandType) {
		case OperandType.InlineNone:
			return 0;
		case OperandType.ShortInlineBrTarget:
		case OperandType.ShortInlineI:
		case OperandType.ShortInlineVar:
			return 1;
		case OperandType.InlineVar:
			return 2;
		case OperandType.InlineBrTarget:
		case OperandType.InlineField:
		case OperandType.InlineI:
		case OperandType.InlineMethod:
		case OperandType.InlineSig:
		case OperandType.InlineString:
		case OperandType.InlineTok:
		case OperandType.InlineType:
		case OperandType.ShortInlineR:
			return 4;
		case OperandType.InlineI8:
		case OperandType.InlineR:
			return 8;
		case OperandType.InlineSwitch:
			EnsureRemainingBytes (il, operandOffset, 4);
			int count = BitConverter.ToInt32 (il, operandOffset);
			if (count < 0 || count > (il.Length - operandOffset - 4) / 4) {
				throw new BadImageFormatException ("Invalid IL switch operand.");
			}
			return 4 + count * 4;
		default:
			throw new BadImageFormatException ($"Unsupported IL operand type '{operandType}'.");
		}
	}

	static void EnsureRemainingBytes (byte[] il, int offset, int count)
	{
		if (offset < 0 || count < 0 || offset > il.Length - count) {
			throw new BadImageFormatException ("Unexpected end of IL stream.");
		}
	}

	static Dictionary<ushort, OperandType> CreateOperandTypes ()
	{
		var result = new Dictionary<ushort, OperandType> ();
		foreach (var field in typeof (OpCodes).GetFields (BindingFlags.Public | BindingFlags.Static)) {
			if (field.GetValue (null) is OpCode opCode) {
				result [(ushort) opCode.Value] = opCode.OperandType;
			}
		}
		return result;
	}

	static string GetMethodInstanceKey (
		AssemblyIndex index,
		MethodDefinitionHandle methodHandle,
		IReadOnlyList<TypeRefData> typeArguments,
		IReadOnlyList<TypeRefData> methodArguments)
		=> $"{index.AssemblyName}:{MetadataTokens.GetRowNumber (methodHandle)}:" +
			$"{string.Join (",", typeArguments.Select (GetTypeKey))}:" +
			$"{string.Join (",", methodArguments.Select (GetTypeKey))}";

	static void AddType (TypeRefData type, SortedDictionary<string, ValueTypeContainerRoot> roots)
	{
		if (TryGetContainerKind (type, out var kind) &&
				type.GenericArguments.All (IsClosedType) &&
				type.GenericArguments.Any (IsValueType)) {
			var root = new ValueTypeContainerRoot {
				Kind = kind,
				TypeArguments = type.GenericArguments,
			};
			roots [GetRootKey (root)] = root;
		}

		foreach (var argument in type.GenericArguments) {
			AddType (argument, roots);
		}
	}

	static bool TryGetContainerKind (TypeRefData type, out ValueTypeContainerKind kind)
	{
		var managedTypeName = type.ManagedTypeName;
		while (TryGetArraySuffix (managedTypeName, out var elementTypeName, out _)) {
			managedTypeName = elementTypeName;
		}
		switch (managedTypeName) {
		case "System.Collections.Generic.IList`1":
		case "Android.Runtime.JavaList`1":
			kind = ValueTypeContainerKind.List;
			return type.GenericArguments.Count == 1;
		case "System.Collections.Generic.ICollection`1":
		case "Android.Runtime.JavaCollection`1":
			kind = ValueTypeContainerKind.Collection;
			return type.GenericArguments.Count == 1;
		case "System.Collections.Generic.IDictionary`2":
		case "Android.Runtime.JavaDictionary`2":
			kind = ValueTypeContainerKind.Dictionary;
			return type.GenericArguments.Count == 2;
		default:
			kind = default;
			return false;
		}
	}

	static bool IsClosedType (TypeRefData type)
	{
		if (type.ManagedTypeName.StartsWith ("!", StringComparison.Ordinal)) {
			return false;
		}
		return type.GenericArguments.All (IsClosedType);
	}

	static bool IsValueType (TypeRefData type)
	{
		if (TryGetArraySuffix (type.ManagedTypeName, out _, out _)) {
			return false;
		}
		if (type.EncodeAsValueType) {
			return true;
		}
		return type.ManagedTypeName switch {
			"System.Boolean" or
			"System.Byte" or
			"System.Char" or
			"System.Decimal" or
			"System.Double" or
			"System.Half" or
			"System.Int16" or
			"System.Int32" or
			"System.Int64" or
			"System.Int128" or
			"System.IntPtr" or
			"System.SByte" or
			"System.Single" or
			"System.UInt16" or
			"System.UInt32" or
			"System.UInt64" or
			"System.UInt128" or
			"System.UIntPtr" => true,
			_ => false,
		};
	}

	static bool TryGetArraySuffix (string typeName, out string elementTypeName, out string suffix)
	{
		if (!typeName.EndsWith ("]", StringComparison.Ordinal)) {
			elementTypeName = "";
			suffix = "";
			return false;
		}
		int openBracket = typeName.LastIndexOf ('[');
		if (openBracket < 0) {
			elementTypeName = "";
			suffix = "";
			return false;
		}
		for (int i = openBracket + 1; i < typeName.Length - 1; i++) {
			if (typeName [i] != ',') {
				elementTypeName = "";
				suffix = "";
				return false;
			}
		}
		elementTypeName = typeName.Substring (0, openBracket);
		suffix = typeName.Substring (openBracket);
		return true;
	}

	static string GetRootKey (ValueTypeContainerRoot root)
		=> $"{root.Kind}:{string.Join ("|", root.TypeArguments.Select (GetTypeKey))}";

	static string GetTypeKey (TypeRefData type)
		=> $"{type.AssemblyName}:{type.ManagedTypeName}:{type.EncodeAsValueType}<{string.Join (",", type.GenericArguments.Select (GetTypeKey))}>";
}
