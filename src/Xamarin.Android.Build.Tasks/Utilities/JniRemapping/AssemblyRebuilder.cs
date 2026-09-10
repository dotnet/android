#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Xamarin.Android.Tasks.JniRemapping
{
	sealed class AssemblyRebuildResult
	{
		public byte [] Image { get; }

		/// <summary>
		/// True when the source assembly was marked <c>StrongNameSigned</c>. The rebuilt image
		/// cannot carry a valid signature (no key is available here), so only the
		/// <c>StrongNameSigned</c> flag is cleared - the signature directory keeps its original
		/// size so the image is genuinely delay-signed (space is reserved) and can be re-signed
		/// without another rewrite, rather than left with a stale flag or a truncated directory.
		/// </summary>
		public bool StrongNameSignatureCleared { get; }

		public AssemblyRebuildResult (byte [] image, bool strongNameSignatureCleared)
		{
			Image = image;
			StrongNameSignatureCleared = strongNameSignatureCleared;
		}
	}

	/// <summary>
	/// Pass two of the rewrite: reconstructs a complete managed PE from a source assembly,
	/// cloning every metadata table row in its original order - so every entity token keeps its
	/// value - while re-emitting the heaps, the method bodies, the managed resources, and the
	/// mapped field data. The JNI edits collected by <see cref="JniRewritePlanner"/> are applied
	/// as the corresponding data is re-emitted, which is what lets two use sites that shared one
	/// deduplicated <c>#US</c>/<c>#Blob</c> entry receive different values.
	/// </summary>
	sealed class AssemblyRebuilder
	{
		static readonly TableIndex [] UnsupportedTables = {
			TableIndex.FieldPtr, TableIndex.MethodPtr, TableIndex.ParamPtr, TableIndex.EventPtr, TableIndex.PropertyPtr,
			TableIndex.EncLog, TableIndex.EncMap,
			TableIndex.AssemblyProcessor, TableIndex.AssemblyOS, TableIndex.AssemblyRefProcessor, TableIndex.AssemblyRefOS,
			TableIndex.Document, TableIndex.MethodDebugInformation, TableIndex.LocalScope, TableIndex.LocalVariable,
			TableIndex.LocalConstant, TableIndex.ImportScope, TableIndex.StateMachineMethod, TableIndex.CustomDebugInformation,
		};

		// Tables whose rows carry tokens that IL, coded indices, or the plan itself can reference;
		// their row counts must survive the round trip exactly.
		static readonly TableIndex [] TokenCriticalTables = {
			TableIndex.TypeRef, TableIndex.TypeDef, TableIndex.Field, TableIndex.MethodDef, TableIndex.Param,
			TableIndex.InterfaceImpl, TableIndex.MemberRef, TableIndex.CustomAttribute, TableIndex.DeclSecurity,
			TableIndex.StandAloneSig, TableIndex.Event, TableIndex.Property, TableIndex.ModuleRef, TableIndex.TypeSpec,
			TableIndex.AssemblyRef, TableIndex.File, TableIndex.ExportedType, TableIndex.ManifestResource,
			TableIndex.GenericParam, TableIndex.MethodSpec, TableIndex.GenericParamConstraint,
		};

		readonly PEReader peReader;
		readonly MetadataReader reader;
		readonly JniRewritePlan plan;
		readonly FieldRvaTable fieldRvaTable;
		readonly MetadataBuilder metadata = new ();
		readonly BlobBuilder ilStream = new ();
		readonly BlobBuilder mappedFieldData = new ();
		readonly BlobBuilder managedResources = new ();

		readonly Dictionary<MethodDefinitionHandle, int> bodyOffsets = new ();
		readonly Dictionary<FieldDefinitionHandle, int> newFieldRvaOffsets = new ();
		readonly Dictionary<FieldDefinitionHandle, TypeDefinitionHandle> resizedFieldTypes = new ();
		readonly Dictionary<ManifestResourceHandle, int> resourceOffsets = new ();
		readonly List<SyntheticSizedType> syntheticSizedTypes = new ();

		int sourceTypeDefCount;

		sealed class SyntheticSizedType
		{
			public TypeDefinitionHandle Handle { get; }
			public TypeDefinitionHandle Template { get; }
			public TypeDefinitionHandle Enclosing { get; }
			public int Size { get; }

			public SyntheticSizedType (TypeDefinitionHandle handle, TypeDefinitionHandle template, TypeDefinitionHandle enclosing, int size)
			{
				Handle = handle;
				Template = template;
				Enclosing = enclosing;
				Size = size;
			}
		}

		public AssemblyRebuilder (PEReader peReader, MetadataReader reader, JniRewritePlan plan, FieldRvaTable fieldRvaTable)
		{
			this.peReader = peReader;
			this.reader = reader;
			this.plan = plan;
			this.fieldRvaTable = fieldRvaTable;
		}

		public AssemblyRebuildResult Build ()
		{
			ValidateSupported ();
			sourceTypeDefCount = reader.GetTableRowCount (TableIndex.TypeDef);

			PlanMappedFieldData ();
			EmitMethodBodies ();
			EmitManagedResources ();
			CloneTables ();
			EmitSyntheticSizedTypes ();
			ValidateRowCounts ();

			return Serialize ();
		}

		void ValidateSupported ()
		{
			if (reader.MetadataKind != MetadataKind.Ecma335) {
				throw new JniRewriteException ($"Only ECMA-335 metadata can be rewritten; this assembly uses '{reader.MetadataKind}'.");
			}

			foreach (TableIndex table in UnsupportedTables) {
				int rows = reader.GetTableRowCount (table);
				if (rows > 0) {
					throw new JniRewriteException ($"The assembly uses the '{table}' metadata table ({rows} row(s)), which this rewriter cannot reproduce.");
				}
			}

			int implMapCount = reader.GetTableRowCount (TableIndex.ImplMap);
			for (int rid = 1; rid <= implMapCount; rid++) {
				if (MetadataRawColumns.GetImplMapMemberForwarded (reader, rid).Kind == HandleKind.FieldDefinition) {
					throw new JniRewriteException ("The assembly has a field-backed ImplMap row, which this rewriter cannot reproduce.");
				}
			}

			CorHeader? corHeader = peReader.PEHeaders.CorHeader;
			if (corHeader == null) {
				throw new JniRewriteException ("The file has no CLI header and is not a managed assembly.");
			}
			if ((corHeader.Flags & CorFlags.NativeEntryPoint) != 0) {
				throw new JniRewriteException ("The assembly declares a native entry point, which this rewriter cannot reproduce.");
			}

			RequireEmptyDirectory (corHeader.ManagedNativeHeaderDirectory, "ReadyToRun/NGen native header");
			RequireEmptyDirectory (corHeader.VtableFixupsDirectory, "CLI vtable fixups (C++/CLI)");
			RequireEmptyDirectory (corHeader.ExportAddressTableJumpsDirectory, "CLI export address table jumps");
			ValidateStrongNameSignatureDirectory (corHeader.StrongNameSignatureDirectory);

			PEHeader? peHeader = peReader.PEHeaders.PEHeader;
			if (peHeader == null) {
				throw new JniRewriteException ("The file has no PE optional header and cannot be rebuilt.");
			}

			// Authenticode covers the complete PE image and is invalidated by any metadata rewrite.
			// ManagedPEBuilder intentionally omits the source certificate table; APK signing protects
			// the final packaged image instead.
			RequireEmptyDirectory (peHeader.ExportTableDirectory, "native export table");
			RequireEmptyDirectory (peHeader.DelayImportTableDirectory, "delay import table");
			RequireEmptyDirectory (peHeader.LoadConfigTableDirectory, "load configuration table");
			RequireEmptyDirectory (peHeader.ThreadLocalStorageTableDirectory, "thread local storage table");
			RequireEmptyDirectory (peHeader.ExceptionTableDirectory, "native exception (unwind) table");

			int entryPointToken = corHeader.EntryPointTokenOrRelativeVirtualAddress;
			if (entryPointToken != 0 && (entryPointToken & 0xFF000000) != 0x06000000) {
				throw new JniRewriteException ($"The assembly's entry point token 0x{entryPointToken:X8} is not a MethodDef token; multi-module entry points are not supported.");
			}
		}

		void ValidateStrongNameSignatureDirectory (DirectoryEntry directory)
		{
			if (directory.RelativeVirtualAddress == 0 && directory.Size == 0) {
				return;
			}
			if (directory.RelativeVirtualAddress <= 0 || directory.Size <= 0) {
				throw new JniRewriteException ("The assembly has an invalid strong-name signature directory.");
			}

			PEMemoryBlock block = peReader.GetSectionData (directory.RelativeVirtualAddress);
			if (block.Length < directory.Size) {
				throw new JniRewriteException ("The strong-name signature directory extends past the end of its PE section.");
			}
		}

		static void RequireEmptyDirectory (DirectoryEntry directory, string description)
		{
			if (directory.Size != 0 || directory.RelativeVirtualAddress != 0) {
				throw new JniRewriteException ($"The assembly has a {description}, which this rewriter cannot reproduce.");
			}
		}

		void ValidateRowCounts ()
		{
			foreach (TableIndex table in TokenCriticalTables) {
				int expected = reader.GetTableRowCount (table);
				if (table == TableIndex.TypeDef) {
					expected += syntheticSizedTypes.Count;
				}

				int actual = metadata.GetRowCount (table);
				if (actual != expected) {
					throw new JniRewriteException ($"Internal error: rebuilt '{table}' table has {actual} row(s) but {expected} were expected; metadata tokens would move.");
				}
			}
		}

		/// <summary>
		/// Re-lays out the <c>FieldRVA</c> data. Overlapping and adjacent source ranges are copied
		/// together so aliases and their relative offsets survive, while ranges from separate PE
		/// sections are emitted as independently aligned blocks. Rewritten UTF-8 JNI data is
		/// written back over its own slot when it still fits. Longer replacements are appended
		/// and the field is re-typed to a wider <c>__utf8_N</c> value type.
		/// </summary>
		void PlanMappedFieldData ()
		{
			IReadOnlyList<FieldRvaEntry> entries = fieldRvaTable.Entries;
			if (entries.Count == 0) {
				return;
			}

			var sortedEntries = new List<FieldRvaEntry> (entries.Count);
			foreach (FieldRvaEntry entry in entries) {
				sortedEntries.Add (entry);
			}
			sortedEntries.Sort (static (left, right) => {
				int result = left.RelativeVirtualAddress.CompareTo (right.RelativeVirtualAddress);
				return result != 0 ? result : MetadataTokens.GetRowNumber (left.Field).CompareTo (MetadataTokens.GetRowNumber (right.Field));
			});

			HashSet<FieldDefinitionHandle> overlappingFields = FindOverlappingFields (sortedEntries);
			var appended = new List<KeyValuePair<FieldDefinitionHandle, byte []>> ();
			int first = 0;
			while (first < sortedEntries.Count) {
				int groupStartRva = sortedEntries [first].RelativeVirtualAddress;
				int groupEndRva = GetFieldDataEnd (sortedEntries [first]);
				int end = first + 1;
				while (end < sortedEntries.Count && sortedEntries [end].RelativeVirtualAddress <= groupEndRva) {
					groupEndRva = Math.Max (groupEndRva, GetFieldDataEnd (sortedEntries [end]));
					end++;
				}

				var data = new byte [groupEndRva - groupStartRva];
				for (int i = first; i < end; i++) {
					FieldRvaEntry entry = sortedEntries [i];
					Array.Copy (entry.Data, 0, data, entry.RelativeVirtualAddress - groupStartRva, entry.Data.Length);
				}

				mappedFieldData.Align (ManagedPEBuilder.MappedFieldDataAlignment);
				int outputGroupOffset = mappedFieldData.Count;

				for (int i = first; i < end; i++) {
					FieldRvaEntry entry = sortedEntries [i];
					int offset = entry.RelativeVirtualAddress - groupStartRva;
					string? newValue = plan.GetUtf8FieldValue (entry.Field);
					if (newValue == null) {
						newFieldRvaOffsets [entry.Field] = outputGroupOffset + offset;
						continue;
					}

					byte [] replacement = EncodeNullTerminatedUtf8 (newValue);
					if (replacement.Length <= entry.Data.Length && !overlappingFields.Contains (entry.Field)) {
						// Shorter or equal: write the NUL-terminated bytes over the original slot
						// and zero the tail. The field keeps its declared size.
						Array.Clear (data, offset, entry.Data.Length);
						Array.Copy (replacement, 0, data, offset, replacement.Length);
						newFieldRvaOffsets [entry.Field] = outputGroupOffset + offset;
						continue;
					}

					if (replacement.Length <= entry.Data.Length) {
						// The source slot overlaps another field, so changing it in place would
						// silently change that alias as well. Preserve this field's declared size
						// while moving only its replacement to independent storage.
						var detached = new byte [entry.Data.Length];
						Array.Copy (replacement, detached, replacement.Length);
						replacement = detached;
					} else {
						resizedFieldTypes [entry.Field] = GetOrCreateSizedType (entry, replacement.Length);
					}
					appended.Add (new KeyValuePair<FieldDefinitionHandle, byte []> (entry.Field, replacement));
				}

				mappedFieldData.WriteBytes (data);
				first = end;
			}

			foreach (KeyValuePair<FieldDefinitionHandle, byte []> extra in appended) {
				mappedFieldData.Align (ManagedPEBuilder.MappedFieldDataAlignment);
				newFieldRvaOffsets [extra.Key] = mappedFieldData.Count;
				mappedFieldData.WriteBytes (extra.Value);
			}
		}

		static HashSet<FieldDefinitionHandle> FindOverlappingFields (IReadOnlyList<FieldRvaEntry> sortedEntries)
		{
			var overlapping = new HashSet<FieldDefinitionHandle> ();
			FieldDefinitionHandle furthestField = default;
			int furthestEnd = 0;

			foreach (FieldRvaEntry entry in sortedEntries) {
				int end = GetFieldDataEnd (entry);
				if (!furthestField.IsNil && entry.RelativeVirtualAddress < furthestEnd) {
					overlapping.Add (furthestField);
					overlapping.Add (entry.Field);
				}
				if (furthestField.IsNil || end > furthestEnd) {
					furthestField = entry.Field;
					furthestEnd = end;
				}
			}

			return overlapping;
		}

		static int GetFieldDataEnd (FieldRvaEntry entry)
		{
			if (entry.RelativeVirtualAddress < 0 || entry.Data.Length > int.MaxValue - entry.RelativeVirtualAddress) {
				throw new JniRewriteException ("A FieldRVA data range cannot be represented safely.");
			}
			return entry.RelativeVirtualAddress + entry.Data.Length;
		}

		static byte [] EncodeNullTerminatedUtf8 (string value)
		{
			byte [] utf8 = System.Text.Encoding.UTF8.GetBytes (value);
			var result = new byte [utf8.Length + 1];
			Array.Copy (utf8, result, utf8.Length);
			return result;
		}

		/// <summary>
		/// Finds - or schedules the creation of - the <c>&lt;PrivateImplementationDetails&gt;/__utf8_N</c>
		/// explicit-layout value type of the requested size. New types are appended after every
		/// cloned <c>TypeDef</c> row, so no existing token moves.
		/// </summary>
		TypeDefinitionHandle GetOrCreateSizedType (FieldRvaEntry entry, int size)
		{
			if (entry.Utf8SizedType.IsNil) {
				throw new JniRewriteException ("Internal error: a non-UTF-8 mapped field was scheduled for replacement.");
			}

			TypeDefinition template = reader.GetTypeDefinition (entry.Utf8SizedType);
			string wantedName = FieldRvaTable.Utf8FieldNamePrefix + size.ToString (System.Globalization.CultureInfo.InvariantCulture);
			TypeDefinitionHandle enclosing = template.GetDeclaringType ();

			foreach (TypeDefinitionHandle candidate in reader.TypeDefinitions) {
				TypeDefinition typeDef = reader.GetTypeDefinition (candidate);
				if (reader.GetString (typeDef.Name) == wantedName && typeDef.GetDeclaringType ().Equals (enclosing)) {
					TypeLayout layout = typeDef.GetLayout ();
					if ((typeDef.Attributes & TypeAttributes.ExplicitLayout) == 0 ||
							layout.IsDefault || layout.Size != size) {
						throw new JniRewriteException ($"The assembly already contains '{wantedName}' with an incompatible layout.");
					}
					return candidate;
				}
			}

			foreach (SyntheticSizedType existing in syntheticSizedTypes) {
				if (existing.Size == size && existing.Enclosing.Equals (enclosing)) {
					return existing.Handle;
				}
			}

			var handle = MetadataTokens.TypeDefinitionHandle (sourceTypeDefCount + syntheticSizedTypes.Count + 1);
			syntheticSizedTypes.Add (new SyntheticSizedType (handle, entry.Utf8SizedType, enclosing, size));
			return handle;
		}

		void EmitSyntheticSizedTypes ()
		{
			if (syntheticSizedTypes.Count == 0) {
				return;
			}

			var fieldList = MetadataTokens.FieldDefinitionHandle (reader.GetTableRowCount (TableIndex.Field) + 1);
			var methodList = MetadataTokens.MethodDefinitionHandle (reader.GetTableRowCount (TableIndex.MethodDef) + 1);

			foreach (SyntheticSizedType synthetic in syntheticSizedTypes) {
				TypeDefinition template = reader.GetTypeDefinition (synthetic.Template);
				string name = FieldRvaTable.Utf8FieldNamePrefix + synthetic.Size.ToString (System.Globalization.CultureInfo.InvariantCulture);

				TypeDefinitionHandle added = metadata.AddTypeDefinition (
					template.Attributes,
					CloneString (template.Namespace),
					metadata.GetOrAddString (name),
					template.BaseType,
					fieldList,
					methodList);

				if (!added.Equals (synthetic.Handle)) {
					throw new JniRewriteException ("Internal error: a synthetic sized type did not land at its reserved token.");
				}

				metadata.AddTypeLayout (added, packingSize: 1, size: (uint) synthetic.Size);

				if (!synthetic.Enclosing.IsNil) {
					metadata.AddNestedType (added, synthetic.Enclosing);
				}
			}
		}

		void EmitMethodBodies ()
		{
			var encoder = new MethodBodyStreamEncoder (ilStream);
			int methodCount = reader.GetTableRowCount (TableIndex.MethodDef);

			for (int rid = 1; rid <= methodCount; rid++) {
				var handle = MetadataTokens.MethodDefinitionHandle (rid);
				MethodDefinition method = reader.GetMethodDefinition (handle);
				if (method.RelativeVirtualAddress == 0) {
					bodyOffsets [handle] = -1;
					continue;
				}

				MethodBodyBlock body = peReader.GetMethodBody (method.RelativeVirtualAddress);
				byte [] il = RewriteIL (handle, body.GetILBytes () ?? [], out bool hasDynamicStackAllocation);
				ImmutableArray<ExceptionRegion> regions = body.ExceptionRegions;

				MethodBodyStreamEncoder.MethodBody emitted = encoder.AddMethodBody (
					il.Length,
					body.MaxStack,
					regions.Length,
					HasSmallExceptionRegions (regions),
					body.LocalSignature,
					body.LocalVariablesInitialized ? MethodBodyAttributes.InitLocals : MethodBodyAttributes.None,
					hasDynamicStackAllocation);

				new BlobWriter (emitted.Instructions).WriteBytes (il);

				ExceptionRegionEncoder regionEncoder = emitted.ExceptionRegions;
				foreach (ExceptionRegion region in regions) {
					regionEncoder.Add (region.Kind, region.TryOffset, region.TryLength, region.HandlerOffset, region.HandlerLength,
						region.Kind == ExceptionRegionKind.Catch ? region.CatchType : default,
						region.Kind == ExceptionRegionKind.Filter ? region.FilterOffset : 0);
				}

				bodyOffsets [handle] = emitted.Offset;
			}
		}

		static bool HasSmallExceptionRegions (ImmutableArray<ExceptionRegion> regions)
		{
			if (!ExceptionRegionEncoder.IsSmallRegionCount (regions.Length)) {
				return false;
			}
			foreach (ExceptionRegion region in regions) {
				if (!ExceptionRegionEncoder.IsSmallExceptionRegion (region.TryOffset, region.TryLength) ||
						!ExceptionRegionEncoder.IsSmallExceptionRegion (region.HandlerOffset, region.HandlerLength)) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Copies a method body's IL, replacing every <c>ldstr</c> token: the <c>#US</c> heap is
		/// rebuilt from scratch, so even unchanged strings need a fresh handle. Instruction widths
		/// never change, so branch targets, exception region offsets, and PDB sequence points stay
		/// valid.
		/// </summary>
		byte [] RewriteIL (MethodDefinitionHandle method, byte [] original, out bool hasDynamicStackAllocation)
		{
			var il = (byte []) original.Clone ();
			Dictionary<int, string>? replacements = plan.GetUserStrings (method);
			bool localloc = false;

			IlInstructionScanner.Walk (il, (code, _, operandOffset, _) => {
				if (code == (ushort) ILOpCode.Localloc) {
					localloc = true;
					return;
				}
				if (code != (ushort) ILOpCode.Ldstr) {
					return;
				}

				uint token = IlInstructionScanner.ReadUInt32 (il, operandOffset);
				if ((token & 0xFF000000) != 0x70000000) {
					throw new JniRewriteException ($"Malformed IL: ldstr operand 0x{token:X8} is not a #US token.");
				}

				string? replacement = null;
				replacements?.TryGetValue (operandOffset, out replacement);
				string value = replacement ?? reader.GetUserString (MetadataTokens.UserStringHandle ((int) (token & 0x00FFFFFF)));

				UserStringHandle newHandle = metadata.GetOrAddUserString (value);
				IlInstructionScanner.WriteUInt32 (il, operandOffset, (uint) MetadataTokens.GetToken (newHandle));
			});

			hasDynamicStackAllocation = localloc;
			return il;
		}

		void EmitManagedResources ()
		{
			int resourceCount = reader.GetTableRowCount (TableIndex.ManifestResource);
			if (resourceCount == 0) {
				return;
			}

			CorHeader corHeader = GetCorHeader ();
			DirectoryEntry directory = corHeader.ResourcesDirectory;

			for (int rid = 1; rid <= resourceCount; rid++) {
				ManifestResource resource = reader.GetManifestResource (MetadataTokens.ManifestResourceHandle (rid));
				if (!resource.Implementation.IsNil) {
					continue; // Lives in another file; the offset is meaningful there, not here.
				}

				if (directory.RelativeVirtualAddress == 0) {
					throw new JniRewriteException ("The assembly declares embedded resources but has no resources directory.");
				}

				PEMemoryBlock block = peReader.GetSectionData (directory.RelativeVirtualAddress);
				if (block.Length < directory.Size) {
					throw new JniRewriteException ("The resources directory extends past the end of its PE section.");
				}

				// Use long arithmetic throughout: offset and size both come from the file (an
				// attacker- or corruption-controlled uint32), and offset + sizeof(int) + size can
				// overflow a 32-bit sum and wrap around to a small or negative value, which would
				// defeat the bounds check below instead of catching it.
				long offset = resource.Offset;
				long headerEnd = offset + sizeof (int);
				if (offset < 0 || headerEnd > directory.Size) {
					throw new JniRewriteException ($"Embedded resource '{reader.GetString (resource.Name)}' starts outside of the resources directory.");
				}

				int resourceOffset = (int) offset;
				int size = block.GetReader (resourceOffset, sizeof (int)).ReadInt32 ();
				if (size < 0 || headerEnd + (long) size > directory.Size) {
					throw new JniRewriteException ($"Embedded resource '{reader.GetString (resource.Name)}' extends past the end of the resources directory.");
				}

				managedResources.Align (ManagedPEBuilder.ManagedResourcesDataAlignment);
				resourceOffsets [MetadataTokens.ManifestResourceHandle (rid)] = managedResources.Count;
				managedResources.WriteInt32 (size);
				managedResources.WriteBytes (block.GetReader (resourceOffset + sizeof (int), size).ReadBytes (size));
			}
		}

		void CloneTables ()
		{
			CloneModule ();
			CloneTypeReferences ();
			CloneTypeDefinitions ();
			CloneFields ();
			CloneMethods ();
			CloneParameters ();
			CloneInterfaceImplementations ();
			CloneMemberReferences ();
			CloneConstants ();
			CloneCustomAttributes ();
			CloneFieldMarshals ();
			CloneDeclarativeSecurity ();
			CloneClassLayouts ();
			CloneFieldLayouts ();
			CloneStandaloneSignatures ();
			CloneEventsAndProperties ();
			CloneMethodImplementations ();
			CloneModuleReferences ();
			CloneTypeSpecifications ();
			CloneImplMaps ();
			CloneFieldRvas ();
			CloneAssembly ();
			CloneAssemblyReferences ();
			CloneFiles ();
			CloneExportedTypes ();
			CloneManifestResources ();
			CloneNestedClasses ();
			CloneGenericParameters ();
			CloneMethodSpecifications ();
			CloneGenericParameterConstraints ();
		}

		void CloneModule ()
		{
			ModuleDefinition module = reader.GetModuleDefinition ();
			metadata.AddModule (
				module.Generation,
				CloneString (module.Name),
				CloneGuid (module.Mvid),
				CloneGuid (module.GenerationId),
				CloneGuid (module.BaseGenerationId));
		}

		void CloneTypeReferences ()
		{
			int count = reader.GetTableRowCount (TableIndex.TypeRef);
			for (int rid = 1; rid <= count; rid++) {
				TypeReference typeRef = reader.GetTypeReference (MetadataTokens.TypeReferenceHandle (rid));
				metadata.AddTypeReference (typeRef.ResolutionScope, CloneString (typeRef.Namespace), CloneString (typeRef.Name));
			}
		}

		void CloneTypeDefinitions ()
		{
			int count = sourceTypeDefCount;
			var fieldLists = new FieldDefinitionHandle [count + 1];
			var methodLists = new MethodDefinitionHandle [count + 1];

			var nextField = MetadataTokens.FieldDefinitionHandle (reader.GetTableRowCount (TableIndex.Field) + 1);
			var nextMethod = MetadataTokens.MethodDefinitionHandle (reader.GetTableRowCount (TableIndex.MethodDef) + 1);

			// An empty member list is encoded as "the next type's first member", so the lists have
			// to be resolved from the back.
			for (int rid = count; rid >= 1; rid--) {
				TypeDefinition typeDef = reader.GetTypeDefinition (MetadataTokens.TypeDefinitionHandle (rid));

				foreach (FieldDefinitionHandle field in typeDef.GetFields ()) {
					nextField = field;
					break;
				}
				foreach (MethodDefinitionHandle method in typeDef.GetMethods ()) {
					nextMethod = method;
					break;
				}

				fieldLists [rid] = nextField;
				methodLists [rid] = nextMethod;
			}

			for (int rid = 1; rid <= count; rid++) {
				TypeDefinition typeDef = reader.GetTypeDefinition (MetadataTokens.TypeDefinitionHandle (rid));
				metadata.AddTypeDefinition (
					typeDef.Attributes,
					CloneString (typeDef.Namespace),
					CloneString (typeDef.Name),
					typeDef.BaseType,
					fieldLists [rid],
					methodLists [rid]);
			}
		}

		void CloneFields ()
		{
			int count = reader.GetTableRowCount (TableIndex.Field);
			for (int rid = 1; rid <= count; rid++) {
				var handle = MetadataTokens.FieldDefinitionHandle (rid);
				FieldDefinition field = reader.GetFieldDefinition (handle);
				metadata.AddFieldDefinition (field.Attributes, CloneString (field.Name), CloneFieldSignature (handle, field));
			}
		}

		BlobHandle CloneFieldSignature (FieldDefinitionHandle handle, FieldDefinition field)
		{
			if (!resizedFieldTypes.TryGetValue (handle, out TypeDefinitionHandle sizedType)) {
				return CloneBlob (field.Signature);
			}

			var blob = new BlobBuilder ();
			blob.WriteByte ((byte) SignatureKind.Field);
			blob.WriteByte ((byte) SignatureTypeKind.ValueType);
			blob.WriteCompressedInteger (CodedIndex.TypeDefOrRefOrSpec (sizedType));
			return metadata.GetOrAddBlob (blob);
		}

		void CloneMethods ()
		{
			int count = reader.GetTableRowCount (TableIndex.MethodDef);
			var parameterLists = new ParameterHandle [count + 1];
			var nextParameter = MetadataTokens.ParameterHandle (reader.GetTableRowCount (TableIndex.Param) + 1);

			for (int rid = count; rid >= 1; rid--) {
				MethodDefinition method = reader.GetMethodDefinition (MetadataTokens.MethodDefinitionHandle (rid));
				foreach (ParameterHandle parameter in method.GetParameters ()) {
					nextParameter = parameter;
					break;
				}
				parameterLists [rid] = nextParameter;
			}

			for (int rid = 1; rid <= count; rid++) {
				var handle = MetadataTokens.MethodDefinitionHandle (rid);
				MethodDefinition method = reader.GetMethodDefinition (handle);
				metadata.AddMethodDefinition (
					method.Attributes,
					method.ImplAttributes,
					CloneString (method.Name),
					CloneBlob (method.Signature),
					bodyOffsets [handle],
					parameterLists [rid]);
			}
		}

		void CloneParameters ()
		{
			int count = reader.GetTableRowCount (TableIndex.Param);
			for (int rid = 1; rid <= count; rid++) {
				Parameter parameter = reader.GetParameter (MetadataTokens.ParameterHandle (rid));
				metadata.AddParameter (parameter.Attributes, CloneString (parameter.Name), parameter.SequenceNumber);
			}
		}

		void CloneInterfaceImplementations ()
		{
			// The table is sorted by its (implicit) Class column, so walking the types in row
			// order reproduces the original row order exactly.
			foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
				TypeDefinition typeDef = reader.GetTypeDefinition (typeHandle);
				foreach (InterfaceImplementationHandle implHandle in typeDef.GetInterfaceImplementations ()) {
					InterfaceImplementation impl = reader.GetInterfaceImplementation (implHandle);
					metadata.AddInterfaceImplementation (typeHandle, impl.Interface);
				}
			}
		}

		void CloneMemberReferences ()
		{
			int count = reader.GetTableRowCount (TableIndex.MemberRef);
			for (int rid = 1; rid <= count; rid++) {
				MemberReference memberRef = reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (rid));
				metadata.AddMemberReference (memberRef.Parent, CloneString (memberRef.Name), CloneBlob (memberRef.Signature));
			}
		}

		void CloneConstants ()
		{
			int count = reader.GetTableRowCount (TableIndex.Constant);
			for (int rid = 1; rid <= count; rid++) {
				Constant constant = reader.GetConstant (MetadataTokens.ConstantHandle (rid));
				BlobReader value = reader.GetBlobReader (constant.Value);
				metadata.AddConstant (constant.Parent, value.ReadConstant (constant.TypeCode));
			}
		}

		void CloneCustomAttributes ()
		{
			int count = reader.GetTableRowCount (TableIndex.CustomAttribute);
			for (int rid = 1; rid <= count; rid++) {
				var handle = MetadataTokens.CustomAttributeHandle (rid);
				CustomAttribute attribute = reader.GetCustomAttribute (handle);
				byte []? replacement = plan.GetCustomAttributeBlob (handle);
				BlobHandle value = replacement != null ? metadata.GetOrAddBlob (replacement) : CloneBlob (attribute.Value);
				metadata.AddCustomAttribute (attribute.Parent, attribute.Constructor, value);
			}
		}

		void CloneFieldMarshals ()
		{
			foreach (FieldDefinitionHandle handle in reader.FieldDefinitions) {
				BlobHandle descriptor = reader.GetFieldDefinition (handle).GetMarshallingDescriptor ();
				if (!descriptor.IsNil) {
					metadata.AddMarshallingDescriptor (handle, CloneBlob (descriptor));
				}
			}

			int parameterCount = reader.GetTableRowCount (TableIndex.Param);
			for (int rid = 1; rid <= parameterCount; rid++) {
				var handle = MetadataTokens.ParameterHandle (rid);
				BlobHandle descriptor = reader.GetParameter (handle).GetMarshallingDescriptor ();
				if (!descriptor.IsNil) {
					metadata.AddMarshallingDescriptor (handle, CloneBlob (descriptor));
				}
			}
		}

		void CloneDeclarativeSecurity ()
		{
			int count = reader.GetTableRowCount (TableIndex.DeclSecurity);
			for (int rid = 1; rid <= count; rid++) {
				DeclarativeSecurityAttribute security = reader.GetDeclarativeSecurityAttribute (MetadataTokens.DeclarativeSecurityAttributeHandle (rid));
				metadata.AddDeclarativeSecurityAttribute (security.Parent, security.Action, CloneBlob (security.PermissionSet));
			}
		}

		void CloneClassLayouts ()
		{
			foreach (TypeDefinitionHandle handle in reader.TypeDefinitions) {
				TypeLayout layout = reader.GetTypeDefinition (handle).GetLayout ();
				if (!layout.IsDefault) {
					metadata.AddTypeLayout (handle, checked ((ushort) layout.PackingSize), checked ((uint) layout.Size));
				}
			}
		}

		void CloneFieldLayouts ()
		{
			foreach (FieldDefinitionHandle handle in reader.FieldDefinitions) {
				int offset = reader.GetFieldDefinition (handle).GetOffset ();
				if (offset >= 0) {
					metadata.AddFieldLayout (handle, offset);
				}
			}
		}

		void CloneStandaloneSignatures ()
		{
			int count = reader.GetTableRowCount (TableIndex.StandAloneSig);
			for (int rid = 1; rid <= count; rid++) {
				StandaloneSignature signature = reader.GetStandaloneSignature (MetadataTokens.StandaloneSignatureHandle (rid));
				metadata.AddStandaloneSignature (CloneBlob (signature.Signature));
			}
		}

		void CloneEventsAndProperties ()
		{
			int eventCount = reader.GetTableRowCount (TableIndex.Event);
			for (int rid = 1; rid <= eventCount; rid++) {
				EventDefinition eventDef = reader.GetEventDefinition (MetadataTokens.EventDefinitionHandle (rid));
				metadata.AddEvent (eventDef.Attributes, CloneString (eventDef.Name), eventDef.Type);
			}

			int propertyCount = reader.GetTableRowCount (TableIndex.Property);
			for (int rid = 1; rid <= propertyCount; rid++) {
				PropertyDefinition property = reader.GetPropertyDefinition (MetadataTokens.PropertyDefinitionHandle (rid));
				metadata.AddProperty (property.Attributes, CloneString (property.Name), CloneBlob (property.Signature));
			}

			foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions) {
				TypeDefinition typeDef = reader.GetTypeDefinition (typeHandle);

				foreach (EventDefinitionHandle eventHandle in typeDef.GetEvents ()) {
					metadata.AddEventMap (typeHandle, eventHandle);
					break;
				}
				foreach (PropertyDefinitionHandle propertyHandle in typeDef.GetProperties ()) {
					metadata.AddPropertyMap (typeHandle, propertyHandle);
					break;
				}
			}

			int associationCount = Math.Max (eventCount, propertyCount);
			for (int rid = 1; rid <= associationCount; rid++) {
				// HasSemantics uses Event tag 0 and Property tag 1, so associations are sorted
				// Event 1, Property 1, Event 2, Property 2, and so on.
				if (rid <= eventCount) {
					var handle = MetadataTokens.EventDefinitionHandle (rid);
					EventAccessors accessors = reader.GetEventDefinition (handle).GetAccessors ();
					AddSemantics (handle, MethodSemanticsAttributes.Adder, accessors.Adder);
					AddSemantics (handle, MethodSemanticsAttributes.Remover, accessors.Remover);
					AddSemantics (handle, MethodSemanticsAttributes.Raiser, accessors.Raiser);
					foreach (MethodDefinitionHandle other in accessors.Others) {
						AddSemantics (handle, MethodSemanticsAttributes.Other, other);
					}
				}

				if (rid <= propertyCount) {
					var handle = MetadataTokens.PropertyDefinitionHandle (rid);
					PropertyAccessors accessors = reader.GetPropertyDefinition (handle).GetAccessors ();
					AddSemantics (handle, MethodSemanticsAttributes.Getter, accessors.Getter);
					AddSemantics (handle, MethodSemanticsAttributes.Setter, accessors.Setter);
					foreach (MethodDefinitionHandle other in accessors.Others) {
						AddSemantics (handle, MethodSemanticsAttributes.Other, other);
					}
				}
			}
		}

		void AddSemantics (EntityHandle association, MethodSemanticsAttributes semantics, MethodDefinitionHandle method)
		{
			if (!method.IsNil) {
				metadata.AddMethodSemantics (association, semantics, method);
			}
		}

		void CloneMethodImplementations ()
		{
			int count = reader.GetTableRowCount (TableIndex.MethodImpl);
			for (int rid = 1; rid <= count; rid++) {
				MethodImplementation impl = reader.GetMethodImplementation (MetadataTokens.MethodImplementationHandle (rid));
				metadata.AddMethodImplementation ((TypeDefinitionHandle) impl.Type, impl.MethodBody, impl.MethodDeclaration);
			}
		}

		void CloneModuleReferences ()
		{
			int count = reader.GetTableRowCount (TableIndex.ModuleRef);
			for (int rid = 1; rid <= count; rid++) {
				ModuleReference moduleRef = reader.GetModuleReference (MetadataTokens.ModuleReferenceHandle (rid));
				metadata.AddModuleReference (CloneString (moduleRef.Name));
			}
		}

		void CloneTypeSpecifications ()
		{
			int count = reader.GetTableRowCount (TableIndex.TypeSpec);
			for (int rid = 1; rid <= count; rid++) {
				TypeSpecification typeSpec = reader.GetTypeSpecification (MetadataTokens.TypeSpecificationHandle (rid));
				metadata.AddTypeSpecification (CloneBlob (typeSpec.Signature));
			}
		}

		void CloneImplMaps ()
		{
			int count = reader.GetTableRowCount (TableIndex.MethodDef);
			for (int rid = 1; rid <= count; rid++) {
				var handle = MetadataTokens.MethodDefinitionHandle (rid);
				MethodImport import = reader.GetMethodDefinition (handle).GetImport ();
				if (!import.Module.IsNil) {
					metadata.AddMethodImport (handle, import.Attributes, CloneString (import.Name), import.Module);
				}
			}
		}

		void CloneFieldRvas ()
		{
			foreach (FieldRvaEntry entry in fieldRvaTable.Entries) {
				if (!newFieldRvaOffsets.TryGetValue (entry.Field, out int offset)) {
					throw new JniRewriteException ("Internal error: a FieldRVA row was not laid out.");
				}
				metadata.AddFieldRelativeVirtualAddress (entry.Field, offset);
			}
		}

		void CloneAssembly ()
		{
			if (!reader.IsAssembly) {
				return;
			}

			AssemblyDefinition assembly = reader.GetAssemblyDefinition ();
			metadata.AddAssembly (
				CloneString (assembly.Name),
				assembly.Version,
				CloneString (assembly.Culture),
				CloneBlob (assembly.PublicKey),
				assembly.Flags,
				assembly.HashAlgorithm);
		}

		void CloneAssemblyReferences ()
		{
			int count = reader.GetTableRowCount (TableIndex.AssemblyRef);
			for (int rid = 1; rid <= count; rid++) {
				AssemblyReference assemblyRef = reader.GetAssemblyReference (MetadataTokens.AssemblyReferenceHandle (rid));
				metadata.AddAssemblyReference (
					CloneString (assemblyRef.Name),
					assemblyRef.Version,
					CloneString (assemblyRef.Culture),
					CloneBlob (assemblyRef.PublicKeyOrToken),
					assemblyRef.Flags,
					CloneBlob (assemblyRef.HashValue));
			}
		}

		void CloneFiles ()
		{
			int count = reader.GetTableRowCount (TableIndex.File);
			for (int rid = 1; rid <= count; rid++) {
				AssemblyFile file = reader.GetAssemblyFile (MetadataTokens.AssemblyFileHandle (rid));
				metadata.AddAssemblyFile (CloneString (file.Name), CloneBlob (file.HashValue), file.ContainsMetadata);
			}
		}

		void CloneExportedTypes ()
		{
			int count = reader.GetTableRowCount (TableIndex.ExportedType);
			for (int rid = 1; rid <= count; rid++) {
				ExportedType exported = reader.GetExportedType (MetadataTokens.ExportedTypeHandle (rid));
				metadata.AddExportedType (
					exported.Attributes,
					CloneString (exported.Namespace),
					CloneString (exported.Name),
					exported.Implementation,
					MetadataRawColumns.GetExportedTypeDefinitionId (reader, rid));
			}
		}

		void CloneManifestResources ()
		{
			int count = reader.GetTableRowCount (TableIndex.ManifestResource);
			for (int rid = 1; rid <= count; rid++) {
				var handle = MetadataTokens.ManifestResourceHandle (rid);
				ManifestResource resource = reader.GetManifestResource (handle);
				uint offset = resourceOffsets.TryGetValue (handle, out int newOffset)
					? (uint) newOffset
					: checked ((uint) resource.Offset);
				metadata.AddManifestResource (resource.Attributes, CloneString (resource.Name), resource.Implementation, offset);
			}
		}

		void CloneNestedClasses ()
		{
			foreach (TypeDefinitionHandle handle in reader.TypeDefinitions) {
				TypeDefinitionHandle enclosing = reader.GetTypeDefinition (handle).GetDeclaringType ();
				if (!enclosing.IsNil) {
					metadata.AddNestedType (handle, enclosing);
				}
			}
		}

		void CloneGenericParameters ()
		{
			int count = reader.GetTableRowCount (TableIndex.GenericParam);
			for (int rid = 1; rid <= count; rid++) {
				GenericParameter parameter = reader.GetGenericParameter (MetadataTokens.GenericParameterHandle (rid));
				metadata.AddGenericParameter (parameter.Parent, parameter.Attributes, CloneString (parameter.Name), parameter.Index);
			}
		}

		void CloneGenericParameterConstraints ()
		{
			int count = reader.GetTableRowCount (TableIndex.GenericParamConstraint);
			for (int rid = 1; rid <= count; rid++) {
				GenericParameterConstraint constraint = reader.GetGenericParameterConstraint (MetadataTokens.GenericParameterConstraintHandle (rid));
				metadata.AddGenericParameterConstraint (constraint.Parameter, constraint.Type);
			}
		}

		void CloneMethodSpecifications ()
		{
			int count = reader.GetTableRowCount (TableIndex.MethodSpec);
			for (int rid = 1; rid <= count; rid++) {
				MethodSpecification methodSpec = reader.GetMethodSpecification (MetadataTokens.MethodSpecificationHandle (rid));
				metadata.AddMethodSpecification (methodSpec.Method, CloneBlob (methodSpec.Signature));
			}
		}

		StringHandle CloneString (StringHandle handle) => handle.IsNil ? default : metadata.GetOrAddString (reader.GetString (handle));

		BlobHandle CloneBlob (BlobHandle handle) => handle.IsNil ? default : metadata.GetOrAddBlob (reader.GetBlobBytes (handle));

		GuidHandle CloneGuid (GuidHandle handle) => handle.IsNil ? default : metadata.GetOrAddGuid (reader.GetGuid (handle));

		CorHeader GetCorHeader ()
		{
			CorHeader? corHeader = peReader.PEHeaders.CorHeader;
			if (corHeader == null) {
				throw new JniRewriteException ("The file has no CLI header and is not a managed assembly.");
			}
			return corHeader;
		}

		AssemblyRebuildResult Serialize ()
		{
			CorHeader corHeader = GetCorHeader ();
			CoffHeader coffHeader = peReader.PEHeaders.CoffHeader;
			PEHeader? peHeader = peReader.PEHeaders.PEHeader;
			if (peHeader == null) {
				throw new JniRewriteException ("The file has no PE optional header and cannot be rebuilt.");
			}

			bool wasStrongNameSigned = (corHeader.Flags & CorFlags.StrongNameSigned) != 0;
			CorFlags flags = wasStrongNameSigned ? corHeader.Flags & ~CorFlags.StrongNameSigned : corHeader.Flags;

			// Reserve the original signature directory's size even though the flag is cleared:
			// the rebuilt image is delay-signed (the space for a signature is preserved) rather
			// than left with no room to re-sign it later.
			int strongNameSignatureSize = corHeader.StrongNameSignatureDirectory.Size;

			var headerBuilder = new PEHeaderBuilder (
				machine: coffHeader.Machine,
				sectionAlignment: peHeader.SectionAlignment,
				fileAlignment: peHeader.FileAlignment,
				imageBase: peHeader.ImageBase,
				majorLinkerVersion: peHeader.MajorLinkerVersion,
				minorLinkerVersion: peHeader.MinorLinkerVersion,
				majorOperatingSystemVersion: peHeader.MajorOperatingSystemVersion,
				minorOperatingSystemVersion: peHeader.MinorOperatingSystemVersion,
				majorImageVersion: peHeader.MajorImageVersion,
				minorImageVersion: peHeader.MinorImageVersion,
				majorSubsystemVersion: peHeader.MajorSubsystemVersion,
				minorSubsystemVersion: peHeader.MinorSubsystemVersion,
				subsystem: peHeader.Subsystem,
				dllCharacteristics: peHeader.DllCharacteristics,
				imageCharacteristics: coffHeader.Characteristics,
				sizeOfStackReserve: peHeader.SizeOfStackReserve,
				sizeOfStackCommit: peHeader.SizeOfStackCommit,
				sizeOfHeapReserve: peHeader.SizeOfHeapReserve,
				sizeOfHeapCommit: peHeader.SizeOfHeapCommit);

			int entryPointToken = corHeader.EntryPointTokenOrRelativeVirtualAddress;
			MethodDefinitionHandle entryPoint = entryPointToken == 0
				? default
				: MetadataTokens.MethodDefinitionHandle (entryPointToken & 0x00FFFFFF);

			Guid contentIdGuid = GetModuleVersionId ();
			uint timeDateStamp = unchecked ((uint) coffHeader.TimeDateStamp);

			var peBuilder = new ManagedPEBuilder (
				headerBuilder,
				new MetadataRootBuilder (metadata, reader.MetadataVersion),
				ilStream,
				mappedFieldData: mappedFieldData.Count > 0 ? mappedFieldData : null,
				managedResources: managedResources.Count > 0 ? managedResources : null,
				nativeResources: NativeResourceSectionCopier.TryCreate (peReader),
				debugDirectoryBuilder: CloneDebugDirectory (),
				strongNameSignatureSize: strongNameSignatureSize,
				entryPoint: entryPoint,
				flags: flags,
				deterministicIdProvider: _ => new BlobContentId (contentIdGuid, timeDateStamp));

			var peBlob = new BlobBuilder ();
			try {
				peBuilder.Serialize (peBlob);
			} catch (InvalidOperationException e) {
				throw new JniRewriteException ($"The rebuilt metadata was rejected during serialization: {e.Message}", e);
			} catch (ArgumentException e) {
				throw new JniRewriteException ($"The rebuilt metadata was rejected during serialization: {e.Message}", e);
			}

			using var stream = new MemoryStream ();
			peBlob.WriteContentTo (stream);
			return new AssemblyRebuildResult (stream.ToArray (), wasStrongNameSigned);
		}

		Guid GetModuleVersionId ()
		{
			GuidHandle mvid = reader.GetModuleDefinition ().Mvid;
			return mvid.IsNil ? Guid.Empty : reader.GetGuid (mvid);
		}

		/// <summary>
		/// Reproduces the source debug directory so an existing portable PDB keeps matching:
		/// method tokens and IL offsets are preserved by construction, and the CodeView identity
		/// (GUID, age, path) is copied verbatim.
		/// </summary>
		DebugDirectoryBuilder CloneDebugDirectory ()
		{
			var builder = new DebugDirectoryBuilder ();

			foreach (DebugDirectoryEntry entry in peReader.ReadDebugDirectory ()) {
				switch (entry.Type) {
				case DebugDirectoryEntryType.CodeView: {
					CodeViewDebugDirectoryData data = peReader.ReadCodeViewDebugDirectoryData (entry);
					ushort portablePdbVersion = entry.IsPortableCodeView ? entry.MajorVersion : (ushort) 0;
					builder.AddCodeViewEntry (data.Path, new BlobContentId (data.Guid, entry.Stamp), portablePdbVersion, data.Age);
					break;
				}
				case DebugDirectoryEntryType.PdbChecksum: {
					PdbChecksumDebugDirectoryData data = peReader.ReadPdbChecksumDebugDirectoryData (entry);
					builder.AddPdbChecksumEntry (data.AlgorithmName, data.Checksum);
					break;
				}
				case DebugDirectoryEntryType.Reproducible:
					builder.AddReproducibleEntry ();
					break;
				default:
					AddRawDebugDirectoryEntry (builder, entry);
					break;
				}
			}

			return builder;
		}

		void AddRawDebugDirectoryEntry (DebugDirectoryBuilder builder, DebugDirectoryEntry entry)
		{
			uint version = (uint) entry.MajorVersion | ((uint) entry.MinorVersion << 16);
			if (entry.DataSize == 0) {
				builder.AddEntry (entry.Type, version, entry.Stamp);
				return;
			}

			PEMemoryBlock block = peReader.GetSectionData (entry.DataRelativeVirtualAddress);
			if (block.Length < entry.DataSize) {
				throw new JniRewriteException ($"Debug directory entry '{entry.Type}' points at data outside of any PE section.");
			}

			byte [] data = block.GetReader (0, entry.DataSize).ReadBytes (entry.DataSize);
			builder.AddEntry (entry.Type, version, entry.Stamp, data, static (blob, bytes) => blob.WriteBytes (bytes));
		}
	}
}
