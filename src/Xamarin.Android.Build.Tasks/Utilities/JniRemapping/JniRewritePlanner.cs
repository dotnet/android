#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Pass one of the rewrite: scans an assembly and produces the exact set of JNI-bearing
	/// managed metadata values that must change, without mutating anything. The rebuilder then
	/// reproduces the assembly and applies the plan.
	/// </summary>
	sealed class JniRewritePlanner
	{
		const string RegisterAttributeFullName = "Android.Runtime.RegisterAttribute";
		const string JniTypeSignatureAttributeFullName = "Java.Interop.JniTypeSignatureAttribute";
		const string JniMethodSignatureAttributeFullName = "Java.Interop.JniMethodSignatureAttribute";
		const string JniConstructorSignatureAttributeFullName = "Java.Interop.JniConstructorSignatureAttribute";

		readonly PEReader peReader;
		readonly MetadataReader reader;
		readonly IJniNameMapping mapping;
		readonly TaskLoggingHelper log;
		readonly Func<string, string?> renameClass;
		readonly Dictionary<TypeDefinitionHandle, string?> ownerJniNameCache = new ();

		public JniRewritePlanner (PEReader peReader, MetadataReader reader, IJniNameMapping mapping, TaskLoggingHelper log)
		{
			this.peReader = peReader;
			this.reader = reader;
			this.mapping = mapping;
			this.log = log;
			renameClass = className => mapping.TryMapClass (className, out string renamed) ? renamed : null;
		}

		public JniRewritePlan CreatePlan ()
		{
			var plan = new JniRewritePlan ();
			foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
				PlanType (plan, typeHandle);
			}
			return plan;
		}

		void PlanType (JniRewritePlan plan, TypeDefinitionHandle typeHandle)
		{
			TypeDefinition typeDef = reader.GetTypeDefinition (typeHandle);
			string? ownerJniName = ResolveOwnerJniName (typeHandle);

			PlanTypeLevelAttributes (plan, typeDef, ownerJniName);

			foreach (MethodDefinitionHandle methodHandle in typeDef.GetMethods ()) {
				PlanMethodAttributes (plan, methodHandle, ownerJniName);
				PlanMethodBody (plan, methodHandle, ownerJniName);
			}

			foreach (FieldDefinitionHandle fieldHandle in typeDef.GetFields ()) {
				PlanMemberNameAttributes (plan, reader.GetFieldDefinition (fieldHandle).GetCustomAttributes (), ownerJniName);
			}

			foreach (PropertyDefinitionHandle propertyHandle in typeDef.GetProperties ()) {
				PlanMemberNameAttributes (plan, reader.GetPropertyDefinition (propertyHandle).GetCustomAttributes (), ownerJniName);
			}

			foreach (EventDefinitionHandle eventHandle in typeDef.GetEvents ()) {
				PlanMemberNameAttributes (plan, reader.GetEventDefinition (eventHandle).GetCustomAttributes (), ownerJniName);
			}
		}

		/// <summary>
		/// Resolves the JNI class name that owns a type from its own Register/JniTypeSignature
		/// argument or, recursively, its enclosing type.
		/// </summary>
		string? ResolveOwnerJniName (TypeDefinitionHandle typeHandle)
		{
			if (ownerJniNameCache.TryGetValue (typeHandle, out string? cached)) {
				return cached;
			}

			ownerJniNameCache [typeHandle] = null;

			TypeDefinition typeDef = reader.GetTypeDefinition (typeHandle);
			string? result = TryGetTypeLevelJniName (typeDef);
			if (result == null) {
				TypeDefinitionHandle declaring = typeDef.GetDeclaringType ();
				if (!declaring.IsNil) {
					result = ResolveOwnerJniName (declaring);
				}
			}

			ownerJniNameCache [typeHandle] = result;
			return result;
		}

		string? TryGetTypeLevelJniName (TypeDefinition typeDef)
		{
			foreach (CustomAttributeHandle caHandle in typeDef.GetCustomAttributes ()) {
				CustomAttribute ca = reader.GetCustomAttribute (caHandle);
				string? fullName = reader.GetCustomAttributeFullName (ca, log);
				if (fullName != RegisterAttributeFullName && fullName != JniTypeSignatureAttributeFullName) {
					continue;
				}

				var args = ca.GetCustomAttributeArguments ().FixedArguments;
				if (args.Length >= 1 && args [0].Value is string s && s.Length > 0) {
					return s;
				}
			}
			return null;
		}

		void PlanTypeLevelAttributes (JniRewritePlan plan, TypeDefinition typeDef, string? ownerJniName)
		{
			if (ownerJniName == null || !mapping.TryMapClass (ownerJniName, out string renamedClass)) {
				return;
			}

			foreach (CustomAttributeHandle caHandle in typeDef.GetCustomAttributes ()) {
				CustomAttribute ca = reader.GetCustomAttribute (caHandle);
				string? fullName = reader.GetCustomAttributeFullName (ca, log);
				if (fullName != RegisterAttributeFullName && fullName != JniTypeSignatureAttributeFullName) {
					continue;
				}

				var args = ca.GetCustomAttributeArguments ().FixedArguments;
				if (args.Length == 0 || args [0].Value is not string current || current != ownerJniName) {
					continue;
				}

				PlanCustomAttributeRewrite (plan, caHandle, ca, args.Length, (i, _) => i == 0 ? renamedClass : null);
			}
		}

		void PlanMethodAttributes (JniRewritePlan plan, MethodDefinitionHandle methodHandle, string? ownerJniName)
		{
			MethodDefinition method = reader.GetMethodDefinition (methodHandle);

			foreach (CustomAttributeHandle caHandle in method.GetCustomAttributes ()) {
				CustomAttribute ca = reader.GetCustomAttribute (caHandle);
				string? fullName = reader.GetCustomAttributeFullName (ca, log);

				switch (fullName) {
				case RegisterAttributeFullName:
				case JniMethodSignatureAttributeFullName:
					PlanNameAndDescriptorAttribute (plan, caHandle, ca, ownerJniName, nameIndex: 0, descriptorIndex: 1);
					break;
				case JniConstructorSignatureAttributeFullName:
					PlanNameAndDescriptorAttribute (plan, caHandle, ca, ownerJniName, nameIndex: -1, descriptorIndex: 0);
					break;
				}
			}
		}

		void PlanNameAndDescriptorAttribute (JniRewritePlan plan, CustomAttributeHandle caHandle, CustomAttribute ca, string? ownerJniName, int nameIndex, int descriptorIndex)
		{
			var args = ca.GetCustomAttributeArguments ().FixedArguments;
			if (args.Length <= descriptorIndex) {
				return;
			}

			string jniMemberName = nameIndex < 0
				? ".ctor"
				: args [nameIndex].Value as string ?? "";
			if (jniMemberName.Length == 0) {
				return;
			}

			string? jniDescriptor = args [descriptorIndex].Value as string;
			string? newName = TryFindRenamedMethodName (ownerJniName, jniMemberName, jniDescriptor);
			string? newDescriptor = jniDescriptor != null && JniDescriptorText.TryRewriteDescriptor (jniDescriptor, renameClass, out string rewrittenDescriptor)
				? rewrittenDescriptor
				: null;

			if (newName == null && newDescriptor == null) {
				return;
			}

			PlanCustomAttributeRewrite (plan, caHandle, ca, args.Length, (i, _) => {
				if (i == nameIndex) {
					return newName;
				}
				if (i == descriptorIndex) {
					return newDescriptor;
				}
				return null;
			});
		}

		void PlanMemberNameAttributes (JniRewritePlan plan, CustomAttributeHandleCollection attributes, string? ownerJniName)
		{
			if (ownerJniName == null) {
				return;
			}

			foreach (CustomAttributeHandle caHandle in attributes) {
				CustomAttribute ca = reader.GetCustomAttribute (caHandle);
				if (reader.GetCustomAttributeFullName (ca, log) != RegisterAttributeFullName) {
					continue;
				}

				var args = ca.GetCustomAttributeArguments ().FixedArguments;
				if (args.Length == 0 || args [0].Value is not string jniFieldName) {
					continue;
				}

				if (!mapping.TryMapField (ownerJniName, jniFieldName, out string renamedField)) {
					continue;
				}

				PlanCustomAttributeRewrite (plan, caHandle, ca, args.Length, (i, _) => i == 0 ? renamedField : null);
			}
		}

		string? TryFindRenamedMethodName (string? ownerJniName, string jniMemberName, string? jniDescriptor)
		{
			if (ownerJniName == null) {
				return null;
			}

			string mappingName = R8Mapping.JniMemberNameToMappingName (jniMemberName);
			if (jniDescriptor != null && JniDescriptorText.IsValidMethodDescriptor (jniDescriptor)) {
				JniDescriptorText.MethodDescriptorToJavaTypes (jniDescriptor, out var javaParams, out string javaReturnType);
				return mapping.TryMapMethod (ownerJniName, mappingName, javaParams, javaReturnType, out string renamed) ? renamed : null;
			}

			return mapping.TryMapMethodByNameOnly (ownerJniName, mappingName, out string renamedByNameOnly)
				? renamedByNameOnly
				: null;
		}

		void PlanCustomAttributeRewrite (JniRewritePlan plan, CustomAttributeHandle caHandle, CustomAttribute ca, int fixedArgCount, Func<int, string?, string?> rewriteArg)
		{
			BlobReader blobReader = reader.GetBlobReader (ca.Value);
			byte [] originalContent = blobReader.ReadBytes (blobReader.Length);
			byte []? newContent = CustomAttributeStringRewriter.TryRewrite (originalContent, fixedArgCount, rewriteArg);
			if (newContent != null) {
				plan.AddCustomAttributeBlob (caHandle, newContent);
			}
		}

		void PlanMethodBody (JniRewritePlan plan, MethodDefinitionHandle methodHandle, string? ownerJniName)
		{
			MethodDefinition method = reader.GetMethodDefinition (methodHandle);
			if (method.RelativeVirtualAddress == 0) {
				return;
			}

			byte [] il = GetILBytes (method);
			string? referencedOwnerJniName = FindSingleReferencedJniClass (il);
			string? pendingMemberName = null;
			int pendingMemberNameOffset = 0;

			IlInstructionScanner.Walk (il, (code, _, operandOffset, _) => {
				if (code == (ushort) ILOpCode.Ldstr) {
					string value = ReadUserString (il, operandOffset);
					if (referencedOwnerJniName != null && pendingMemberName != null) {
						if (JniDescriptorText.IsValidFieldDescriptor (value) &&
								mapping.TryMapField (referencedOwnerJniName, pendingMemberName, out string renamedField)) {
							plan.AddUserString (methodHandle, pendingMemberNameOffset, renamedField);
						} else if (JniDescriptorText.IsValidMethodDescriptor (value)) {
							JniDescriptorText.MethodDescriptorToJavaTypes (value, out var javaParams, out string javaReturnType);
							string mappingName = R8Mapping.JniMemberNameToMappingName (pendingMemberName);
							if (mapping.TryMapMethod (referencedOwnerJniName, mappingName, javaParams, javaReturnType, out string renamedMethod)) {
								plan.AddUserString (methodHandle, pendingMemberNameOffset, renamedMethod);
							}
						}
					}
					if (LdstrRewriter.TryRewrite (value, ownerJniName, mapping, out string rewritten)) {
						plan.AddUserString (methodHandle, operandOffset, rewritten);
					}
					pendingMemberName = IsBareMemberName (value) ? value : null;
					pendingMemberNameOffset = operandOffset;
					return;
				}

				if (code != (ushort) ILOpCode.Pop) {
					pendingMemberName = null;
				}
			});
		}

		string? FindSingleReferencedJniClass (byte [] il)
		{
			string? referencedClass = null;
			bool ambiguous = false;
			IlInstructionScanner.Walk (il, (code, _, operandOffset, _) => {
				if (ambiguous || code != (ushort) ILOpCode.Ldstr) {
					return;
				}

				string value = ReadUserString (il, operandOffset);
				if (!mapping.TryMapClass (value, out _)) {
					return;
				}
				if (referencedClass != null && referencedClass != value) {
					ambiguous = true;
					return;
				}
				referencedClass = value;
			});
			return ambiguous ? null : referencedClass;
		}

		static bool IsBareMemberName (string value)
		{
			if (value.Length == 0) {
				return false;
			}
			foreach (char c in value) {
				if (!(char.IsLetterOrDigit (c) || c == '_' || c == '$' || c == '<' || c == '>')) {
					return false;
				}
			}
			return true;
		}

		byte [] GetILBytes (MethodDefinition method)
		{
			MethodBodyBlock body = peReader.GetMethodBody (method.RelativeVirtualAddress);
			return body.GetILBytes () ?? [];
		}

		string ReadUserString (byte [] il, int operandOffset)
		{
			uint token = IlInstructionScanner.ReadUInt32 (il, operandOffset);
			if ((token & 0xFF000000) != 0x70000000) {
				throw new JniRewriteException ($"Malformed IL: ldstr operand 0x{token:X8} is not a #US token.");
			}
			return reader.GetUserString (MetadataTokens.UserStringHandle ((int) (token & 0x00FFFFFF)));
		}
	}
}
