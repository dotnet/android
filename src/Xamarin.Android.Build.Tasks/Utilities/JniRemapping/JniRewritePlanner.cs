#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Pass one of the rewrite: scans an assembly and produces the exact set of JNI-bearing
	/// values that must change, without mutating anything. The rebuilder then reproduces the
	/// assembly and applies the plan.
	/// </summary>
	sealed class JniRewritePlanner
	{
		const string RegisterAttributeFullName = "Android.Runtime.RegisterAttribute";
		const string JniTypeSignatureAttributeFullName = "Java.Interop.JniTypeSignatureAttribute";
		const string JniMethodSignatureAttributeFullName = "Java.Interop.JniMethodSignatureAttribute";
		const string JniConstructorSignatureAttributeFullName = "Java.Interop.JniConstructorSignatureAttribute";
		const string JavaPeerAliasesAttributeFullName = "Java.Interop.JavaPeerAliasesAttribute";
		const string TypeMapAttributeFullName = "System.Runtime.InteropServices.TypeMapAttribute`1";

		const string JavaPeerProxyNamespace = "Java.Interop";
		const string JavaPeerProxyName = "JavaPeerProxy";

		enum Utf8Role
		{
			Unknown,
			MethodName,
			MethodSignature,
		}

		readonly struct Utf8Use
		{
			public Utf8Role Role { get; }
			public string? OwnerJniName { get; }
			public string? PairedSignature { get; }

			public Utf8Use (Utf8Role role, string? ownerJniName, string? pairedSignature)
			{
				Role = role;
				OwnerJniName = ownerJniName;
				PairedSignature = pairedSignature;
			}
		}

		readonly PEReader peReader;
		readonly MetadataReader reader;
		readonly IJniNameMapping mapping;
		readonly FieldRvaTable fieldRvaTable;
		readonly TaskLoggingHelper log;
		readonly Func<string, string?> renameClass;
		readonly Dictionary<TypeDefinitionHandle, string?> ownerJniNameCache = new ();
		readonly Dictionary<FieldDefinitionHandle, List<Utf8Use>> utf8Uses = new ();

		public JniRewritePlanner (PEReader peReader, MetadataReader reader, IJniNameMapping mapping, FieldRvaTable fieldRvaTable, TaskLoggingHelper log)
		{
			this.peReader = peReader;
			this.reader = reader;
			this.mapping = mapping;
			this.fieldRvaTable = fieldRvaTable;
			this.log = log;
			renameClass = className => mapping.TryMapClass (className, out string renamed) ? renamed : null;
		}

		public JniRewritePlan CreatePlan ()
		{
			var plan = new JniRewritePlan ();

			PlanAssemblyAttributes (plan);
			foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
				PlanType (plan, typeHandle);
			}

			PlanUtf8FieldData (plan);
			return plan;
		}

		void PlanType (JniRewritePlan plan, TypeDefinitionHandle typeHandle)
		{
			TypeDefinition typeDef = reader.GetTypeDefinition (typeHandle);
			string? ownerJniName = ResolveOwnerJniName (typeHandle);

			PlanJavaPeerAliasesAttributes (plan, typeDef.GetCustomAttributes ());
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

		void PlanAssemblyAttributes (JniRewritePlan plan)
		{
			foreach (CustomAttributeHandle caHandle in reader.GetAssemblyDefinition ().GetCustomAttributes ()) {
				CustomAttribute ca = reader.GetCustomAttribute (caHandle);
				if (reader.GetCustomAttributeFullName (ca, log) != TypeMapAttributeFullName) {
					continue;
				}

				// TypeMapAttribute<T>'s first argument is the JNI map key. Its following
				// System.Type arguments are also SerStrings, but must remain unchanged.
				PlanCustomAttributeRewrite (plan, caHandle, ca, fixedArgCount: 1,
					(i, value) => value != null ? TryRewriteTypeMapKey (value) : null);
			}
		}

		void PlanJavaPeerAliasesAttributes (JniRewritePlan plan, CustomAttributeHandleCollection attributes)
		{
			foreach (CustomAttributeHandle caHandle in attributes) {
				CustomAttribute ca = reader.GetCustomAttribute (caHandle);
				if (reader.GetCustomAttributeFullName (ca, log) != JavaPeerAliasesAttributeFullName) {
					continue;
				}

				BlobReader blobReader = reader.GetBlobReader (ca.Value);
				byte [] originalContent = blobReader.ReadBytes (blobReader.Length);
				byte []? newContent = CustomAttributeStringRewriter.TryRewriteStringArray (originalContent, TryRewriteTypeMapKey);
				if (newContent != null) {
					plan.AddCustomAttributeBlob (caHandle, newContent);
				}
			}
		}

		string? TryRewriteTypeMapKey (string value)
		{
			int suffixStart = value.LastIndexOf ('[');
			string suffix = "";
			string jniName = value;
			if (suffixStart > 0 && value [value.Length - 1] == ']' && IsDecimalIndex (value, suffixStart + 1, value.Length - 1)) {
				suffix = value.Substring (suffixStart);
				jniName = value.Substring (0, suffixStart);
			}

			return mapping.TryMapClass (jniName, out string renamed) ? renamed + suffix : null;
		}

		static bool IsDecimalIndex (string value, int start, int end)
		{
			if (start == end) {
				return false;
			}
			for (int i = start; i < end; i++) {
				if (value [i] < '0' || value [i] > '9') {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Resolves the JNI class name that "owns" a type: its own Register/JniTypeSignature
		/// argument, the JNI name a generated <c>JavaPeerProxy</c> passes to its base constructor,
		/// or (recursively) its enclosing type's.
		/// </summary>
		string? ResolveOwnerJniName (TypeDefinitionHandle typeHandle)
		{
			if (ownerJniNameCache.TryGetValue (typeHandle, out string? cached)) {
				return cached;
			}

			// Guard against a pathological/cyclical nesting chain while resolving.
			ownerJniNameCache [typeHandle] = null;

			TypeDefinition typeDef = reader.GetTypeDefinition (typeHandle);
			string? result = TryGetTypeLevelJniName (typeDef) ?? TryGetJavaPeerProxyJniName (typeDef);
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

		/// <summary>
		/// The trimmable typemap generator emits one <c>JavaPeerProxy</c> subclass per Java peer
		/// whose parameterless constructor passes the peer's JNI name to the base constructor as
		/// its only <c>ldstr</c>. That is the type's JNI identity.
		/// </summary>
		string? TryGetJavaPeerProxyJniName (TypeDefinition typeDef)
		{
			if (!IsJavaPeerProxy (typeDef.BaseType)) {
				return null;
			}

			foreach (MethodDefinitionHandle methodHandle in typeDef.GetMethods ()) {
				MethodDefinition method = reader.GetMethodDefinition (methodHandle);
				if ((method.Attributes & MethodAttributes.RTSpecialName) == 0 || reader.GetString (method.Name) != ".ctor") {
					continue;
				}
				if (method.RelativeVirtualAddress == 0) {
					continue;
				}

				string? found = null;
				bool ambiguous = false;
				byte [] il = GetILBytes (method);
				IlInstructionScanner.Walk (il, (code, _, operandOffset, _) => {
					if (code != (ushort) ILOpCode.Ldstr) {
						return;
					}
					string value = ReadUserString (il, operandOffset);
					if (found != null && found != value) {
						ambiguous = true;
					}
					found ??= value;
				});

				if (!ambiguous && found != null && found.Length > 0) {
					return found;
				}
			}

			return null;
		}

		bool IsJavaPeerProxy (EntityHandle baseType)
		{
			if (baseType.IsNil) {
				return false;
			}

			if (baseType.Kind == HandleKind.TypeReference) {
				TypeReference typeRef = reader.GetTypeReference ((TypeReferenceHandle) baseType);
				return reader.GetString (typeRef.Name) == JavaPeerProxyName && reader.GetString (typeRef.Namespace) == JavaPeerProxyNamespace;
			}

			if (baseType.Kind == HandleKind.TypeDefinition) {
				TypeDefinition typeDef = reader.GetTypeDefinition ((TypeDefinitionHandle) baseType);
				return reader.GetString (typeDef.Name) == JavaPeerProxyName && reader.GetString (typeDef.Namespace) == JavaPeerProxyNamespace;
			}

			return false;
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
					// (string memberSignature): the member name is implicitly ".ctor".
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
				return; // No IL body (abstract, extern/P-Invoke, etc).
			}

			byte [] il = GetILBytes (method);
			FieldDefinitionHandle pendingUtf8Name = default;
			string? referencedOwnerJniName = FindSingleReferencedJniClass (il);
			string? pendingMemberName = null;
			int pendingMemberNameOffset = 0;

			IlInstructionScanner.Walk (il, (code, _, operandOffset, _) => {
				if (code == (ushort) ILOpCode.Ldstr) {
					pendingUtf8Name = default;
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
				if (code != (ushort) ILOpCode.Ldsflda && code != (ushort) ILOpCode.Ldsfld) {
					pendingUtf8Name = default;
					return;
				}

				FieldDefinitionHandle field = TryGetUtf8Field (il, operandOffset);
				if (field.IsNil) {
					pendingUtf8Name = default;
					return;
				}

				// The typemap generator emits `ldsflda <name>; ldsflda <signature>` pairs when
				// filling in a JniNativeMethod for RegisterNatives; that adjacency is what makes
				// an otherwise ambiguous bare method name resolvable against the owning class.
				if (pendingUtf8Name.IsNil) {
					pendingUtf8Name = field;
					return;
				}

				string? signature = GetUtf8Value (field);
				if (signature != null && JniDescriptorText.IsValidMethodDescriptor (signature)) {
					RecordUtf8Use (pendingUtf8Name, new Utf8Use (Utf8Role.MethodName, ownerJniName, signature));
					RecordUtf8Use (field, new Utf8Use (Utf8Role.MethodSignature, ownerJniName, null));
				} else {
					RecordUtf8Use (pendingUtf8Name, new Utf8Use (Utf8Role.Unknown, null, null));
					RecordUtf8Use (field, new Utf8Use (Utf8Role.Unknown, null, null));
				}

				pendingUtf8Name = default;
			});

			if (!pendingUtf8Name.IsNil) {
				RecordUtf8Use (pendingUtf8Name, new Utf8Use (Utf8Role.Unknown, null, null));
			}
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

		void RecordUtf8Use (FieldDefinitionHandle field, Utf8Use use)
		{
			if (!utf8Uses.TryGetValue (field, out var uses)) {
				utf8Uses [field] = uses = new List<Utf8Use> ();
			}
			uses.Add (use);
		}

		FieldDefinitionHandle TryGetUtf8Field (byte [] il, int operandOffset)
		{
			uint token = IlInstructionScanner.ReadUInt32 (il, operandOffset);
			if ((token & 0xFF000000) != 0x04000000) {
				return default; // Not a FieldDefinition token.
			}

			var handle = MetadataTokens.FieldDefinitionHandle ((int) (token & 0x00FFFFFF));
			FieldRvaEntry? entry = fieldRvaTable.Get (handle);
			return entry != null && entry.IsUtf8Datum ? handle : default;
		}

		string? GetUtf8Value (FieldDefinitionHandle field) => fieldRvaTable.Get (field)?.Utf8Value;

		void PlanUtf8FieldData (JniRewritePlan plan)
		{
			foreach (FieldRvaEntry entry in fieldRvaTable.Entries) {
				string? value = entry.Utf8Value;
				if (value == null) {
					continue;
				}

				string? resolved = null;
				foreach (Utf8Use use in GetUses (entry.Field)) {
					string? candidate = ComputeNewUtf8Value (value, use);
					if (candidate == null) {
						continue;
					}
					if (resolved != null && resolved != candidate) {
						throw new JniRewriteException (
							$"The mapped UTF-8 JNI datum '{value}' is shared by more than one Java class, but the mapping renames it to both " +
							$"'{resolved}' and '{candidate}'. Splitting a shared '{FieldRvaTable.Utf8FieldNamePrefix}' field would move metadata tokens, which this rewriter does not do.");
					}
					resolved ??= candidate;
				}

				if (resolved != null && resolved != value) {
					plan.AddUtf8FieldValue (entry.Field, resolved);
				}
			}
		}

		IEnumerable<Utf8Use> GetUses (FieldDefinitionHandle field)
		{
			if (utf8Uses.TryGetValue (field, out var uses)) {
				return uses;
			}
			return new [] { new Utf8Use (Utf8Role.Unknown, null, null) };
		}

		string? ComputeNewUtf8Value (string value, Utf8Use use)
		{
			if (use.Role == Utf8Role.MethodName && use.OwnerJniName != null && use.PairedSignature != null) {
				JniDescriptorText.MethodDescriptorToJavaTypes (use.PairedSignature, out var javaParams, out string javaReturnType);
				string mappingName = R8Mapping.JniMemberNameToMappingName (value);
				return mapping.TryMapMethod (use.OwnerJniName, mappingName, javaParams, javaReturnType, out string renamed) ? renamed : null;
			}

			if (JniDescriptorText.IsValidMethodDescriptor (value) || JniDescriptorText.IsValidFieldDescriptor (value)) {
				return JniDescriptorText.TryRewriteDescriptor (value, renameClass, out string rewritten) ? rewritten : null;
			}

			if (use.Role == Utf8Role.Unknown && mapping.TryMapClass (value, out string renamedClass)) {
				return renamedClass;
			}

			return null;
		}

		byte [] GetILBytes (MethodDefinition method)
		{
			MethodBodyBlock body = peReader.GetMethodBody (method.RelativeVirtualAddress);
			return body.GetILBytes () ?? Array.Empty<byte> ();
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
