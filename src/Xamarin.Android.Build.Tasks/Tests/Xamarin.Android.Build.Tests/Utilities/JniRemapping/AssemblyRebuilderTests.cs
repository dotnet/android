using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AssemblyRebuilderTests : BaseTest
	{
		[Test]
		public void EmptyPlanPreservesMappedFieldDataAndResources ()
		{
			var fixture = new JniFixtureBuilder ();
			byte [] fieldData = { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
			byte [] resourceData = { 1, 2, 3, 4, 5, 6, 7 };
			fixture.AddEmbeddedResource ("Fixture.resources", resourceData);

			TypeDefinitionHandle enclosing = fixture.EnsurePrivateImplementationDetails ();
			TypeDefinitionHandle dataType = fixture.AddType (null, "__StaticArrayInitTypeSize=8", fixture.NextFieldRid, fixture.NextMethodRid,
				TypeAttributes.NestedPrivate | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
				fixture.ValueTypeReference);
			fixture.Metadata.AddTypeLayout (dataType, packingSize: 1, size: (uint) fieldData.Length);
			fixture.Metadata.AddNestedType (dataType, enclosing);

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Type (dataType, isValueType: true);
			int rva = fixture.MappedFieldData.Count;
			fixture.MappedFieldData.WriteBytes (fieldData);
			FieldDefinitionHandle dataField = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA,
				fixture.Metadata.GetOrAddString ("ArrayData"), fixture.Metadata.GetOrAddBlob (signature));
			fixture.Metadata.AddFieldRelativeVirtualAddress (dataField, rva);

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			FieldRvaTable sourceFieldRvas = FieldRvaTable.Read (sourcePe, before);

			var result = new AssemblyRebuilder (sourcePe, before, new JniRewritePlan (), sourceFieldRvas).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			FieldRvaTable rebuiltFieldRvas = FieldRvaTable.Read (rebuiltPe, after);
			FieldRvaEntry rebuiltField = rebuiltFieldRvas.Get (dataField);

			Assert.IsFalse (result.StrongNameSignatureCleared);
			Assert.IsNotNull (rebuiltField);
			CollectionAssert.AreEqual (fieldData, rebuiltField.Data);
			CollectionAssert.AreEqual (resourceData, ReadResource (rebuiltPe, after, "Fixture.resources"));
			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid));

			for (int i = 0; i < MetadataTokens.TableCount; i++) {
				var table = (TableIndex) i;
				Assert.AreEqual (before.GetTableRowCount (table), after.GetTableRowCount (table), $"Row count of table '{table}' changed.");
			}
		}

		[Test]
		public void RejectsFieldBackedImplMapRows ()
		{
			var fixture = new JniFixtureBuilder ();
			fixture.AddType ("Acme", "NativeData", fixture.NextFieldRid, fixture.NextMethodRid);

			var fieldSignature = new BlobBuilder ();
			new BlobEncoder (fieldSignature).FieldSignature ().Int32 ();
			FieldDefinitionHandle field = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.PinvokeImpl,
				fixture.Metadata.GetOrAddString ("NativeField"),
				fixture.Metadata.GetOrAddBlob (fieldSignature));

			MethodDefinitionHandle method = fixture.AddVoidMethod ("Placeholder", fixture.EmitReturnOnlyBody ());
			ModuleReferenceHandle module = fixture.Metadata.AddModuleReference (fixture.Metadata.GetOrAddString ("native"));
			fixture.Metadata.AddMethodImport (
				method,
				MethodImportAttributes.CallingConventionCDecl,
				fixture.Metadata.GetOrAddString ("native_field"),
				module);

			byte [] source = fixture.Serialize ();
			PatchImplMapMemberForwarded (source, field);

			using var peReader = new PEReader (ImmutableArray.Create (source));
			MetadataReader reader = peReader.GetMetadataReader ();
			FieldRvaTable fieldRvas = FieldRvaTable.Read (peReader, reader);

			var ex = Assert.Throws<JniRewriteException> (() =>
				new AssemblyRebuilder (peReader, reader, new JniRewritePlan (), fieldRvas).Build ());
			StringAssert.Contains ("field-backed ImplMap", ex.Message);
		}

		[Test]
		public void RejectsSwitchOperandWhoseSizeWouldOverflow ()
		{
			const int switchOffset = 10;
			var il = new byte [switchOffset + 5];
			il [switchOffset] = (byte) ILOpCode.Switch;
			uint caseCount = (uint) ((int.MaxValue - sizeof (uint)) / sizeof (int));
			IlInstructionScanner.WriteUInt32 (il, switchOffset + 1, caseCount);

			var ex = Assert.Throws<JniRewriteException> (() =>
				IlInstructionScanner.Walk (il, (_, _, _, _) => { }));
			StringAssert.Contains ("extends past the end", ex.Message);
		}

		[Test]
		public void RejectsNegativeCompressedIntegerOffset ()
		{
			var ex = Assert.Throws<JniRewriteException> (() =>
				MetadataEncoding.ReadCompressedInteger (new byte [1], -1, out int _));
			StringAssert.Contains ("truncated compressed integer", ex.Message);
		}

		[Test]
		public void EmptyPlanPreservesFieldRvasFromDifferentSections ()
		{
			var fixture = new JniFixtureBuilder ();
			fixture.AddType ("Acme", "MappedData", fixture.NextFieldRid, fixture.NextMethodRid);

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Int32 ();
			BlobHandle signatureHandle = fixture.Metadata.GetOrAddBlob (signature);

			FieldDefinitionHandle firstField = AddMappedInt32Field (fixture, signatureHandle, "First", 0x12345678);
			FieldDefinitionHandle secondField = AddMappedInt32Field (fixture, signatureHandle, "Second", 0x23456789);

			byte [] source = fixture.Serialize ();
			MoveFieldRvaToAnotherSection (source, firstField, secondField);

			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			FieldRvaTable sourceFieldRvas = FieldRvaTable.Read (sourcePe, before);

			var result = new AssemblyRebuilder (sourcePe, before, new JniRewritePlan (), sourceFieldRvas).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			FieldRvaTable rebuiltFieldRvas = FieldRvaTable.Read (rebuiltPe, after);

			CollectionAssert.AreEqual (sourceFieldRvas.Get (firstField).Data, rebuiltFieldRvas.Get (firstField).Data);
			CollectionAssert.AreEqual (sourceFieldRvas.Get (secondField).Data, rebuiltFieldRvas.Get (secondField).Data);
		}

		[Test]
		public void LongerUtf8FieldReplacementAddsCorrectlySizedNestedType ()
		{
			const string replacement = "aMuchLongerReplacement";
			int replacementSize = Encoding.UTF8.GetByteCount (replacement) + 1;
			var fixture = new JniFixtureBuilder ();
			FieldDefinitionHandle field = fixture.AddUtf8Field ("x");

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			var plan = new JniRewritePlan ();
			plan.AddUtf8FieldValue (field, replacement);

			var result = new AssemblyRebuilder (sourcePe, before, plan, FieldRvaTable.Read (sourcePe, before)).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			FieldRvaEntry rebuiltField = GetRequiredFieldRva (rebuiltPe, after, field);
			TypeDefinitionHandle sizedType = GetFieldValueType (after, field);
			TypeDefinition type = after.GetTypeDefinition (sizedType);
			TypeDefinitionHandle enclosing = type.GetDeclaringType ();
			TypeLayout layout = type.GetLayout ();

			Assert.AreEqual (replacement, rebuiltField.Utf8Value);
			Assert.AreEqual (before.GetTableRowCount (TableIndex.TypeDef) + 1, after.GetTableRowCount (TableIndex.TypeDef));
			Assert.AreEqual (before.GetTableRowCount (TableIndex.ClassLayout) + 1, after.GetTableRowCount (TableIndex.ClassLayout));
			Assert.AreEqual (before.GetTableRowCount (TableIndex.NestedClass) + 1, after.GetTableRowCount (TableIndex.NestedClass));
			Assert.AreEqual (FieldRvaTable.Utf8FieldNamePrefix + replacementSize, after.GetString (type.Name));
			Assert.AreEqual (JniFixtureBuilder.PrivateImplementationDetails, after.GetString (after.GetTypeDefinition (enclosing).Name));
			Assert.AreEqual (replacementSize, layout.Size);
			Assert.AreEqual (1, layout.PackingSize);

			AssertTableRowCountsMatchExcept (before, after, TableIndex.TypeDef, TableIndex.ClassLayout, TableIndex.NestedClass);
		}

		[Test]
		public void ShorterUtf8FieldReplacementPreservesTypeAndZeroFillsSlot ()
		{
			var fixture = new JniFixtureBuilder ();
			FieldDefinitionHandle field = fixture.AddUtf8Field ("longOriginalValue");

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			TypeDefinitionHandle originalType = GetFieldValueType (before, field);
			int originalSize = GetRequiredFieldRva (sourcePe, before, field).Data.Length;
			var plan = new JniRewritePlan ();
			plan.AddUtf8FieldValue (field, "x");

			var result = new AssemblyRebuilder (sourcePe, before, plan, FieldRvaTable.Read (sourcePe, before)).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			FieldRvaEntry rebuiltField = GetRequiredFieldRva (rebuiltPe, after, field);

			Assert.AreEqual (originalType, GetFieldValueType (after, field));
			Assert.AreEqual ("x", rebuiltField.Utf8Value);
			Assert.AreEqual (originalSize, rebuiltField.Data.Length);
			Assert.AreEqual ((byte) 'x', rebuiltField.Data [0]);
			for (int i = 1; i < rebuiltField.Data.Length; i++) {
				Assert.AreEqual (0, rebuiltField.Data [i], $"Mapped field byte {i} was not zero-filled.");
			}
			AssertTableRowCountsMatchExcept (before, after);
		}

		[Test]
		public void ShorterUtf8FieldReplacementDoesNotModifyAliasedField ()
		{
			const string original = "longOriginalValue";
			var fixture = new JniFixtureBuilder ();
			(FieldDefinitionHandle field, FieldDefinitionHandle alias) = AddAliasedUtf8Fields (fixture, original);

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			TypeDefinitionHandle originalType = GetFieldValueType (before, field);
			byte [] aliasData = GetRequiredFieldRva (sourcePe, before, alias).Data;
			var plan = new JniRewritePlan ();
			plan.AddUtf8FieldValue (field, "x");

			var result = new AssemblyRebuilder (sourcePe, before, plan, FieldRvaTable.Read (sourcePe, before)).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			FieldRvaEntry rebuiltField = GetRequiredFieldRva (rebuiltPe, after, field);
			FieldRvaEntry rebuiltAlias = GetRequiredFieldRva (rebuiltPe, after, alias);

			Assert.AreEqual (originalType, GetFieldValueType (after, field));
			Assert.AreEqual ("x", rebuiltField.Utf8Value);
			CollectionAssert.AreEqual (aliasData, rebuiltAlias.Data);
			Assert.AreNotEqual (rebuiltField.RelativeVirtualAddress, rebuiltAlias.RelativeVirtualAddress);
			AssertTableRowCountsMatchExcept (before, after);
		}

		[Test]
		public void SharedUserStringCanBeReplacedAtOneUseSite ()
		{
			var fixture = new JniFixtureBuilder ();
			UserStringHandle shared = fixture.String ("shared");
			MethodDefinitionHandle method = fixture.AddVoidMethod ("LoadShared", fixture.EmitLoadStringBody (shared, shared));
			fixture.AddType ("Acme", "StringUser", fixture.NextFieldRid, MetadataTokens.GetRowNumber (method));

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader before = sourcePe.GetMetadataReader ();
			var plan = new JniRewritePlan ();
			plan.AddUserString (method, operandOffset: 1, "changed");

			var result = new AssemblyRebuilder (sourcePe, before, plan, FieldRvaTable.Read (sourcePe, before)).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			MetadataReader after = rebuiltPe.GetMetadataReader ();
			byte [] il = rebuiltPe.GetMethodBody (after.GetMethodDefinition (method).RelativeVirtualAddress).GetILBytes ();
			var tokens = new System.Collections.Generic.List<int> ();
			var values = new System.Collections.Generic.List<string> ();
			IlInstructionScanner.Walk (il, (code, _, operandOffset, _) => {
				if (code != (ushort) ILOpCode.Ldstr) {
					return;
				}
				int token = checked ((int) IlInstructionScanner.ReadUInt32 (il, operandOffset));
				tokens.Add (token);
				values.Add (after.GetUserString (MetadataTokens.UserStringHandle (token & 0x00FFFFFF)));
			});

			CollectionAssert.AreEqual (new [] { "changed", "shared" }, values);
			Assert.AreEqual (2, tokens.Count);
			Assert.AreNotEqual (tokens [0], tokens [1], "The two use sites should resolve through distinct #US tokens.");
		}

		[Test]
		public void StrongNameFlagIsClearedButSignatureSpaceIsPreserved ()
		{
			var fixture = new JniFixtureBuilder (hasPublicKey: true) {
				Flags = CorFlags.ILOnly | CorFlags.StrongNameSigned,
				StrongNameSignatureSize = 128,
			};

			byte [] source = fixture.Serialize ();
			using var sourcePe = new PEReader (ImmutableArray.Create (source));
			MetadataReader reader = sourcePe.GetMetadataReader ();

			var result = new AssemblyRebuilder (sourcePe, reader, new JniRewritePlan (), FieldRvaTable.Read (sourcePe, reader)).Build ();

			using var rebuiltPe = new PEReader (ImmutableArray.Create (result.Image));
			CorHeader corHeader = rebuiltPe.PEHeaders.CorHeader;
			Assert.IsTrue (result.StrongNameSignatureCleared);
			Assert.AreEqual ((CorFlags) 0, corHeader.Flags & CorFlags.StrongNameSigned);
			Assert.AreEqual (128, corHeader.StrongNameSignatureDirectory.Size);
			Assert.AreNotEqual (0, corHeader.StrongNameSignatureDirectory.RelativeVirtualAddress);
		}

		static FieldRvaEntry GetRequiredFieldRva (PEReader peReader, MetadataReader reader, FieldDefinitionHandle field)
		{
			FieldRvaEntry? entry = FieldRvaTable.Read (peReader, reader).Get (field);
			if (entry == null) {
				throw new AssertionException ($"Field 0x{MetadataTokens.GetToken (field):X8} has no FieldRVA row.");
			}
			return entry;
		}

		static TypeDefinitionHandle GetFieldValueType (MetadataReader reader, FieldDefinitionHandle field)
		{
			BlobReader signature = reader.GetBlobReader (reader.GetFieldDefinition (field).Signature);
			Assert.AreEqual (SignatureKind.Field, signature.ReadSignatureHeader ().Kind);
			Assert.AreEqual ((int) SignatureTypeKind.ValueType, signature.ReadCompressedInteger ());
			EntityHandle type = signature.ReadTypeHandle ();
			Assert.AreEqual (HandleKind.TypeDefinition, type.Kind);
			return (TypeDefinitionHandle) type;
		}

		static void AssertTableRowCountsMatchExcept (MetadataReader expected, MetadataReader actual, params TableIndex [] except)
		{
			for (int i = 0; i < MetadataTokens.TableCount; i++) {
				var table = (TableIndex) i;
				if (Array.IndexOf (except, table) >= 0) {
					continue;
				}
				Assert.AreEqual (expected.GetTableRowCount (table), actual.GetTableRowCount (table), $"Row count of table '{table}' changed.");
			}
		}

		static FieldDefinitionHandle AddMappedInt32Field (JniFixtureBuilder fixture, BlobHandle signature, string name, int value)
		{
			int rva = fixture.MappedFieldData.Count;
			fixture.MappedFieldData.WriteInt32 (value);
			FieldDefinitionHandle field = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.HasFieldRVA,
				fixture.Metadata.GetOrAddString (name),
				signature);
			fixture.Metadata.AddFieldRelativeVirtualAddress (field, rva);
			return field;
		}

		static (FieldDefinitionHandle Field, FieldDefinitionHandle Alias) AddAliasedUtf8Fields (JniFixtureBuilder fixture, string value)
		{
			int size = Encoding.UTF8.GetByteCount (value) + 1;
			TypeDefinitionHandle enclosing = fixture.EnsurePrivateImplementationDetails ();
			TypeDefinitionHandle dataType = fixture.AddType (null, FieldRvaTable.Utf8FieldNamePrefix + size,
				fixture.NextFieldRid, fixture.NextMethodRid,
				TypeAttributes.NestedAssembly | TypeAttributes.ExplicitLayout | TypeAttributes.Sealed | TypeAttributes.AnsiClass,
				fixture.ValueTypeReference);
			fixture.Metadata.AddTypeLayout (dataType, packingSize: 1, size: (uint) size);
			fixture.Metadata.AddNestedType (dataType, enclosing);

			var signature = new BlobBuilder ();
			new BlobEncoder (signature).FieldSignature ().Type (dataType, isValueType: true);
			BlobHandle signatureHandle = fixture.Metadata.GetOrAddBlob (signature);
			int rva = fixture.MappedFieldData.Count;
			fixture.MappedFieldData.WriteUTF8 (value);
			fixture.MappedFieldData.WriteByte (0);

			FieldDefinitionHandle field = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA | FieldAttributes.InitOnly,
				fixture.Metadata.GetOrAddString (FieldRvaTable.Utf8FieldNamePrefix + "0"), signatureHandle);
			fixture.Metadata.AddFieldRelativeVirtualAddress (field, rva);
			FieldDefinitionHandle alias = fixture.Metadata.AddFieldDefinition (
				FieldAttributes.Static | FieldAttributes.Assembly | FieldAttributes.HasFieldRVA | FieldAttributes.InitOnly,
				fixture.Metadata.GetOrAddString ("Alias"), signatureHandle);
			fixture.Metadata.AddFieldRelativeVirtualAddress (alias, rva);
			return (field, alias);
		}

		static void PatchImplMapMemberForwarded (byte [] image, FieldDefinitionHandle field)
		{
			int memberOffset;
			using (var peReader = new PEReader (ImmutableArray.Create (image))) {
				MetadataReader reader = peReader.GetMetadataReader ();
				Assert.AreEqual (1, reader.GetTableRowCount (TableIndex.ImplMap));
				memberOffset = peReader.PEHeaders.MetadataStartOffset +
					reader.GetTableMetadataOffset (TableIndex.ImplMap) + sizeof (ushort);
			}

			ushort codedIndex = checked ((ushort) (MetadataTokens.GetRowNumber (field) << 1));
			image [memberOffset] = (byte) codedIndex;
			image [memberOffset + 1] = (byte) (codedIndex >> 8);
		}

		static void MoveFieldRvaToAnotherSection (
			byte [] image,
			FieldDefinitionHandle firstField,
			FieldDefinitionHandle fieldToMove)
		{
			int fieldRvaOffset;
			int targetRva = 0;
			using (var peReader = new PEReader (ImmutableArray.Create (image))) {
				MetadataReader reader = peReader.GetMetadataReader ();
				int firstRva = reader.GetFieldDefinition (firstField).GetRelativeVirtualAddress ();

				foreach (SectionHeader section in peReader.PEHeaders.SectionHeaders) {
					int sectionSize = Math.Max (section.VirtualSize, section.SizeOfRawData);
					bool containsFirstField = firstRva >= section.VirtualAddress && firstRva - section.VirtualAddress < sectionSize;
					if (!containsFirstField && peReader.GetSectionData (section.VirtualAddress).Length >= sizeof (int)) {
						targetRva = section.VirtualAddress;
						break;
					}
				}

				Assert.AreNotEqual (0, targetRva, "The fixture needs a second non-empty PE section.");
				int row = MetadataTokens.GetRowNumber (fieldToMove);
				fieldRvaOffset = peReader.PEHeaders.MetadataStartOffset +
					reader.GetTableMetadataOffset (TableIndex.FieldRva) +
					(row - 1) * reader.GetTableRowSize (TableIndex.FieldRva);
			}

			image [fieldRvaOffset] = (byte) targetRva;
			image [fieldRvaOffset + 1] = (byte) (targetRva >> 8);
			image [fieldRvaOffset + 2] = (byte) (targetRva >> 16);
			image [fieldRvaOffset + 3] = (byte) (targetRva >> 24);
		}

		static byte [] ReadResource (PEReader peReader, MetadataReader reader, string name)
		{
			foreach (ManifestResourceHandle handle in reader.ManifestResources) {
				ManifestResource resource = reader.GetManifestResource (handle);
				if (reader.GetString (resource.Name) != name) {
					continue;
				}

				DirectoryEntry directory = peReader.PEHeaders.CorHeader.ResourcesDirectory;
				PEMemoryBlock block = peReader.GetSectionData (directory.RelativeVirtualAddress);
				int offset = checked ((int) resource.Offset);
				int size = block.GetReader (offset, sizeof (int)).ReadInt32 ();
				return block.GetReader (offset + sizeof (int), size).ReadBytes (size);
			}

			Assert.Fail ($"Resource '{name}' is missing.");
			return [];
		}
	}
}
