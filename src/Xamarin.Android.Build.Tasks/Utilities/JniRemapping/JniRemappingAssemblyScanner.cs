#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Reads linked managed metadata to identify JNI mappings that still have managed consumers.
	/// This scanner never modifies or reconstructs the input assembly.
	/// </summary>
	static class JniRemappingAssemblyScanner
	{
		const string RegisterAttributeFullName = "Android.Runtime.RegisterAttribute";
		const string JniTypeSignatureAttributeFullName = "Java.Interop.JniTypeSignatureAttribute";
		const string JniMethodSignatureAttributeFullName = "Java.Interop.JniMethodSignatureAttribute";
		const string JniConstructorSignatureAttributeFullName = "Java.Interop.JniConstructorSignatureAttribute";

		public static void Scan (MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log)
		{
			ScanTypeMapAttributes (reader, mapping, log);
			var ownerJniNames = new Dictionary<TypeDefinitionHandle, string?> ();
			foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
				ScanType (reader, mapping, log, ownerJniNames, typeHandle);
			}
		}

		static void ScanType (MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log,
			Dictionary<TypeDefinitionHandle, string?> ownerJniNames, TypeDefinitionHandle typeHandle)
		{
			TypeDefinition type = reader.GetTypeDefinition (typeHandle);
			string? ownerJniName = ResolveOwnerJniName (reader, log, ownerJniNames, typeHandle);
			if (ownerJniName != null) {
				mapping.TryGetRenamedClass (ownerJniName, out _);
			}

			foreach (MethodDefinitionHandle methodHandle in type.GetMethods ()) {
				ScanMethod (reader, mapping, log, ownerJniName, methodHandle);
			}

			foreach (FieldDefinitionHandle fieldHandle in type.GetFields ()) {
				ScanFieldLikeMember (reader, mapping, log, ownerJniName, reader.GetFieldDefinition (fieldHandle).GetCustomAttributes ());
			}
			foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties ()) {
				ScanFieldLikeMember (reader, mapping, log, ownerJniName, reader.GetPropertyDefinition (propertyHandle).GetCustomAttributes ());
			}
			foreach (EventDefinitionHandle eventHandle in type.GetEvents ()) {
				ScanFieldLikeMember (reader, mapping, log, ownerJniName, reader.GetEventDefinition (eventHandle).GetCustomAttributes ());
			}
		}

		static string? ResolveOwnerJniName (MetadataReader reader, TaskLoggingHelper log,
			Dictionary<TypeDefinitionHandle, string?> ownerJniNames, TypeDefinitionHandle typeHandle)
		{
			if (ownerJniNames.TryGetValue (typeHandle, out string? cached)) {
				return cached;
			}

			ownerJniNames [typeHandle] = null;
			TypeDefinition type = reader.GetTypeDefinition (typeHandle);
			string? result = GetTypeJniName (reader, log, type.GetCustomAttributes ());
			if (result == null) {
				TypeDefinitionHandle declaringType = type.GetDeclaringType ();
				if (!declaringType.IsNil) {
					result = ResolveOwnerJniName (reader, log, ownerJniNames, declaringType);
				}
			}
			ownerJniNames [typeHandle] = result;
			return result;
		}

		static string? GetTypeJniName (MetadataReader reader, TaskLoggingHelper log, CustomAttributeHandleCollection attributes)
		{
			foreach (CustomAttributeHandle attributeHandle in attributes) {
				CustomAttribute attribute = reader.GetCustomAttribute (attributeHandle);
				string? name = reader.GetCustomAttributeFullName (attribute, log);
				if (name != RegisterAttributeFullName && name != JniTypeSignatureAttributeFullName) {
					continue;
				}
				var arguments = attribute.GetCustomAttributeArguments ().FixedArguments;
				if (arguments.Length > 0 && arguments [0].Value is string jniName && jniName.Length > 0) {
					return jniName;
				}
			}
			return null;
		}

		static void ScanTypeMapAttributes (MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log)
		{
			if (!reader.IsAssembly) {
				return;
			}

			foreach (CustomAttributeHandle attributeHandle in reader.GetAssemblyDefinition ().GetCustomAttributes ()) {
				CustomAttribute attribute = reader.GetCustomAttribute (attributeHandle);
				if (!IsTypeMapAttribute (reader, attribute)) {
					continue;
				}

				int argumentCount = GetArgumentCount (reader, attribute);
				var blob = reader.GetBlobReader (attribute.Value);
				if (blob.ReadUInt16 () != 1) {
					throw new BadImageFormatException ("Invalid TypeMap attribute prolog.");
				}
				string? key = blob.ReadSerializedString ();
				if (key.IsNullOrEmpty ()) {
					throw new BadImageFormatException ("Invalid TypeMap key.");
				}
				for (int i = 1; i < argumentCount; i++) {
					if (blob.ReadSerializedString ().IsNullOrEmpty ()) {
						throw new BadImageFormatException ("Invalid TypeMap type argument.");
					}
				}
				if (blob.ReadUInt16 () != 0 || blob.RemainingBytes != 0) {
					throw new BadImageFormatException ("Invalid TypeMap attribute arguments.");
				}

				key = NormalizeAliasKey (key);
				if (!TryGetClassName (key, out string? className)) {
					throw new BadImageFormatException ($"Invalid TypeMap class name '{key}'.");
				}
				if (className != null) {
					RecordAllMappings (mapping, className);
				}
			}
		}

		static bool IsTypeMapAttribute (MetadataReader reader, CustomAttribute attribute)
		{
			if (attribute.Constructor.Kind != HandleKind.MemberReference) {
				return false;
			}
			var constructor = reader.GetMemberReference ((MemberReferenceHandle) attribute.Constructor);
			if (constructor.Parent.Kind != HandleKind.TypeSpecification) {
				return false;
			}
			var type = reader.GetTypeSpecification ((TypeSpecificationHandle) constructor.Parent);
			var blob = reader.GetBlobReader (type.Signature);
			if (blob.ReadSignatureTypeCode () != SignatureTypeCode.GenericTypeInstance ||
					(SignatureTypeKind) blob.ReadByte () != SignatureTypeKind.Class) {
				return false;
			}
			EntityHandle openType = blob.ReadTypeHandle ();
			if (!IsTypeReferenceFromAssembly (
					reader,
					openType,
					"System.Runtime.InteropServices",
					"TypeMapAttribute`1",
					"System.Runtime.InteropServices") ||
					blob.ReadCompressedInteger () != 1) {
				return false;
			}
			if (blob.ReadSignatureTypeCode () != SignatureTypeCode.TypeHandle) {
				return false;
			}
			EntityHandle group = blob.ReadTypeHandle ();
			return blob.RemainingBytes == 0 && IsJavaTypeMapGroup (reader, group);
		}

		static bool IsJavaTypeMapGroup (MetadataReader reader, EntityHandle group)
		{
			string ns;
			string name;
			string assemblyName;
			if (group.Kind == HandleKind.TypeReference) {
				var type = reader.GetTypeReference ((TypeReferenceHandle) group);
				if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) {
					return false;
				}
				ns = reader.GetString (type.Namespace);
				name = reader.GetString (type.Name);
				var assembly = reader.GetAssemblyReference ((AssemblyReferenceHandle) type.ResolutionScope);
				assemblyName = reader.GetString (assembly.Name);
			} else if (group.Kind == HandleKind.TypeDefinition) {
				var type = reader.GetTypeDefinition ((TypeDefinitionHandle) group);
				if (!type.GetDeclaringType ().IsNil) {
					return false;
				}
				ns = reader.GetString (type.Namespace);
				name = reader.GetString (type.Name);
				assemblyName = reader.GetString (reader.GetAssemblyDefinition ().Name);
			} else {
				return false;
			}

			return (ns == "Java.Lang" && name == "Object" && assemblyName == "Mono.Android") ||
				(ns == "" && name == "__TypeMapAnchor" &&
					assemblyName.StartsWith ("_", StringComparison.Ordinal) &&
					assemblyName.EndsWith (".TypeMap", StringComparison.Ordinal));
		}

		static int GetArgumentCount (MetadataReader reader, CustomAttribute attribute)
		{
			var constructor = reader.GetMemberReference ((MemberReferenceHandle) attribute.Constructor);
			BlobHandle signature = constructor.Signature;
			StringHandle constructorName = constructor.Name;
			var blob = reader.GetBlobReader (signature);
			var header = blob.ReadSignatureHeader ();
			int count = blob.ReadCompressedInteger ();
			if (reader.GetString (constructorName) != ".ctor" || header.Kind != SignatureKind.Method || !header.IsInstance || header.IsGeneric ||
					(count != 2 && count != 3) || blob.ReadSignatureTypeCode () != SignatureTypeCode.Void ||
					blob.ReadSignatureTypeCode () != SignatureTypeCode.String) {
				throw new BadImageFormatException ("Invalid TypeMap constructor signature.");
			}
			for (int i = 1; i < count; i++) {
				if (blob.ReadSignatureTypeCode () != SignatureTypeCode.TypeHandle) {
					throw new BadImageFormatException ("Invalid TypeMap constructor type parameter.");
				}
				var typeHandle = blob.ReadTypeHandle ();
				if (!IsTypeReferenceFromAssembly (reader, typeHandle, "System", "Type", "System.Runtime")) {
					throw new BadImageFormatException ("Invalid TypeMap constructor type parameter.");
				}
			}
			if (blob.RemainingBytes != 0) {
				throw new BadImageFormatException ("Invalid TypeMap constructor signature.");
			}
			return count;
		}

		static bool IsTypeReferenceFromAssembly (
			MetadataReader reader,
			EntityHandle handle,
			string expectedNamespace,
			string expectedName,
			string expectedAssembly)
		{
			if (handle.Kind != HandleKind.TypeReference) {
				return false;
			}
			var type = reader.GetTypeReference ((TypeReferenceHandle) handle);
			if (reader.GetString (type.Namespace) != expectedNamespace ||
					reader.GetString (type.Name) != expectedName ||
					type.ResolutionScope.Kind != HandleKind.AssemblyReference) {
				return false;
			}
			var assembly = reader.GetAssemblyReference ((AssemblyReferenceHandle) type.ResolutionScope);
			string assemblyName = reader.GetString (assembly.Name);
			return assemblyName == expectedAssembly || assemblyName == "System.Private.CoreLib";
		}

		static string NormalizeAliasKey (string key)
		{
			int start = key.LastIndexOf ('[');
			if (start <= 0 || start == key.Length - 2 || key [key.Length - 1] != ']') {
				return key;
			}
			for (int i = start + 1; i < key.Length - 1; i++) {
				if (key [i] < '0' || key [i] > '9') {
					return key;
				}
			}
			return key.Substring (0, start);
		}

		static bool TryGetClassName (string name, out string? className)
		{
			className = null;
			int dimensions = 0;
			while (dimensions < name.Length && name [dimensions] == '[') {
				dimensions++;
			}
			if (dimensions > 0) {
				if (dimensions > 255 || dimensions == name.Length) {
					return false;
				}
				if (dimensions == name.Length - 1 && "BCDFIJSZ".IndexOf (name [dimensions]) >= 0) {
					return true;
				}
				if (name [dimensions] != 'L' || name [name.Length - 1] != ';') {
					return false;
				}
				name = name.Substring (dimensions + 1, name.Length - dimensions - 2);
			}
			if (!IsClassName (name)) {
				return false;
			}
			className = name;
			return true;
		}

		static bool IsClassName (string name)
		{
			bool first = true;
			for (int i = 0; i < name.Length; i++) {
				if (name [i] == '/') {
					if (first) {
						return false;
					}
					first = true;
					continue;
				}

				var category = CharUnicodeInfo.GetUnicodeCategory (name, i);
				bool start = category == UnicodeCategory.UppercaseLetter ||
					category == UnicodeCategory.LowercaseLetter ||
					category == UnicodeCategory.TitlecaseLetter ||
					category == UnicodeCategory.ModifierLetter ||
					category == UnicodeCategory.OtherLetter ||
					category == UnicodeCategory.LetterNumber ||
					category == UnicodeCategory.CurrencySymbol ||
					category == UnicodeCategory.ConnectorPunctuation;
				if (!start && (first || (category != UnicodeCategory.DecimalDigitNumber &&
						category != UnicodeCategory.NonSpacingMark && category != UnicodeCategory.SpacingCombiningMark))) {
					return false;
				}
				if (char.IsHighSurrogate (name [i])) {
					i++;
				}
				first = false;
			}
			return !first;
		}

		static void RecordAllMappings (R8Mapping mapping, string ownerJniName)
		{
			mapping.TryGetRenamedClass (ownerJniName, out _);
			foreach (R8ClassMapping type in mapping.EnumerateClassMappings ()) {
				if (type.OriginalJniName != ownerJniName) {
					continue;
				}
				foreach (R8FieldMapping field in type.Fields) {
					mapping.RecordFieldAccess (ownerJniName, field.OriginalName);
				}
				foreach (R8MethodMapping method in type.Methods) {
					mapping.TryGetRenamedMethod (
						ownerJniName,
						method.OriginalName,
						method.JavaParameterTypes,
						method.JavaReturnType,
						out _);
				}
				return;
			}
		}

		static void ScanMethod (MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log,
			string? ownerJniName, MethodDefinitionHandle methodHandle)
		{
			if (ownerJniName == null) {
				return;
			}

			MethodDefinition method = reader.GetMethodDefinition (methodHandle);
			foreach (CustomAttributeHandle attributeHandle in method.GetCustomAttributes ()) {
				CustomAttribute attribute = reader.GetCustomAttribute (attributeHandle);
				string? name = reader.GetCustomAttributeFullName (attribute, log);
				if (name != RegisterAttributeFullName && name != JniMethodSignatureAttributeFullName &&
						name != JniConstructorSignatureAttributeFullName) {
					continue;
				}
				var arguments = attribute.GetCustomAttributeArguments ().FixedArguments;
				string? methodName;
				string? descriptor;
				switch (name) {
				case RegisterAttributeFullName:
				case JniMethodSignatureAttributeFullName:
					methodName = arguments.Length > 0 ? arguments [0].Value as string : null;
					descriptor = arguments.Length > 1 ? arguments [1].Value as string : null;
					break;
				case JniConstructorSignatureAttributeFullName:
					methodName = "<init>";
					descriptor = arguments.Length > 0 ? arguments [0].Value as string : null;
					break;
				default:
					continue;
				}

				if (methodName.IsNullOrEmpty ()) {
					continue;
				}
				if (descriptor != null && JniDescriptorText.IsValidMethodDescriptor (descriptor)) {
					JniDescriptorText.MethodDescriptorToJavaTypes (descriptor, out var parameterTypes, out string returnType);
					mapping.TryGetRenamedMethod (ownerJniName, R8Mapping.JniMemberNameToMappingName (methodName), parameterTypes, returnType, out _);
				} else {
					mapping.TryGetRenamedMethodByNameOnly (ownerJniName, R8Mapping.JniMemberNameToMappingName (methodName), out _);
				}
			}
		}

		static void ScanFieldLikeMember (MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log,
			string? ownerJniName, CustomAttributeHandleCollection attributes)
		{
			if (ownerJniName == null) {
				return;
			}

			foreach (CustomAttributeHandle attributeHandle in attributes) {
				CustomAttribute attribute = reader.GetCustomAttribute (attributeHandle);
				if (reader.GetCustomAttributeFullName (attribute, log) != RegisterAttributeFullName) {
					continue;
				}
				var arguments = attribute.GetCustomAttributeArguments ().FixedArguments;
				if (arguments.Length > 0 && arguments [0].Value is string fieldName && fieldName.Length > 0) {
					mapping.RecordFieldAccess (ownerJniName, fieldName);
				}
			}
		}
	}
}
