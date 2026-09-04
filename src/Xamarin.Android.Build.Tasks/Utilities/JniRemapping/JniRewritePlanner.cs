#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Build.Tasks;
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
		const string JniEnvironmentFullName = "Android.Runtime.JNIEnv";

		readonly PEReader peReader;
		readonly MetadataReader reader;
		readonly IJniNameMapping mapping;
		readonly R8Mapping? forwardMapping;
		readonly TaskLoggingHelper log;
		readonly Func<string, string?> renameClass;
		readonly Dictionary<TypeDefinitionHandle, string?> ownerJniNameCache = new ();
		readonly Dictionary<string, List<StaticJniClassAssignment>> staticJniClassAssignments = new (StringComparer.Ordinal);
		readonly HashSet<string> warnedUnsafeLookupSources = new (StringComparer.Ordinal);

		public JniRewritePlanner (PEReader peReader, MetadataReader reader, IJniNameMapping mapping, TaskLoggingHelper log)
		{
			this.peReader = peReader;
			this.reader = reader;
			this.mapping = mapping;
			forwardMapping = mapping as R8Mapping;
			this.log = log;
			renameClass = className => mapping.TryMapClass (className, out string renamed) ? renamed : null;
		}

		public JniRewritePlan CreatePlan ()
		{
			IndexStaticJniClassAssignments ();
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
			if (jniMemberName == ".ctor" || jniMemberName == ".cctor") {
				newName = null;
			}
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

			MethodBodyBlock body = peReader.GetMethodBody (method.RelativeVirtualAddress);
			byte [] il = body.GetILBytes () ?? [];
			var instructions = new List<IlInstruction> ();
			IlInstructionScanner.Walk (il, (code, instructionOffset, operandOffset, operandSize) =>
				instructions.Add (new IlInstruction (code, instructionOffset, operandOffset, operandSize)));
			HashSet<int> controlFlowEntries = GetControlFlowEntryOffsets (body, il, instructions);

			for (int i = 0; i < instructions.Count; i++) {
				IlInstruction instruction = instructions [i];
				if (instruction.Code == (ushort) ILOpCode.Ldstr) {
					string value = ReadUserString (il, instruction.OperandOffset);
					if (LdstrRewriter.TryRewrite (value, ownerJniName, mapping, out string rewritten) &&
							!String.Equals (value, rewritten, StringComparison.Ordinal)) {
						plan.AddUserString (methodHandle, instruction.OperandOffset, rewritten);
					}
				} else if (TryGetJniLookupKind (il, instruction, out bool isField)) {
					PlanLegacyJniLookup (plan, methodHandle, il, instructions, controlFlowEntries, i, isField);
				}
			}
		}

		void PlanLegacyJniLookup (JniRewritePlan plan, MethodDefinitionHandle methodHandle, byte [] il,
			List<IlInstruction> instructions, HashSet<int> controlFlowEntries, int callIndex, bool isField)
		{
			int descriptorIndex = PreviousNonNop (instructions, callIndex - 1);
			int memberNameIndex = PreviousNonNop (instructions, descriptorIndex - 1);
			int classIndex = PreviousNonNop (instructions, memberNameIndex - 1);
			if (classIndex < 0 ||
					instructions [descriptorIndex].Code != (ushort) ILOpCode.Ldstr ||
					instructions [memberNameIndex].Code != (ushort) ILOpCode.Ldstr) {
				return;
			}

			string memberName = ReadUserString (il, instructions [memberNameIndex].OperandOffset);
			if (!IsBareMemberName (memberName)) {
				return;
			}
			string descriptor = ReadUserString (il, instructions [descriptorIndex].OperandOffset);
			if (isField ? !JniDescriptorText.IsValidFieldDescriptor (descriptor) : !JniDescriptorText.IsValidMethodDescriptor (descriptor)) {
				return;
			}

			if (HasControlFlowEntry (instructions, controlFlowEntries, classIndex, callIndex) ||
					!TryResolveLegacyLookupClass (il, instructions, controlFlowEntries, classIndex, out string className)) {
				WarnForUnsafeRenamedLookup (methodHandle, il, instructions, classIndex, memberName, descriptor, isField);
				return;
			}

			if (isField) {
				if (mapping.TryMapField (className, memberName, out string renamedField) &&
						!String.Equals (memberName, renamedField, StringComparison.Ordinal)) {
					plan.AddUserString (methodHandle, instructions [memberNameIndex].OperandOffset, renamedField);
				}
			} else {
				JniDescriptorText.MethodDescriptorToJavaTypes (descriptor, out var javaParams, out string javaReturnType);
				string mappingName = R8Mapping.JniMemberNameToMappingName (memberName);
				if (mapping.TryMapMethod (className, mappingName, javaParams, javaReturnType, out string renamedMethod) &&
						!String.Equals (memberName, renamedMethod, StringComparison.Ordinal)) {
					plan.AddUserString (methodHandle, instructions [memberNameIndex].OperandOffset, renamedMethod);
				}
			}
		}

		bool TryResolveLegacyLookupClass (byte [] il, List<IlInstruction> instructions,
			HashSet<int> controlFlowEntries, int classIndex, out string className)
		{
			className = "";
			IlInstruction classInstruction = instructions [classIndex];
			if (IsJniEnvironmentMethod (il, classInstruction, "FindClass")) {
				return TryReadFindClassName (il, instructions, controlFlowEntries, classIndex, classIndex, out className);
			}

			if (TryGetStaticField (il, classInstruction, load: true, out string fieldKey)) {
				if (staticJniClassAssignments.TryGetValue (fieldKey, out var assignments) &&
						assignments.Count == 1) {
					string? assignedClassName = assignments [0].ClassName;
					if (assignedClassName != null) {
						className = assignedClassName;
						return true;
					}
				}
				return false;
			}

			if (!TryGetLocalIndex (il, classInstruction, load: true, out int localIndex)) {
				return false;
			}

			for (int storeIndex = classIndex - 1; storeIndex >= 0; storeIndex--) {
				IlInstruction candidate = instructions [storeIndex];
				if (controlFlowEntries.Contains (candidate.InstructionOffset) || IsControlFlowBarrier (candidate.Code)) {
					return false;
				}
				if (!TryGetLocalIndex (il, candidate, load: false, out int storedLocalIndex) || storedLocalIndex != localIndex) {
					continue;
				}

				int findClassIndex = PreviousNonNop (instructions, storeIndex - 1);
				return findClassIndex >= 0 &&
					IsJniEnvironmentMethod (il, instructions [findClassIndex], "FindClass") &&
					TryReadFindClassName (il, instructions, controlFlowEntries, findClassIndex, classIndex, out className);
			}
			return false;
		}

		void IndexStaticJniClassAssignments ()
		{
			foreach (MethodDefinitionHandle methodHandle in reader.MethodDefinitions) {
				MethodDefinition method = reader.GetMethodDefinition (methodHandle);
				if (method.RelativeVirtualAddress == 0) {
					continue;
				}

				MethodBodyBlock body = peReader.GetMethodBody (method.RelativeVirtualAddress);
				byte [] il = body.GetILBytes () ?? [];
				var instructions = new List<IlInstruction> ();
				IlInstructionScanner.Walk (il, (code, instructionOffset, operandOffset, operandSize) =>
					instructions.Add (new IlInstruction (code, instructionOffset, operandOffset, operandSize)));
				HashSet<int> controlFlowEntries = GetControlFlowEntryOffsets (body, il, instructions);

				for (int i = 0; i < instructions.Count; i++) {
					if (!TryGetStaticField (il, instructions [i], load: false, out string fieldKey)) {
						continue;
					}

					string? className = null;
					string? candidateClassName = null;
					int findClassIndex = PreviousNonNop (instructions, i - 1);
					if (findClassIndex >= 0 && IsJniEnvironmentMethod (il, instructions [findClassIndex], "FindClass")) {
						int classNameIndex = PreviousNonNop (instructions, findClassIndex - 1);
						if (classNameIndex >= 0 && instructions [classNameIndex].Code == (ushort) ILOpCode.Ldstr) {
							candidateClassName = ReadUserString (il, instructions [classNameIndex].OperandOffset);
							if (!HasControlFlowEntry (instructions, controlFlowEntries, classNameIndex, i)) {
								className = candidateClassName;
							}
						}
					}

					if (!staticJniClassAssignments.TryGetValue (fieldKey, out var assignments)) {
						staticJniClassAssignments [fieldKey] = assignments = new List<StaticJniClassAssignment> ();
					}
					assignments.Add (new StaticJniClassAssignment (className, candidateClassName));
				}
			}
		}

		void WarnForUnsafeRenamedLookup (MethodDefinitionHandle methodHandle, byte [] il,
			List<IlInstruction> instructions, int classIndex, string memberName, string descriptor, bool isField)
		{
			IlInstruction classInstruction = instructions [classIndex];
			if (IsJniEnvironmentMethod (il, classInstruction, "FindClass")) {
				int classNameIndex = PreviousNonNop (instructions, classIndex - 1);
				if (classNameIndex >= 0 &&
						instructions [classNameIndex].Code == (ushort) ILOpCode.Ldstr &&
						WouldRenameLookupMember (ReadUserString (il, instructions [classNameIndex].OperandOffset), memberName, descriptor, isField)) {
					LogUnsafeLookupWarning ("D:" + MetadataTokens.GetToken (methodHandle) + ":" + classInstruction.InstructionOffset);
				}
				return;
			}

			if (TryGetStaticField (il, classInstruction, load: true, out string fieldKey)) {
				if (!staticJniClassAssignments.TryGetValue (fieldKey, out var assignments)) {
					return;
				}
				foreach (StaticJniClassAssignment assignment in assignments) {
					if (assignment.CandidateClassName != null &&
							WouldRenameLookupMember (assignment.CandidateClassName, memberName, descriptor, isField)) {
						LogUnsafeLookupWarning ("F:" + fieldKey);
						return;
					}
				}
				return;
			}

			if (!TryGetLocalIndex (il, classInstruction, load: true, out int localIndex)) {
				return;
			}
			for (int storeIndex = 0; storeIndex < instructions.Count; storeIndex++) {
				IlInstruction instruction = instructions [storeIndex];
				if (!TryGetLocalIndex (il, instruction, load: false, out int storedLocalIndex) || storedLocalIndex != localIndex) {
					continue;
				}
				int findClassIndex = PreviousNonNop (instructions, storeIndex - 1);
				if (findClassIndex < 0 || !IsJniEnvironmentMethod (il, instructions [findClassIndex], "FindClass")) {
					continue;
				}
				int classNameIndex = PreviousNonNop (instructions, findClassIndex - 1);
				if (classNameIndex >= 0 &&
						instructions [classNameIndex].Code == (ushort) ILOpCode.Ldstr &&
						WouldRenameLookupMember (ReadUserString (il, instructions [classNameIndex].OperandOffset), memberName, descriptor, isField)) {
					LogUnsafeLookupWarning ("L:" + MetadataTokens.GetToken (methodHandle) + ":" + localIndex);
					return;
				}
			}
		}

		bool WouldRenameLookupMember (string className, string memberName, string descriptor, bool isField)
		{
			if (forwardMapping == null) {
				return false;
			}
			if (isField) {
				return forwardMapping.TryPeekRenamedField (className, memberName, out string renamedField) &&
					!String.Equals (memberName, renamedField, StringComparison.Ordinal);
			}

			JniDescriptorText.MethodDescriptorToJavaTypes (descriptor, out var javaParams, out string javaReturnType);
			string mappingName = R8Mapping.JniMemberNameToMappingName (memberName);
			return forwardMapping.TryPeekRenamedMethod (className, mappingName, javaParams, javaReturnType, out string renamedMethod) &&
				!String.Equals (memberName, renamedMethod, StringComparison.Ordinal);
		}

		void LogUnsafeLookupWarning (string sourceKey)
		{
			if (warnedUnsafeLookupSources.Add (sourceKey)) {
				log.LogCodedWarning ("XA4326", Properties.Resources.XA4326);
			}
		}

		bool TryGetStaticField (byte [] il, IlInstruction instruction, bool load, out string fieldKey)
		{
			fieldKey = "";
			ushort expectedCode = load ? (ushort) ILOpCode.Ldsfld : (ushort) ILOpCode.Stsfld;
			if (instruction.Code != expectedCode || instruction.OperandSize != sizeof (uint)) {
				return false;
			}

			EntityHandle fieldHandle = MetadataTokens.EntityHandle ((int) IlInstructionScanner.ReadUInt32 (il, instruction.OperandOffset));
			EntityHandle declaringTypeHandle;
			BlobHandle signature;
			switch (fieldHandle.Kind) {
			case HandleKind.FieldDefinition:
				FieldDefinition field = reader.GetFieldDefinition ((FieldDefinitionHandle) fieldHandle);
				string fieldName = reader.GetString (field.Name);
				signature = field.Signature;
				declaringTypeHandle = field.GetDeclaringType ();
				fieldKey = fieldName;
				break;
			case HandleKind.MemberReference:
				MemberReference member = reader.GetMemberReference ((MemberReferenceHandle) fieldHandle);
				string memberName = reader.GetString (member.Name);
				signature = member.Signature;
				declaringTypeHandle = member.Parent;
				fieldKey = memberName;
				break;
			default:
				return false;
			}

			if (!TryGetTypeIdentity (declaringTypeHandle, out string declaringType)) {
				fieldKey = "";
				return false;
			}
			fieldKey = declaringType + "\0" + fieldKey + "\0" + Convert.ToBase64String (reader.GetBlobBytes (signature));
			return true;
		}

		bool TryGetTypeIdentity (EntityHandle typeHandle, out string identity)
		{
			switch (typeHandle.Kind) {
			case HandleKind.TypeDefinition:
				TypeDefinition definition = reader.GetTypeDefinition ((TypeDefinitionHandle) typeHandle);
				string definitionName = reader.GetString (definition.Name);
				TypeDefinitionHandle declaringType = definition.GetDeclaringType ();
				if (!declaringType.IsNil) {
					if (!TryGetTypeIdentity (declaringType, out string declaringIdentity)) {
						identity = "";
						return false;
					}
					identity = declaringIdentity + "$" + definitionName;
					return true;
				}
				identity = reader.GetString (definition.Namespace) + "." + definitionName;
				return true;
			case HandleKind.TypeReference:
				TypeReference reference = reader.GetTypeReference ((TypeReferenceHandle) typeHandle);
				string referenceName = reader.GetString (reference.Name);
				if (reference.ResolutionScope.Kind == HandleKind.TypeReference) {
					if (!TryGetTypeIdentity (reference.ResolutionScope, out string declaringIdentity)) {
						identity = "";
						return false;
					}
					identity = declaringIdentity + "$" + referenceName;
					return true;
				}
				identity = reader.GetString (reference.Namespace) + "." + referenceName;
				return true;
			default:
				identity = "";
				return false;
			}
		}

		bool TryReadFindClassName (byte [] il, List<IlInstruction> instructions, HashSet<int> controlFlowEntries,
			int findClassIndex, int sequenceEndIndex, out string className)
		{
			className = "";
			int classNameIndex = PreviousNonNop (instructions, findClassIndex - 1);
			if (classNameIndex < 0 ||
					instructions [classNameIndex].Code != (ushort) ILOpCode.Ldstr ||
					HasControlFlowEntry (instructions, controlFlowEntries, classNameIndex, sequenceEndIndex)) {
				return false;
			}
			className = ReadUserString (il, instructions [classNameIndex].OperandOffset);
			return true;
		}

		static bool HasControlFlowEntry (List<IlInstruction> instructions, HashSet<int> controlFlowEntries,
			int startIndex, int endIndex)
		{
			for (int i = startIndex; i <= endIndex; i++) {
				if (controlFlowEntries.Contains (instructions [i].InstructionOffset)) {
					return true;
				}
			}
			return false;
		}

		static HashSet<int> GetControlFlowEntryOffsets (MethodBodyBlock body, byte [] il, List<IlInstruction> instructions)
		{
			var entries = new HashSet<int> ();
			foreach (ExceptionRegion region in body.ExceptionRegions) {
				entries.Add (region.HandlerOffset);
				if (region.Kind == ExceptionRegionKind.Filter) {
					entries.Add (region.FilterOffset);
				}
			}

			foreach (IlInstruction instruction in instructions) {
				int nextOffset = instruction.OperandOffset + instruction.OperandSize;
				if (IsShortBranch (instruction.Code)) {
					entries.Add (nextOffset + unchecked ((sbyte) il [instruction.OperandOffset]));
				} else if (IsLongBranch (instruction.Code)) {
					entries.Add (nextOffset + unchecked ((int) IlInstructionScanner.ReadUInt32 (il, instruction.OperandOffset)));
				} else if (instruction.Code == (ushort) ILOpCode.Switch) {
					int branchCount = unchecked ((int) IlInstructionScanner.ReadUInt32 (il, instruction.OperandOffset));
					for (int i = 0; i < branchCount; i++) {
						int deltaOffset = instruction.OperandOffset + sizeof (uint) + i * sizeof (uint);
						entries.Add (nextOffset + unchecked ((int) IlInstructionScanner.ReadUInt32 (il, deltaOffset)));
					}
				}
			}
			return entries;
		}

		static bool IsShortBranch (ushort code)
			=> code >= (ushort) ILOpCode.Br_s && code <= (ushort) ILOpCode.Blt_un_s ||
				code == (ushort) ILOpCode.Leave_s;

		static bool IsLongBranch (ushort code)
			=> code >= (ushort) ILOpCode.Br && code <= (ushort) ILOpCode.Blt_un ||
				code == (ushort) ILOpCode.Leave;

		bool TryGetJniLookupKind (byte [] il, IlInstruction instruction, out bool isField)
		{
			isField = false;
			if (!TryGetMethodIdentity (il, instruction, out string declaringType, out string methodName) ||
					declaringType != JniEnvironmentFullName) {
				return false;
			}

			switch (methodName) {
			case "GetFieldID":
			case "GetStaticFieldID":
				isField = true;
				return true;
			case "GetMethodID":
			case "GetStaticMethodID":
				return true;
			default:
				return false;
			}
		}

		bool IsJniEnvironmentMethod (byte [] il, IlInstruction instruction, string methodName)
			=> TryGetMethodIdentity (il, instruction, out string declaringType, out string actualMethodName) &&
				declaringType == JniEnvironmentFullName &&
				actualMethodName == methodName;

		bool TryGetMethodIdentity (byte [] il, IlInstruction instruction, out string declaringType, out string methodName)
		{
			declaringType = "";
			methodName = "";
			if (instruction.Code != (ushort) ILOpCode.Call || instruction.OperandSize != sizeof (uint)) {
				return false;
			}

			EntityHandle methodHandle = MetadataTokens.EntityHandle ((int) IlInstructionScanner.ReadUInt32 (il, instruction.OperandOffset));
			if (methodHandle.Kind == HandleKind.MethodSpecification) {
				methodHandle = reader.GetMethodSpecification ((MethodSpecificationHandle) methodHandle).Method;
			}

			EntityHandle declaringTypeHandle;
			if (methodHandle.Kind == HandleKind.MethodDefinition) {
				MethodDefinition method = reader.GetMethodDefinition ((MethodDefinitionHandle) methodHandle);
				methodName = reader.GetString (method.Name);
				declaringTypeHandle = method.GetDeclaringType ();
			} else if (methodHandle.Kind == HandleKind.MemberReference) {
				MemberReference method = reader.GetMemberReference ((MemberReferenceHandle) methodHandle);
				methodName = reader.GetString (method.Name);
				declaringTypeHandle = method.Parent;
			} else {
				return false;
			}

			switch (declaringTypeHandle.Kind) {
			case HandleKind.TypeDefinition:
				TypeDefinition typeDefinition = reader.GetTypeDefinition ((TypeDefinitionHandle) declaringTypeHandle);
				declaringType = reader.GetString (typeDefinition.Namespace) + "." + reader.GetString (typeDefinition.Name);
				return true;
			case HandleKind.TypeReference:
				TypeReference typeReference = reader.GetTypeReference ((TypeReferenceHandle) declaringTypeHandle);
				declaringType = reader.GetString (typeReference.Namespace) + "." + reader.GetString (typeReference.Name);
				return true;
			default:
				return false;
			}
		}

		static int PreviousNonNop (List<IlInstruction> instructions, int index)
		{
			while (index >= 0 && instructions [index].Code == (ushort) ILOpCode.Nop) {
				index--;
			}
			return index;
		}

		static bool IsControlFlowBarrier (ushort code)
		{
			switch ((ILOpCode) code) {
			case ILOpCode.Jmp:
			case ILOpCode.Br_s:
			case ILOpCode.Brfalse_s:
			case ILOpCode.Brtrue_s:
			case ILOpCode.Beq_s:
			case ILOpCode.Bge_s:
			case ILOpCode.Bgt_s:
			case ILOpCode.Ble_s:
			case ILOpCode.Blt_s:
			case ILOpCode.Bne_un_s:
			case ILOpCode.Bge_un_s:
			case ILOpCode.Bgt_un_s:
			case ILOpCode.Ble_un_s:
			case ILOpCode.Blt_un_s:
			case ILOpCode.Br:
			case ILOpCode.Brfalse:
			case ILOpCode.Brtrue:
			case ILOpCode.Beq:
			case ILOpCode.Bge:
			case ILOpCode.Bgt:
			case ILOpCode.Ble:
			case ILOpCode.Blt:
			case ILOpCode.Bne_un:
			case ILOpCode.Bge_un:
			case ILOpCode.Bgt_un:
			case ILOpCode.Ble_un:
			case ILOpCode.Blt_un:
			case ILOpCode.Switch:
			case ILOpCode.Ret:
			case ILOpCode.Throw:
			case ILOpCode.Endfinally:
			case ILOpCode.Leave:
			case ILOpCode.Leave_s:
			case ILOpCode.Endfilter:
			case ILOpCode.Rethrow:
				return true;
			default:
				return false;
			}
		}

		static bool TryGetLocalIndex (byte [] il, IlInstruction instruction, bool load, out int index)
		{
			index = 0;
			ushort code = instruction.Code;
			ushort first = load ? (ushort) ILOpCode.Ldloc_0 : (ushort) ILOpCode.Stloc_0;
			ushort last = load ? (ushort) ILOpCode.Ldloc_3 : (ushort) ILOpCode.Stloc_3;
			if (code >= first && code <= last) {
				index = code - first;
				return true;
			}

			ushort shortForm = load ? (ushort) ILOpCode.Ldloc_s : (ushort) ILOpCode.Stloc_s;
			if (code == shortForm) {
				index = il [instruction.OperandOffset];
				return true;
			}

			ushort longForm = load ? (ushort) ILOpCode.Ldloc : (ushort) ILOpCode.Stloc;
			if (code == longForm) {
				index = il [instruction.OperandOffset] | (il [instruction.OperandOffset + 1] << 8);
				return true;
			}
			return false;
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

		string ReadUserString (byte [] il, int operandOffset)
		{
			uint token = IlInstructionScanner.ReadUInt32 (il, operandOffset);
			if ((token & 0xFF000000) != 0x70000000) {
				throw new JniRewriteException ($"Malformed IL: ldstr operand 0x{token:X8} is not a #US token.");
			}
			return reader.GetUserString (MetadataTokens.UserStringHandle ((int) (token & 0x00FFFFFF)));
		}

		readonly struct IlInstruction
		{
			public ushort Code { get; }
			public int InstructionOffset { get; }
			public int OperandOffset { get; }
			public int OperandSize { get; }

			public IlInstruction (ushort code, int instructionOffset, int operandOffset, int operandSize)
			{
				Code = code;
				InstructionOffset = instructionOffset;
				OperandOffset = operandOffset;
				OperandSize = operandSize;
			}
		}

		readonly struct StaticJniClassAssignment
		{
			public string? ClassName { get; }
			public string? CandidateClassName { get; }

			public StaticJniClassAssignment (string? className, string? candidateClassName)
			{
				ClassName = className;
				CandidateClassName = candidateClassName;
			}
		}
	}
}
