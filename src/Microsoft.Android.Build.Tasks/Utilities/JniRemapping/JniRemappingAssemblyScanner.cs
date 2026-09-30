#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

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
		const string JavaPeerProxyNamespace = "Java.Interop";
		const string JavaPeerProxyName = "JavaPeerProxy";

		public static void Scan (PEReader peReader, MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log)
		{
			var ownerJniNames = new Dictionary<TypeDefinitionHandle, string?> ();
			foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
				ScanType (peReader, reader, mapping, log, ownerJniNames, typeHandle);
			}
		}

		static void ScanType (PEReader peReader, MetadataReader reader, R8Mapping mapping, TaskLoggingHelper log,
			Dictionary<TypeDefinitionHandle, string?> ownerJniNames, TypeDefinitionHandle typeHandle)
		{
			TypeDefinition type = reader.GetTypeDefinition (typeHandle);
			string? ownerJniName = ResolveOwnerJniName (peReader, reader, log, ownerJniNames, typeHandle);
			if (ownerJniName != null) {
				mapping.TryGetRenamedClass (ownerJniName, out _);
				if (IsJavaPeerProxy (reader, type.BaseType)) {
					RecordAllMappings (mapping, ownerJniName);
				}
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

		static string? ResolveOwnerJniName (PEReader peReader, MetadataReader reader, TaskLoggingHelper log,
			Dictionary<TypeDefinitionHandle, string?> ownerJniNames, TypeDefinitionHandle typeHandle)
		{
			if (ownerJniNames.TryGetValue (typeHandle, out string? cached)) {
				return cached;
			}

			ownerJniNames [typeHandle] = null;
			TypeDefinition type = reader.GetTypeDefinition (typeHandle);
			string? result = GetTypeJniName (reader, log, type.GetCustomAttributes ()) ?? GetJavaPeerProxyJniName (peReader, reader, type);
			if (result == null) {
				TypeDefinitionHandle declaringType = type.GetDeclaringType ();
				if (!declaringType.IsNil) {
					result = ResolveOwnerJniName (peReader, reader, log, ownerJniNames, declaringType);
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

		static string? GetJavaPeerProxyJniName (PEReader peReader, MetadataReader reader, TypeDefinition type)
		{
			if (!IsJavaPeerProxy (reader, type.BaseType)) {
				return null;
			}

			foreach (MethodDefinitionHandle methodHandle in type.GetMethods ()) {
				MethodDefinition method = reader.GetMethodDefinition (methodHandle);
				if ((method.Attributes & System.Reflection.MethodAttributes.RTSpecialName) == 0 ||
						reader.GetString (method.Name) != ".ctor" ||
						method.RelativeVirtualAddress == 0) {
					continue;
				}

				byte [] il = peReader.GetMethodBody (method.RelativeVirtualAddress).GetILBytes () ?? [];
				for (int i = 0; i + 4 < il.Length; i++) {
					if (il [i] != (byte) ILOpCode.Ldstr) {
						continue;
					}
					int token = il [i + 1] | il [i + 2] << 8 | il [i + 3] << 16 | il [i + 4] << 24;
					if ((token & unchecked ((int) 0xFF000000)) != 0x70000000) {
						continue;
					}
					string value = reader.GetUserString (MetadataTokens.UserStringHandle (token & 0x00FFFFFF));
					if (value.Length > 0) {
						return value;
					}
				}
			}
			return null;
		}

		static bool IsJavaPeerProxy (MetadataReader reader, EntityHandle baseType)
		{
			if (baseType.IsNil) {
				return false;
			}
			if (baseType.Kind == HandleKind.TypeReference) {
				TypeReference type = reader.GetTypeReference ((TypeReferenceHandle) baseType);
				return reader.GetString (type.Namespace) == JavaPeerProxyNamespace &&
					reader.GetString (type.Name) == JavaPeerProxyName;
			}
			if (baseType.Kind == HandleKind.TypeDefinition) {
				TypeDefinition type = reader.GetTypeDefinition ((TypeDefinitionHandle) baseType);
				return reader.GetString (type.Namespace) == JavaPeerProxyNamespace &&
					reader.GetString (type.Name) == JavaPeerProxyName;
			}
			return false;
		}

		static void RecordAllMappings (R8Mapping mapping, string ownerJniName)
		{
			foreach (R8ClassMapping type in mapping.EnumerateClassMappings ()) {
				if (type.OriginalJniName != ownerJniName) {
					continue;
				}
				foreach (R8FieldMapping field in type.Fields) {
					mapping.TryGetRenamedField (ownerJniName, field.OriginalName, out _);
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
					mapping.TryGetRenamedField (ownerJniName, fieldName, out _);
				}
			}
		}
	}
}
